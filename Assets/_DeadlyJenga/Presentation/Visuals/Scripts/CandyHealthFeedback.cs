using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Samples health for the shared overlay; missing health stays visible until healed.
    [DisallowMultipleComponent]
    public sealed class CandyHealthFeedback : MonoBehaviour
    {
        public float Damage { get; private set; }
        public float Healing { get; private set; }
        public float Impact { get; private set; }
        public Vector2 Bearing { get; private set; }
        float previous = -1, lastHit = -100, lastHealingSound = -100;
        int pushFrame = -1; Vector3 pushDirection;
        void OnEnable() { GameEvents.Damaged += Hit; GameEvents.Pushed += Push; }
        void OnDisable() { GameEvents.Damaged -= Hit; GameEvents.Pushed -= Push; ResetDisplay(); }
        public void ResetDisplay()
        {
            previous = -1; Damage = Healing = Impact = 0;
            Bearing = Vector2.zero; pushFrame = -1;
        }
        void Push(GameObject who, Vector3 force)
        {
            if (who != gameObject || force.sqrMagnitude < .0004f) return;
            pushDirection = -force.normalized; pushFrame = Time.frameCount;
        }
        void Hit(GameObject who, float amount)
        {
            if (who != gameObject || !float.IsFinite(amount) || amount <= 0 ||
                Services.Session?.MenuOpen == true || MatchRuntime.Current?.Rules?.healthEnabled != true) return;
            var health = GetComponent<IDamageable>();
            if (health == null || health.Health <= 0) return;
            var camera = Services.Session?.ActiveCamera;
            Bearing = pushFrame == Time.frameCount && camera
                ? SplatBearing(camera.transform.InverseTransformDirection(pushDirection)) : Vector2.zero;
            Impact = Mathf.Clamp01(Impact + amount / Mathf.Max(1, health.MaxHealth) * 3);
            lastHit = Time.unscaledTime; pushFrame = -1;
        }
        internal static Vector2 SplatBearing(Vector3 direction)
        {
            var bearing = new Vector2(direction.x, direction.y);
            if (direction.z < 0) bearing.y += direction.z * .38f;
            float extent = Mathf.Max(Mathf.Abs(bearing.x), Mathf.Abs(bearing.y));
            return extent < .1f ? Vector2.up : bearing / extent;
        }
        public void Sample(MatchPlayerFrame player, bool enabled, float dt)
        {
            if (!enabled || player == null || !player.alive || player.dying) { ResetDisplay(); return; }
            float value = Mathf.Clamp01(player.health);
            if (previous < 0) Damage = 1 - value;
            else
            {
                float change = value - previous;
                if (change < -.001f && Time.unscaledTime - lastHit > .45f)
                { Impact = Mathf.Clamp01(Impact - change * 3); Bearing = Vector2.zero; }
                if (change > .001f)
                {
                    Healing = Mathf.Min(1, Healing + .2f + change);
                    if (Time.unscaledTime - lastHealingSound > 1.2f)
                    { CandySoundSuite.Local(CandySound.HealthRegenerating); lastHealingSound = Time.unscaledTime; }
                }
            }
            previous = value;
            Damage = Mathf.MoveTowards(Damage, 1 - value, Mathf.Max(0, dt) * 2);
            Healing = Mathf.MoveTowards(Healing, 0, Mathf.Max(0, dt));
            Impact = Mathf.MoveTowards(Impact, 0, Mathf.Max(0, dt) * 2.5f);
        }
    }
}
