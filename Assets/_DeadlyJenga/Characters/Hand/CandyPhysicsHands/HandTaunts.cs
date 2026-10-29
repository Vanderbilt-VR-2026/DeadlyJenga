using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyPhysicsHands
{
    // Giant-hand taunts. The local player's hand plays the one they pick; AI hands taunt now and then.
    // The taunt rides in the hand's network byte (high 4 bits) so everyone sees it.
    [DisallowMultipleComponent]
    public sealed class HandTaunts : MonoBehaviour
    {
        public const float Length = 2.2f;
        CandyFlyingHandBridge bridge;
        int current;            // taunt index + 1 while showing, else 0
        float until, nextAi;

        public int NetworkBits => Time.time < until ? current : 0;

        void Awake()
        {
            bridge = GetComponent<CandyFlyingHandBridge>();
            nextAi = Time.time + Random.Range(10f, 30f);
        }
        void OnEnable() => Emotes.LocalRequested += OnRequested;
        void OnDisable() => Emotes.LocalRequested -= OnRequested;

        bool IsAi => bridge && bridge.actor && bridge.actor.motor.externallyDriven;

        void OnRequested(int index)
        {
            if (IsAi || !(Services.Session?.PlayingHand ?? false)) return;
            Play(index);
        }

        void Update()
        {
            if (!IsAi || Time.time < nextAi) return;
            nextAi = Time.time + Random.Range(15f, 40f);
            Play(Random.Range(0, Emotes.Hand.Length));
        }

        void Play(int index)
        {
            if (index < 0 || index >= Emotes.Hand.Length || Time.time < until) return;
            var hand = bridge ? bridge.PhysicalHand : null;
            if (!hand || !hand.skin || !hand.skin.enabled) return;
            current = index + 1; until = Time.time + Length;
            GameEvents.RaiseEmoted(hand.gameObject, Emotes.Hand[index]);
        }
    }
}
