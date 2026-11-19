using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DeadlyJenga.CandyAudio.Editor
{
    [Serializable]
    public sealed class CandyCaptureBrief
    {
        public int cueId;
        public string title, category, source, action, target, listenerRole, camera, audibleMoment, reject;
        public bool verifyCaller = true;
    }
    public static class CandySoundCaptureBriefs
    {
        public static CandyCaptureBrief For(CandySoundEntry entry)
        {
            string name = entry.id.ToString();
            var b = new CandyCaptureBrief { cueId = (int)entry.id, title = entry.title, category = entry.category,
                source = "Resolve the sound-producing object from the linked caller; do not infer it from the filename.",
                action = entry.title, target = "Resolve the affected object, surface or player; use 'none' only when there is no target.",
                listenerRole = "Resolve local/remote listener routing from the caller.",
                camera = "One stable gameplay angle showing the source, the action and its consequence together.",
                audibleMoment = "The actual cue request in the linked caller; align the event marker to that visible instant.",
                reject = "A beauty shot, wrong phase, unrelated action, stale map, hidden contact, or a cue name without caller evidence." };
            if (name.StartsWith("Kraken"))
            {
                b.verifyCaller = false; b.source = "The specific Kraken body or tentacle performing this phase";
                b.listenerRole = "Nearby survivor hearing spatial Kraken audio";
                b.camera = "Stable three-quarter survivor-height view; include the waterline or contacted tower. Keep the acting arm large enough to read, never inside it.";
                b.target = "Water or the exact tower/block/player the arm is acting upon";
                switch (entry.id)
                {
                    case CandySound.KrakenRise: b.action = "Kraken rises from below the waterline"; b.audibleMoment = "Body rise crosses 0.05 (KrakenPresentation.Sounds). Show the emergence, not an already-emerged idle body."; break;
                    case CandySound.KrakenIdle: b.action = "Exposed Kraken body idles and breathes above water"; b.audibleMoment = "Sustained idle loop while rise is above 0.05"; break;
                    case CandySound.KrakenSurface: b.action = "One arm surfaces into its Resting state"; b.audibleMoment = "That arm enters Resting; frame its waterline, not the entire Kraken's initial rise"; break;
                    case CandySound.KrakenReach: b.action = "Tentacle extends toward its chosen target"; b.audibleMoment = "Arm enters Reaching; include the intended target and closing gap"; break;
                    case CandySound.KrakenGrip: b.action = "Tentacle closes around and holds its target"; b.audibleMoment = "Arm enters Holding; contact/grip must be visibly established"; break;
                    case CandySound.KrakenDrag: b.action = "Tentacle pulls its held target away"; b.audibleMoment = "Arm enters Dragging; show actual target displacement"; break;
                    case CandySound.KrakenWindup: b.action = "Tentacle rears back before its slam"; b.audibleMoment = "Arm enters Rearing; stop this clip before confusing the windup with the impact"; break;
                    case CandySound.KrakenSlam: b.action = "Tentacle smacks the tower or player and causes visible impact"; b.audibleMoment = "First accepted Slamming cue: phase progress >=0.68, or >=0.15 with nearby Block/Survivor overlap. Frame real contact and its consequence; if timing precedes visible contact, report the mismatch instead of faking it."; break;
                    case CandySound.KrakenSubmerge: b.action = "One tentacle sinks below the waterline"; b.audibleMoment = "A previously observed arm enters Sinking"; break;
                    case CandySound.KrakenRetreat: b.action = "The entire Kraken withdraws at disaster end"; b.audibleMoment = "Remaining time reaches zero after body rise; distinguish this from one arm submerging"; break;
                }
                b.reject = "Reusing the Kraken rise clip for another phase; an arm swinging in empty air for Slam; contact hidden behind blocks; unrelated tentacle motion.";
                return b;
            }
            if (entry.id == CandySound.HitConfirm || entry.id == CandySound.KillConfirm ||
                entry.id == CandySound.PersonalShot || entry.id == CandySound.NearbyShot || entry.id == CandySound.PersonalHit || entry.id == CandySound.NearbyHit)
            {
                b.verifyCaller = false; b.source = "Damage-causing projectile, melee weapon, or hazard at the victim's impact point";
                b.target = "The damaged player, with readable contact/reaction";
                b.listenerRole = entry.id == CandySound.HitConfirm || entry.id == CandySound.KillConfirm ? "Attacker receiving confirmation" :
                    entry.id == CandySound.PersonalHit || entry.id == CandySound.PersonalShot ? "Victim receiving personal damage feedback" : "Nearby observer, neither attacker nor victim";
                b.action = entry.id == CandySound.KillConfirm ? "A confirmed hit eliminates the victim" : "A confirmed hit damages the victim";
                b.camera = b.listenerRole.StartsWith("Victim") ? "Victim first-person with incoming source visible and hit feedback readable; a contextual third-person inset may be a separate optional clip." : "Close attacker/observer view that includes the victim and actual impact; muzzle-only footage is insufficient.";
                b.audibleMoment = "MatchModeRuntime.SoundSuite routes successful damage at impactPoint. PersonalShot and NearbyShot mean being hit by / observing a projectile hit, NOT the weapon firing.";
                b.reject = "Only a firing gun, a miss, wrong listener role, an already-dead target, or a damage animation without the triggering hit.";
                return b;
            }
            if (entry.category.StartsWith("Combat / ") && entry.symbol == "CandySignatureSounds")
            {
                string action = entry.title.Split('·')[0].Trim(); b.verifyCaller = false;
                b.source = entry.category.Substring("Combat / ".Length) + " in the acting player's hands";
                b.target = action == "Impact" ? "The exact victim, block or surface contacted by this weapon/projectile" : "The weapon, holder and relevant nearby target";
                b.action = action + " with " + entry.category.Substring("Combat / ".Length);
                b.audibleMoment = "The matching EquipmentAction frame. Verify that this weapon actually emits this action/alternate flag; reserved catalog routes must remain uncaptured.";
                b.camera = action == "Impact" ? "Close three-quarter view of the impact point and victim/block response; include enough incoming path to establish the cause." :
                    action == "Reload" || action == "Draw" || action == "Holster" || action == "Mode Switch" ? "First-person or close over-shoulder showing hands, full weapon and complete handling motion." : "First-person/over-shoulder with the weapon action and immediate result visible.";
                if (action == "Shot") b.action = "Weapon attack / swing / discharge (inspect this weapon's caller; a melee Shot is not a bullet)";
                b.reject = "Wrong weapon or alternate mode; idle weapon; a muzzle shot substituted for an impact; a reserved action that gameplay never emits.";
                return b;
            }
            if (entry.category.StartsWith("Player / Movement") || entry.category.StartsWith("Player / Climbing"))
            {
                b.source = "The moving player's feet, hands or exertion"; b.target = "The contacted ground or climbable ledge; show its material";
                b.listenerRole = "That player for local breathing; nearby listener for movement contacts";
                b.action = entry.title; b.camera = name.StartsWith("Footstep") || name.StartsWith("Land") ? "Low three-quarter view showing feet and the actual surface contact; include enough body to read the motion." : "Player-height three-quarter or first-person view that clearly shows the relevant hand/ledge/body motion.";
                b.reject = "A walking torso without foot contact, wrong ground material, static climbing pose, or a different movement state.";
            }
            if (entry.category.StartsWith("Map ambience") || entry.category.StartsWith("Music"))
            {
                b.source = "The actual environment or menu where this bed is active"; b.action = "Sustained environmental/menu state";
                b.target = "none (ambient bed)"; b.listenerRole = "Local listener in that environment";
                b.camera = "Stable representative player view with readable contextual motion (surf, foliage or menu state).";
                b.reject = "Random scenery from another map, a still photo labeled gameplay, or unrelated catastrophic action.";
            }
            return b;
        }
        public static string Text(CandySoundEntry entry)
        {
            var b = For(entry); var text = new StringBuilder();
            text.AppendLine("DEADLY JENGA · TRIGGER PREVIEW CAPTURE");
            text.AppendLine("Cue ID: " + b.cueId + " | " + b.category + " | " + b.title);
            text.AppendLine("SOURCE: " + b.source); text.AppendLine("ACTION: " + b.action);
            text.AppendLine("TARGET: " + b.target); text.AppendLine("LISTENER: " + b.listenerRole);
            text.AppendLine("CAMERA: " + b.camera); text.AppendLine("AUDIBLE MOMENT: " + b.audibleMoment);
            text.AppendLine("REJECT: " + b.reject);
            text.AppendLine("Caller review: " + (b.verifyCaller ? "Required: resolve the contextual roles before staging; this is a planning template, not verified runtime evidence." : "Context mapped from source; verify the current caller and actual event timing before capture."));
            text.AppendLine("Current trigger: " + entry.trigger);
            foreach (var use in entry.uses.Take(12)) text.AppendLine("Inspect: " + use.path + ":" + use.line + " " + use.text);
            text.AppendLine("Capture 1–5 seconds of actual gameplay, including 0.3–1 s of anticipation, the action and its aftermath. No unrelated camera cuts. Never retime or synthesize an action to hide a timing bug.");
            text.AppendLine("Read SOUND_PREVIEW_CAPTURE_GUIDE.md. Package with build_trigger_preview.py; import preview.json using Sound Studio > Library > Import trigger previews. Preserve stable cue ID. Real captures are supplied separately; no concept or example fixture is accepted as gameplay evidence.");
            return text.ToString();
        }
        public static void Export(string folder, CandyAudioBank bank)
        {
            Directory.CreateDirectory(folder);
            var entries = CandySoundCatalog.Build(bank);
            foreach (var entry in entries) File.WriteAllText(Path.Combine(folder, "cue-" + (int)entry.id + ".md"), Text(entry));
            File.WriteAllText(Path.Combine(folder, "README.md"), "# Trigger capture queue\n\n" + entries.Count + " stable sound routes. These are capture plans, not footage. Read the project SOUND_PREVIEW_CAPTURE_GUIDE.md first. Capture active routes only; mark dormant/ambiguous routes blocked instead of fabricating a clip.\n");
        }
    }
}
