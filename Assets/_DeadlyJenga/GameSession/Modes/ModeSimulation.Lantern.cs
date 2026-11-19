using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    // Shadow Lantern: one spirit lantern, free-for-all. The carrier scores; any hit knocks it loose.
    public sealed partial class ModeSimulation
    {
        internal System.Func<Vector3,Vector3,bool> LanternPathClear;
        public const float LanternReach = 1.8f, LanternRegrabSeconds = 2f;
        readonly LanternFrame lantern = new LanternFrame();
        float lanternIdle, lanternLockUntil, carrierHealth, carriedFor;
        string lanternLockedFor;
        bool lanternFlee;
        public LanternFrame Lantern => lantern;
        public bool NinjaRound => Rules.mode == GameModeKind.Ninja;
        public bool LanternNeedsPerch { get; private set; }
        public bool GongActive => NinjaRound && lantern.gongRemaining > 0;
        public bool CarriesLantern(string id) => NinjaRound && id != null && lantern.carrier == id;

        void ResetLantern()
        {
            lantern.active = NinjaRound; lantern.carrier = null; lantern.gongRemaining = 0; lantern.holdRemaining = 0; carriedFor = 0; lantern.gongNextIn = Mathf.Max(5, Rules.gongInterval);
            lantern.eventKind = lantern.eventActor = lantern.eventVictim = null;
            lanternIdle = 0; lanternLockUntil = 0; lanternLockedFor = null; lanternFlee = false; LanternNeedsPerch = NinjaRound;
        }
        public void PerchLantern(Vector3 position)
        {
            if (!NinjaRound) return;
            var from = lantern.position; bool flee = lanternFlee && lantern.carrier == null;
            lantern.carrier = null; lantern.position = position; lanternIdle = 0; LanternNeedsPerch = false; lanternFlee = false;
            if (flee) LanternEvent("flee", null, null, from);
        }
        public void SettleLantern(Vector3 position) { if (NinjaRound && lantern.carrier == null && !LanternNeedsPerch) lantern.position = position; }
        void LanternEvent(string kind, string actor, string victim, Vector3 at)
        { lantern.eventSequence++; lantern.eventKind = kind; lantern.eventActor = actor; lantern.eventVictim = victim; lantern.eventPosition = at; }
        void DropLantern(string id, string kind)
        {
            if (!CarriesLantern(id)) return;
            var p = Find(id); var at = p != null ? p.frame.position + Vector3.up * 1.1f : lantern.position;
            lantern.carrier = null; lantern.position = at; lanternIdle = 0;
            lanternLockedFor = id; lanternLockUntil = Elapsed + LanternRegrabSeconds;
            LanternEvent(kind, null, id, at);
        }
        void TickLantern(float seconds)
        {
            lantern.active = true;
            if (lantern.gongRemaining > 0) lantern.gongRemaining = Mathf.Max(0, lantern.gongRemaining - seconds);
            else
            {
                lantern.gongNextIn -= seconds;
                if (lantern.gongNextIn <= 0) { lantern.gongRemaining = Mathf.Max(1, Rules.gongSeconds); lantern.gongNextIn = Mathf.Max(5, Rules.gongInterval); LanternEvent("gong", lantern.carrier, null, lantern.position); }
            }
            if (lantern.carrier != null)
            {
                var c = Find(lantern.carrier);
                if (c == null || !c.present || !c.frame.alive || c.frame.dying || c.frame.role != "survivor") { DropLantern(lantern.carrier, "drop"); return; }
                lantern.position = c.frame.position + Vector3.up * 1.1f;
                if (c.frame.health < carrierHealth - .005f) { DropLantern(c.frame.id, "knock"); return; }
                carriedFor += seconds; lantern.holdRemaining = Mathf.Max(0, Rules.lanternHoldSeconds - carriedFor);
                if (Rules.lanternHoldSeconds > 0 && carriedFor >= Rules.lanternHoldSeconds)
                { lanternLockedFor = c.frame.id; lanternLockUntil = Elapsed + 4; lantern.carrier = null; lantern.holdRemaining = 0; LanternNeedsPerch = true; lanternFlee = true; return; }
                carrierHealth = c.frame.health;
                c.frame.lanternPoints += seconds * (lantern.gongRemaining > 0 ? 2 : 1);
                return;
            }
            if (LanternNeedsPerch) return;
            lanternIdle += seconds;
            if (lanternIdle >= Mathf.Max(3, Rules.lanternIdleSeconds)) { LanternNeedsPerch = true; lanternFlee = true; lanternIdle = 0; return; }
            Participant best = null; float reach = LanternReach * LanternReach;
            foreach (var p in players.Values)
            {
                var f = p.frame;
                if (!p.present || p.deferredParticipation || !f.alive || f.dying || f.role != "survivor") continue;
                if (f.id == lanternLockedFor && Elapsed < lanternLockUntil) continue;
                float d = (f.position + Vector3.up * .9f - lantern.position).sqrMagnitude;
                if (d < reach && (LanternPathClear?.Invoke(f.position + Vector3.up * .9f, lantern.position) ?? true))
                { reach = d; best = p; }
            }
            if (best == null) return;
            string victim = lanternLockedFor;
            bool steal = victim != null && victim != best.frame.id && Elapsed < lanternLockUntil + 6;
            lantern.carrier = best.frame.id; carrierHealth = best.frame.health; lanternIdle = 0; lanternLockedFor = null; carriedFor = 0; lantern.holdRemaining = Rules.lanternHoldSeconds;
            if (steal) best.frame.steals++;
            LanternEvent(steal ? "steal" : "grab", best.frame.id, steal ? victim : null, lantern.position);
        }
        void CheckLanternWinner()
        {
            foreach (var p in players.Values)
                if (p.present && !p.deferredParticipation && p.frame.lanternPoints >= Mathf.Max(1, Rules.lanternTarget)) { Finish(p.frame.name, -1); return; }
        }
        void RestoreLantern(LanternFrame source)
        {
            if (source == null) { ResetLantern(); return; }
            lantern.active = source.active; lantern.position = source.position; lantern.carrier = string.IsNullOrEmpty(source.carrier) ? null : source.carrier;
            lantern.gongRemaining = source.gongRemaining; lantern.gongNextIn = source.gongNextIn; lantern.eventSequence = source.eventSequence;
            lantern.eventKind = source.eventKind; lantern.eventActor = source.eventActor; lantern.eventVictim = source.eventVictim; lantern.eventPosition = source.eventPosition;
            lantern.holdRemaining = source.holdRemaining; carriedFor = Mathf.Max(0, Rules.lanternHoldSeconds - source.holdRemaining);
            carrierHealth = Find(lantern.carrier)?.frame.health ?? 1;
            lanternIdle = 0; lanternLockUntil = 0; lanternLockedFor = null; lanternFlee = false;
            LanternNeedsPerch = NinjaRound && !source.active;
        }
    }
}
