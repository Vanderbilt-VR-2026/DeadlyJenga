using System;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    // Lets the social system (through Core's ISocialBot) give a survivor bot a personality and
    // nudge its walking. Sits between the bot brain and the survivor; never teleports or pushes blocks.
    public sealed class SocialBotDriver : MonoBehaviour, ISocialBot, ICastleSurvivorDriver
    {
        CastleBotBrain brain;
        CastleBotPersonality original, personal;
        Vector3 previousMove;
        bool tuned;

        string generatedNetworkId;
        public string NetworkId
        {
            get
            {
                var actor=GetComponent<IModeActor>();var assigned=actor?.ActorId;
                if(!string.IsNullOrEmpty(assigned)&&assigned.StartsWith("bot:",StringComparison.Ordinal))return assigned;
                return generatedNetworkId??(generatedNetworkId="bot:"+Guid.NewGuid().ToString("N"));
            }
        }
        public bool Respawning => Brain.Respawning;
        public int Respawns => Brain.Respawns;
        public bool Grounded => Brain.Motor.Grounded;
        public float VerticalSpeed => Brain.Motor.VerticalSpeed;
        public int VaultCount => Brain.Motor.VaultCount;
        public float Skill { get; set; } = .6f;
        public float HesitateUntil { get; set; }
        public Func<Vector3?> LookTarget { get; set; }
        CastleBotBrain Brain => brain ? brain : brain = GetComponent<CastleBotBrain>();
        CastleBotEmotes Emotes { get { var e = GetComponent<CastleBotEmotes>(); return e ? e : gameObject.AddComponent<CastleBotEmotes>(); } }

        public void Tune(SocialBotTuning t)
        {
            if (!personal) { original = Brain.Personality; personal = Instantiate(original); personal.name = "Generated social personality"; }
            personal.bravery = t.bravery; personal.goofiness = t.goofiness; personal.sociability = t.sociability;
            personal.exploration = t.exploration; personal.survivalPriority = t.survivalPriority; personal.pace = t.pace;
            personal.decisionInterval = t.decisionInterval; personal.journeyDrive = t.journeyDrive;
            personal.hidePreference = t.hidePreference; personal.restSeconds = t.restSeconds;
            Brain.Personality = personal; Brain.Motor.InputDriver = this; tuned = true;
            _ = Emotes;
        }
        public void SetBravery(float bravery) { if (personal) personal.bravery = Mathf.Clamp01(bravery); }
        public void SeekPoint(Vector3 point, float seconds) { if(Brain.AcceptsSocialMovement)Brain.SetJourneyDestination(point, seconds); }
        public float EmoteFrequency { get => Emotes.frequency; set => Emotes.frequency = value; }
        public void PlayEmote(SocialEmote emote) => Emotes.TryPlay(emote == SocialEmote.Scared ? CastleBotEmotes.Emote.NopeNopeNope : CastleBotEmotes.Emote.FriendlyWave);

        public CastleBotCommand ReadCommand(CastleSurvivor motor, float dt)
        {
            var command = Brain.ReadCommand(motor, dt); if (!tuned || !Brain.AcceptsSocialMovement) return command;
            // Keep ballistic/ledge commands exact. Skill only smooths safe grounded walking.
            if (motor.Grounded && !motor.Vaulting && !command.jump && !motor.Climbing && !Brain.PrecisionTraversal)
            {
                float smooth = 1 - Mathf.Exp(-dt * Mathf.Lerp(7, 18, Skill));
                previousMove = Vector3.Lerp(previousMove, command.move, smooth); command.move = previousMove;
                if (Time.time < HesitateUntil && Brain.Population && Brain.Population.ThreatCost(transform.position) < .2f) command.move *= .2f;
                if (command.move.sqrMagnitude < .1f) { var target = LookTarget?.Invoke(); if (target.HasValue) command.look = target.Value - transform.position; }
            }
            return command;
        }

        public void Release()
        {
            tuned = false; LookTarget = null;
            if (Brain && Brain.Motor && ReferenceEquals(Brain.Motor.InputDriver, this)) Brain.Motor.InputDriver = Brain;
            if (Brain && original) Brain.Personality = original;
            if (personal) Destroy(personal); personal = null; original = null;
        }
        void OnDestroy() => Release();
    }
}
