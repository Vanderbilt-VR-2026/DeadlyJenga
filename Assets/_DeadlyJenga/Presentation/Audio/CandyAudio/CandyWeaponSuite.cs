using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Approved per-weapon sounds (24 Nov 2026): four recorded takes per cue, picked at random.
    // Clips live in Resources/CandyAudio/WeaponSuite as "<weapon>_<cue>_01..04".
    public static class CandyWeaponSuite
    {
        const int Takes = 4;
        static readonly Dictionary<string, CandyAudioCue> cues = new Dictionary<string, CandyAudioCue>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => cues.Clear();

        // Plays the approved sounds for this action. Returns false when the action has none,
        // so the caller can keep the older shared sound.
        public static bool Play(EquipmentActionFrame action)
        {
            var engine = CandyAudioEngine.Instance;
            if (!engine) return false;
            int owner = action.actor?.GetHashCode() ?? 0;
            var spec = EquipmentSpecifications.Get(action.kind);

            if (action.action == EquipmentAction.Shot)
            {
                string melee = MeleeId(action);
                if (melee != null)
                {
                    bool played = Emit(engine, melee, "swing", action.position, owner);
                    if (action.endpoints != null && action.endpoints.Length > 0)
                    {
                        var point = action.endpoints[0];
                        played |= Emit(engine, melee, HitsPlayer(point) ? "hit-player" : "hit-block", point, owner);
                    }
                    return played;
                }

                string gun = RangedId(action);
                if (gun == null) return false;
                // The wrist laser fires every 0.1 s: its ignition and hum come from Held instead.
                bool fired = action.kind == EquipmentKind.WristLaser || Emit(engine, gun, "fire", action.position, owner);
                // Hitscan guns announce no separate impact, so their hits sound at the shot's endpoints.
                if (Hitscan(action.kind) && action.endpoints != null)
                {
                    int heard = 0;
                    foreach (var end in action.endpoints)
                    {
                        if (heard >= 2) break;
                        if ((end - action.position).magnitude >= spec.range - .12f) continue;
                        Emit(engine, gun, "impact", end, owner);
                        heard++;
                    }
                }
                return fired;
            }

            if (action.action == EquipmentAction.Impact)
            {
                string melee = MeleeId(action);
                if (melee != null)
                    return Emit(engine, melee, HitsPlayer(action.position) ? "hit-player" : "hit-block", action.position, owner);
                string gun = RangedId(action);
                return gun != null && Emit(engine, gun, "impact", action.position, owner);
            }

            if (action.action == EquipmentAction.Reload && action.kind == EquipmentKind.Blaster)
                return Emit(engine, "alien-blaster", "vent", action.position, owner);
            return false;
        }

        static string RangedId(EquipmentActionFrame action) => action.kind switch
        {
            EquipmentKind.Blaster => "alien-blaster",
            EquipmentKind.SheriffHammer => "sheriff",
            EquipmentKind.DualRifle => action.shotgun ? "duplex-shotgun" : "duplex-rifle",
            EquipmentKind.PoweredArmor => "iron-suit",
            EquipmentKind.RocketLauncher => "rpg",
            EquipmentKind.DualUzi => "spikers",
            EquipmentKind.ChainGun => "chaingun",
            EquipmentKind.GrenadeLauncher => "sugar-corps",
            EquipmentKind.WristLaser => "wrist-laser",
            EquipmentKind.NeedleDrill => "needle-drill",
            EquipmentKind.StockPistol => "stock-pistol",
            EquipmentKind.CompactSidearm => "compact-sidearm",
            EquipmentKind.Chainsaw when action.shotgun => "saw-shooter",
            EquipmentKind.LaserAxe when action.shotgun => "storm-axe-bolt",
            _ => null
        };

        static string MeleeId(EquipmentActionFrame action) => action.kind switch
        {
            EquipmentKind.MurderSword => "murderer-sword",
            EquipmentKind.BarbedBat => "barbed-bat",
            EquipmentKind.SawStaff => "saw-staff",
            EquipmentKind.Dagger => "dagger",
            EquipmentKind.BoxingGlove => "boxing-glove",
            EquipmentKind.LaserClaws => "laser-claws",
            EquipmentKind.FlamingMarshmallow => "flaming-marshmallow",
            EquipmentKind.LaserAxe when !action.shotgun => "storm-axe",
            _ => null
        };

        static bool Hitscan(EquipmentKind kind) => kind is EquipmentKind.DualRifle or EquipmentKind.SheriffHammer or
            EquipmentKind.StockPistol or EquipmentKind.ChainGun or EquipmentKind.NeedleDrill or EquipmentKind.WristLaser;

        // Trigger-held sounds, called every frame per player: chaingun spin up/down and the
        // wrist laser's ignition, hum and cut-off. Returns true when it voiced this weapon.
        public static bool Held(Transform player, EquipmentKind kind, bool held, bool wasHeld, bool local)
        {
            var engine = CandyAudioEngine.Instance;
            if (!engine || !player) return false;
            int owner = player.GetInstanceID();
            if (kind == EquipmentKind.ChainGun)
            {
                if (held && !wasHeld) Emit(engine, "chaingun", "spinup", player.position, owner, local);
                else if (!held && wasHeld) Emit(engine, "chaingun", "spindown", player.position, owner, local);
                return true;
            }
            if (kind == EquipmentKind.WristLaser)
            {
                if (held && !wasHeld) Emit(engine, "wrist-laser", "fire", player.position, owner, local);
                if (held) engine.LoopCue(Cue("wrist-laser", "loop"), player, .8f, local);
                else if (wasHeld) Emit(engine, "wrist-laser", "stop", player.position, owner, local);
                return true;
            }
            return false;
        }

        // A one-off sound from this weapon's approved set (for example "spikers", "shatter").
        public static bool PlayExtra(string weapon, string cue, Vector3 position, int owner = 0)
        {
            var engine = CandyAudioEngine.Instance;
            return engine && Emit(engine, weapon, cue, position, owner);
        }

        // Keeps a looping sound from the approved set on a moving object (for example "rpg", "flight").
        public static void Loop(string weapon, string cue, Transform owner, float intensity)
        {
            var engine = CandyAudioEngine.Instance;
            if (engine) engine.LoopCue(Cue(weapon, cue), owner, intensity);
        }

        static bool HitsPlayer(Vector3 point) =>
            Physics.CheckSphere(point, .14f, 1 << Layers.Survivor, QueryTriggerInteraction.Collide);

        static bool Emit(CandyAudioEngine engine, string weapon, string cue, Vector3 position, int owner, bool local = false)
        {
            var sound = Cue(weapon, cue);
            return sound != null && engine.PlayCue(sound, position, 1, owner, 1, local);
        }

        static CandyAudioCue Cue(string weapon, string cue)
        {
            string key = weapon + "_" + cue;
            if (cues.TryGetValue(key, out var found)) return found;
            var takes = new List<AudioClip>(Takes);
            for (int i = 1; i <= Takes; i++)
            {
                var clip = Resources.Load<AudioClip>("CandyAudio/WeaponSuite/" + key + "_" + i.ToString("00"));
                if (clip) takes.Add(clip);
            }
            if (takes.Count == 0) { cues[key] = null; return null; }

            bool fire = cue == "fire";
            bool swing = cue == "swing";
            bool explosion = weapon is "rpg" or "sugar-corps" or "storm-axe-bolt" && cue == "impact";
            var made = new CandyAudioCue
            {
                sound = CandySignatureSounds.Id("suite/" + key),
                displayName = weapon + " " + cue,
                category = "Weapons",
                variants = takes.ToArray(),
                bus = CandyBus.Character,
                volume = fire ? .85f : swing ? .6f : explosion ? .95f : .75f,
                pitch = new Vector2(.98f, 1.02f),
                cooldown = weapon == "chaingun" ? .045f : weapon == "wrist-laser" && cue == "impact" ? .3f : .03f,
                maxVoices = weapon == "chaingun" ? 3 : 6,
                priority = explosion ? 30 : fire ? 45 : 70,
                minDistance = explosion ? 6 : fire ? 3 : swing ? 1.3f : 2,
                maxDistance = explosion ? 160 : fire ? 90 : swing ? 24 : 70
            };
            cues[key] = made;
            return made;
        }
    }
}
