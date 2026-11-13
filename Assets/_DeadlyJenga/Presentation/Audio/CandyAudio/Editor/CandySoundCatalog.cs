using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CandyAudio.Editor
{
    public sealed class CandySoundEntry
    {
        public CandySound id;
        public string title, category, trigger, symbol;
        public bool direct;
        public Func<CandyAudioCue> fallback;
        public readonly List<CandySoundUse> uses = new();
    }
    public sealed class CandySoundUse
    {
        public string path, text;
        public int line;
    }

    public static class CandySoundCatalog
    {
        static Dictionary<string,List<CandySoundUse>> sourceCache;
        public static void RefreshSourceIndex() => sourceCache = null;
        public static string Label(string value) => Regex.Replace(value ?? "", "(?<=[a-z0-9])([A-Z])", " $1");
        public static CandyAudioCue Authored(CandyAudioBank bank, CandySound id)
            => bank && bank.cues != null ? Array.Find(bank.cues, c => c != null && c.sound == id) : null;

        public static List<CandySoundEntry> Build(CandyAudioBank bank)
        {
            var entries = new List<CandySoundEntry>();
            foreach (CandySound id in Enum.GetValues(typeof(CandySound)))
            {
                var captured = id;
                entries.Add(new CandySoundEntry { id = id, title = Label(id.ToString()), category = Category(id),
                    trigger = Describe(id), symbol = "CandySound." + id, fallback = () => CandyAudioDefaults.Find(captured) });
            }
            AddDirect(entries, CandyDirectAudio.Home, "Home menu / Shorebreak", "Music / Menus", "CandyHomeMusic",
                "Loops while the home screen is active; stops when the home screen closes.", "CandyMenuMusic/Shorebreak-Home");
            AddDirect(entries, CandyDirectAudio.Lobby, "Lobby / Ready Set Splash", "Music / Menus", "CandyLobbyMusic",
                "Loops while the local player is present in the lobby. Pauses when the player leaves for the match.", "CandyMenuMusic/ReadySetSplash-Lobby");
            AddDirect(entries, CandyDirectAudio.Gulls, "Marina gull calls", "Map ambience / Marina", "MarinaGullAmbience",
                "Marina-only calls from four positions. First call after 5 seconds, then roughly every 13–21 seconds while the local player is in the lobby.", "CandyAudio/MarinaGullCalls");

            foreach (EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
            {
                if (kind <= EquipmentKind.PoweredArmor || kind == EquipmentKind.FlyingHand) continue;
                foreach (EquipmentAction action in Enum.GetValues(typeof(EquipmentAction)))
                {
                    if (action == EquipmentAction.Bash) continue;
                    AddWeapon(entries, kind, action, false);
                    if (action == EquipmentAction.Shot) AddWeapon(entries, kind, action, true);
                }
            }
            // Older weapons also use signatures for reload and global murder-mystery reports.
            foreach (var kind in new[] { EquipmentKind.MurderSword, EquipmentKind.SheriffHammer, EquipmentKind.DualRifle, EquipmentKind.Blaster })
                AddWeapon(entries, kind, EquipmentAction.Reload, false);
            AddWeapon(entries, EquipmentKind.MurderSword, EquipmentAction.Shot, false);
            AddWeapon(entries, EquipmentKind.SheriffHammer, EquipmentAction.Shot, false);
            foreach (var emote in Emotes.Survivor.Concat(Emotes.Hand))
            {
                if (Enum.TryParse<CandySound>(emote, out _)) continue;
                string captured = emote;
                entries.Add(new CandySoundEntry { id = CandySignatureSounds.Id("emote/" + emote), title = Label(emote), category = "Player / Emotes",
                    symbol = "CandySignatureSounds.PlayEmote", trigger = "Emote '" + emote + "' reported by the player audio observer. Hand-only emotes need that observer to invoke the cue; see source references.",
                    fallback = () => CandySignatureSounds.DescribeEmote(captured) });
            }
            for (int i = 0; i < CandyWildlifeAudio.Species.Length; i++)
            {
                string species = CandyWildlifeAudio.Species[i];
                entries.Add(new CandySoundEntry { id = (CandySound)(900 + i), title = Label(species), category = "Wildlife / Calls",
                    symbol = "CandyWildlifeAudio", trigger = "Nearest eligible " + species + " within 18 m. Each animal waits 7–15 seconds between calls; wildlife calls share a 1.2-second global gap.",
                    fallback = () => CandyWildlifeAudio.Cue(species) });
            }
            var ninja = new[] {
                ("Rain", "rain", "Steady rain in Ninja Temple", true),
                ("Wind", "bamboo wind", "Wind through temple bamboo", true),
                ("Water", "lake", "Temple lake ambience", true),
                ("Gong", "gong", "Lantern gong event, heard globally", false),
                ("Poof", "smoke bomb", "Lantern knock/drop or a player entering dying state", false),
                ("Chime", "lantern chime", "Lantern grab, steal and flee accents", false),
                ("Steal", "koto steal", "Lantern stolen from its carrier", false),
                ("Whoosh", "spirit whoosh", "Lantern spirit flees", false),
                ("Hum", "lantern hum", "Spatial loop attached to the lantern", true),
                ("Taiko", "taiko", "Temple taiko bed follows match tension", true),
                ("Clack", "hyoshigi", "Occasional positional temple clack", false)
            };
            foreach (var item in ninja)
            {
                var captured = item;
                entries.Add(new CandySoundEntry { id = DeadlyJenga.NinjaArt.NinjaSounds.SoundId("Ninja " + item.Item2),
                    title = item.Item1, category = "Maps / Ninja Temple", symbol = "NinjaSounds." + item.Item1,
                    trigger = item.Item3 + ".", direct = item.Item4, fallback = () => new CandyAudioCue {
                        sound = DeadlyJenga.NinjaArt.NinjaSounds.SoundId("Ninja " + captured.Item2),
                        variants = new[] { (AudioClip)typeof(DeadlyJenga.NinjaArt.NinjaSounds).GetProperty(captured.Item1).GetValue(null) },
                        bus = captured.Item4 ? CandyBus.Ambience : CandyBus.World, volume = 1, loop = captured.Item4,
                        minDistance = 4, maxDistance = 70, priority = 40, cooldown = .05f, maxVoices = 3, pitch = new Vector2(.97f,1.03f) } });
            }
            if (bank && bank.cues != null) foreach (var cue in bank.cues)
            {
                if (cue == null) continue;
                var entry = entries.Find(e => e.id == cue.sound);
                if (entry == null) { entry = new CandySoundEntry { id = cue.sound, title = "Custom " + (int)cue.sound, category = "Custom / Unsorted", trigger = "Manual emitter or authored event binding.", symbol = "" }; entries.Add(entry); }
                if (!string.IsNullOrWhiteSpace(cue.displayName)) entry.title = cue.displayName;
                if (!string.IsNullOrWhiteSpace(cue.category)) entry.category = cue.category;
                if (!string.IsNullOrWhiteSpace(cue.triggerNotes)) entry.trigger = cue.triggerNotes;
            }
            IndexSources(entries);
            return entries.OrderBy(e => e.category).ThenBy(e => e.title).ToList();
        }

        static void AddDirect(List<CandySoundEntry> entries, CandySound id, string title, string category, string symbol, string trigger, string resource)
        {
            entries.Add(new CandySoundEntry { id = id, title = title, category = category, symbol = symbol, direct = true, trigger = trigger,
                fallback = () => new CandyAudioCue { sound = id, variants = new[] { Resources.Load<AudioClip>(resource) }, volume = 1,
                    pitch = Vector2.one, bus = id == CandyDirectAudio.Gulls ? CandyBus.Ambience : CandyBus.Music, loop = id != CandyDirectAudio.Gulls } });
        }
        static void AddWeapon(List<CandySoundEntry> entries, EquipmentKind kind, EquipmentAction action, bool wide)
        {
            string key = kind + "/" + action + (action == EquipmentAction.Shot ? wide ? "/wide" : "/precision" : "");
            entries.Add(new CandySoundEntry { id = CandySignatureSounds.Id(key), category = "Combat / " + Label(kind.ToString()),
                title = Label(action.ToString()) + (action == EquipmentAction.Shot ? wide ? " · alternate" : " · primary" : ""),
                trigger = "Equipment action " + action + " for " + kind + (action == EquipmentAction.Shot ? (wide ? ", shotgun/alternate flag true." : ", shotgun/alternate flag false.") : ".") +
                    " Consumed once from the match equipment-action sequence; only plays when that weapon actually emits this action. Editing a sound does not add weapon abilities.",
                symbol = "CandySignatureSounds", fallback = () => CandySignatureSounds.DescribeWeapon(new EquipmentActionFrame { kind = kind, action = action, shotgun = wide }) });
        }

        static string Category(CandySound sound)
        {
            string s = sound.ToString();
            if (s.StartsWith("Kraken")) return "Disasters / Kraken";
            if (s.StartsWith("Wave") || s == "PuddleDrain") return "Disasters / Tsunami";
            if (s.StartsWith("Acid")) return "Disasters / Acid rain";
            if (s.StartsWith("Tornado") || s.StartsWith("Cyclone")) return "Disasters / Tornado";
            if (s.StartsWith("Earth")) return "Disasters / Earthquake";
            if (s.StartsWith("Meteor") || s.StartsWith("Planet")) return "Disasters / Meteors and planet";
            if (s.StartsWith("Fire")) return "Disasters / Fire";
            if (s.StartsWith("Frost")) return "Disasters / Ice";
            if (s.StartsWith("Power") || s.StartsWith("Outage")) return "Disasters / Power outage";
            if (s.StartsWith("Vampire")) return "Disasters / Vampire";
            if (s.StartsWith("Rewind")) return "Disasters / Rewind";
            if (s.StartsWith("LowGravity")) return "Disasters / Low gravity";
            if (s == "PlayerHeal") return "Player / Health and death";
            if (s.StartsWith("Disaster")) return "Disasters / Warnings";
            if (s.StartsWith("Weapon") || s.Contains("Shot") || s.Contains("Hit") && !s.StartsWith("Block") || s.Contains("Kill")) return "Combat / Shared feedback";
            if (s.StartsWith("Suit")) return "Combat / Iron suit";
            if (s.StartsWith("Jaguar")) return "Wildlife / Jaguar";
            if (s.StartsWith("Block") || s == "StructureLean") return "World / Blocks";
            if (s.StartsWith("Island")) return "World / Sinking island";
            if (s.StartsWith("Hand") || s.StartsWith("Finger") || s.StartsWith("Rubber") || s == "PushImpact") return "Flying hand / Movement and contact";
            if (s.StartsWith("Climb") || s.StartsWith("Vault") || s.StartsWith("Breath") || s.StartsWith("Grip") || s.StartsWith("Stamina")) return "Player / Climbing and stamina";
            if (s.StartsWith("Footstep") || s.StartsWith("Land") || s == "Jump" || s == "FallWind" || s == "PlayerSplash") return "Player / Movement";
            if (s.StartsWith("Player") || s.StartsWith("Health") || s.Contains("Death")) return "Player / Health and death";
            if (s.StartsWith("Beam") || s.StartsWith("Spawn") || s.StartsWith("Respawn") || s == "LobbyLaunch") return "Player / Travel and spawning";
            if (s == "BeachWind" || s == "Surf") return "Map ambience / Ocean";
            if (s == "Music") return "Music / Match";
            if (s == "UiClick" || s.StartsWith("Score") || s.StartsWith("Point") || s == "RoundResults" || s == "ClaimWin") return "UI / Menus and results";
            return "Player / Emotes";
        }
        static string Describe(CandySound sound)
        {
            string s = sound.ToString();
            if (s.StartsWith("Kraken")) return "Kraken " + Label(s.Substring(6)).ToLowerInvariant() + " phase, observed from the active tentacle/creature presentation. Only the active phase requests the cue; spatial falloff and voice limits apply.";
            if (s.StartsWith("Weapon")) return "Shared equipment feedback for " + Label(s.Substring(6)).ToLowerInvariant() + ". Newer weapons use their own Combat subcategory; source references show which actions use this shared cue.";
            if (s.StartsWith("Jaguar")) return "Jaguar " + Label(s.Substring(6)).ToLowerInvariant() + " state or enemy action. Local damage/death events and movement observation are deduplicated by the wildlife observer.";
            if (Emotes.Survivor.Contains(s) || Emotes.Hand.Contains(s)) return "GameEvents.Emoted reports '" + s + "' for this character. The player audio observer plays it once when the character is within 70 m of the listener. " + CandyAudioStudio.Description(sound);
            return sound switch {
                CandySound.FootstepCandy => "Grounded travel passes the step-distance threshold while not climbing or vaulting. A downward surface check selects this cue for non-sand surfaces; minimum step gap is 0.19 s. Gain follows movement speed.",
                CandySound.FootstepSand => "Same grounded step-distance trigger as candy footsteps, but the surface is tagged Sand or the ground check finds no surface. Minimum step gap is 0.19 s.",
                CandySound.Jump => "Character leaves the ground with upward speed above 2 m/s, outside a vault.",
                CandySound.LandSoft => "Character becomes grounded after more than 0.1 s airborne, with recorded falling speed at or below 5 m/s.",
                CandySound.LandHard => "Character becomes grounded after more than 0.1 s airborne, with recorded falling speed above 5 m/s. Gain grows with falling speed.",
                CandySound.VaultGrunt => "Movement state reports a new vault count; emitted once for that vault.",
                CandySound.ClimbPlant => "Climbing begins, a new vault is reported, or a moving climbing grip settles into contact. Repeated grip plants are limited to one per hand per 0.25 s.",
                CandySound.ClimbGrunt => "Character moves vertically during climbing, at a spaced interval of roughly 1.5–2.1 s.",
                CandySound.ClimbRelease => "Character leaves climbing state without entering a vault.",
                CandySound.FallWind => "Continuous loop while downward speed exceeds 3 m/s, outside climbing and vaulting. Gain and pitch rise with falling speed.",
                CandySound.Spawn => "The survivor's movement-audio observer initializes. Jaguar actors use their own audio instead.",
                CandySound.Respawn => "The movement observer detects a position jump greater than 4 m in one frame and resets its step/fall history. Includes teleports, not only respawns.",
                CandySound.BreathExertion => "Local player is climbing with nonempty stamina, input is active, and tsunami immersion is below 30%. Uses the reserved breathing loop voice.",
                CandySound.BreathRecovery => "Local player has partially depleted stamina but is no longer climbing. Stops once stamina is full, input is paused, or the player is submerged.",
                CandySound.BreathExhausted => "Local stamina reaches zero. Paused input or substantial tsunami immersion suppresses this breathing loop.",
                CandySound.GripWarning => "Legacy grip-warning cue, also used by hand-look audio styling. Current player stamina warnings use Stamina Low/Empty/Usable; see linked callers.",
                CandySound.HandFlight => "Giant hand movement speed exceeds 0.5 m/s; loop gain and pitch increase with speed.",
                CandySound.BeachWind => "Ambient loop follows the listener. Height and exposure increase wind; shelter reduces and muffles it.",
                CandySound.Surf => "Ambient ocean loop follows the listener, becoming quieter with height and muffled under shelter.",
                CandySound.Music => "Match music loop while world music is enabled and neither dedicated home nor lobby music is active. Mix follows tension.",
                CandySound.BlockTap => "Block collision strength is at most 0.24; a small contact accent is selected.",
                CandySound.BlockHit => "Block collision strength is above 0.24 and at most 0.72; the medium impact is selected.",
                CandySound.BlockSmash => "Block collision strength exceeds 0.72, or presentation reports a block-break accent. Voice limits prevent every debris impact from overlapping.",
                CandySound.BlockScrape => "A block maintains contact with sliding speed above 0.25 m/s and collision impulse above 1. Loop gain follows sliding speed.",
                CandySound.DisasterHorn => "HazardStarted announcement horn, gated by the disaster feedback observer.",
                CandySound.DisasterTension => "Rising tension bed requested by active disaster feedback.",
                CandySound.WaveImpact => "Tsunami impact/contact report; gain follows reported strength.",
                CandySound.WaveSpray => "Spray and water contact accent from tsunami feedback.",
                CandySound.PuddleDrain => "Tsunami residual water drains away.",
                CandySound.MeteorWhistle => "Legacy approaching meteor cue. Check source references for active callers.",
                CandySound.FingerFlex => "Giant hand finger bend or grip motion crosses its audio threshold.",
                CandySound.HandSwirl => "Giant hand motion accent reported by the hand observer.",
                CandySound.PushImpact => "Push/contact feedback from the giant hand.",
                CandySound.ClaimWin => "Giant hand victory claim is announced.",
                CandySound.IslandSink => "Legacy island sinking warning from hazard presentation.",
                CandySound.IslandSinking => "Host-selected island sinking warning for this listener.",
                CandySound.LobbyLaunch => "Lobby launch / round departure feedback.",
                CandySound.SpawnSlam => "Arrival contact after the player's fly-in.",
                CandySound.PlayerHeal => "Survivor health increases; deduplicated by the player audio observer.",
                CandySound.AcidRoofRain => "Rain patter when the listener has overhead shelter during acid rain. Fades with the shelter estimate.",
                CandySound.AcidSizzle => "Outdoor acid-rain bed, reduced and muffled under cover.",
                CandySound.AcidBurn => "Local player takes acid-rain damage while exposed.",
                CandySound.CycloneWind => "Loop while a tornado hazard is active; intensity follows proximity and strength.",
                CandySound.CycloneDebris => "Nearby tornado debris layer, scaled by proximity and ferocity.",
                CandySound.TornadoPass => "One-shot when a tornado makes a nearby pass.",
                CandySound.TornadoPressure => "Local pressure layer while the listener is inside the tornado.",
                CandySound.EarthRumble => "Continuous bed for an active earthquake.",
                CandySound.EarthJolt => "Initial earthquake jolt when the disaster starts.",
                CandySound.FireCrackle => "Fire disaster crackle loop while fire is active.",
                CandySound.FrostCreak => "Ice disaster ambience while Icey is active.",
                CandySound.LowGravityHum => "Low-gravity disaster ambience while AntiGravity is active.",
                CandySound.PowerDown => "PowerOutage begins.", CandySound.PowerUp => "PowerOutage ends.",
                CandySound.OutageHum => "Low electrical bed during PowerOutage.",
                CandySound.MeteorFlyby => "Approaching meteor, with gain and pitch driven by distance.",
                CandySound.MeteorImpact => "MeteorLandContact is reported.", CandySound.MeteorWaterImpact => "MeteorWaterContact is reported.",
                CandySound.PlanetApproach => "Approaching planet-killer hazard, scaled by distance.",
                CandySound.PlanetImpact => "PlanetLandContact or PlanetWaterContact is reported.",
                CandySound.VampireSizzle => "Listener becomes exposed to sun during Vampire.", CandySound.VampireShade => "Listener reaches shade during Vampire.",
                CandySound.RewindStart => "Rewind state changes from inactive to active.", CandySound.RewindBed => "Loop while rewind is active.",
                CandySound.RewindRelease => "Rewind finishes in the same session.",
                CandySound.DisasterWarningTone => "Accepted upcoming disaster warning; see the countdown/announcement gate in CandyFeedback.DisasterMix.",
                CandySound.WaveRoar => "Nearby tsunami body roar, scaled by wave proximity.",
                CandySound.WaveSubmerged => "Listener is immersed inside the tsunami; underwater muffling is mixed separately.",
                CandySound.WaveSurfaceBreath => "Listener emerges from tsunami immersion.",
                CandySound.BeamFlyout => "DepartureStarted announces the player's travel out.",
                CandySound.BeamArrival => "Arrival emote is reported after travel.",
                CandySound.StaminaLow => "Local stamina crosses the low threshold.", CandySound.StaminaEmpty => "Local stamina becomes empty.",
                CandySound.StaminaUsable => "Local stamina becomes usable again after being empty.",
                CandySound.HitConfirm or CandySound.KillConfirm => "Host-selected MatchSound feedback for this listener; dedicated low-latency confirmation speaker.",
                CandySound.PersonalHit or CandySound.PersonalShot or CandySound.NearbyHit or CandySound.NearbyShot or CandySound.PlayerKilled or CandySound.NearbyDeath or CandySound.IslandDeath or CandySound.DrowningDeath => "Host-selected MatchSound event for this listener: " + Label(s) + ".",
                CandySound.ScoreboardOpen => "Scoreboard UI opens.", CandySound.PointScored => "Scoring feedback is requested by presentation.",
                CandySound.RoundResults => "Round results are presented.", CandySound.HealthRegenerating => "Health regeneration presentation requests feedback.",
                _ => CandyAudioStudio.Description(sound)
            };
        }

        static void IndexSources(List<CandySoundEntry> entries)
        {
            if (sourceCache != null)
            {
                foreach (var entry in entries) if (!string.IsNullOrEmpty(entry.symbol) && sourceCache.TryGetValue(entry.symbol, out var uses)) entry.uses.AddRange(uses);
                return;
            }
            string root = ProjectPath.FolderOf("CandyAudioBank.cs");
            root = root.Substring(0, root.IndexOf("/Audio/", StringComparison.Ordinal));
            var symbols = entries.Where(e => !string.IsNullOrEmpty(e.symbol)).GroupBy(e => e.symbol).ToDictionary(g => g.Key, g => g.ToArray());
            var pattern = new Regex(string.Join("|", symbols.Keys.OrderByDescending(k => k.Length).Select(k => Regex.Escape(k) + @"(?!\w)")));
            foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/'), name = Path.GetFileName(path);
                if (normalized.Contains("/Editor/") || name == "CandyAudioBank.cs" || name == "CandyAudioStudio.cs" || name == "CandyAudioDefaults.cs") continue;
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++) foreach (Match match in pattern.Matches(lines[i]))
                    foreach (var entry in symbols[match.Value])
                        if (entry.uses.Count < 40) entry.uses.Add(new CandySoundUse { path = normalized, line = i + 1, text = lines[i].Trim() });
            }
            sourceCache = entries.Where(e => !string.IsNullOrEmpty(e.symbol)).GroupBy(e => e.symbol)
                .ToDictionary(g => g.Key, g => new List<CandySoundUse>(g.First().uses));
        }
    }
}
