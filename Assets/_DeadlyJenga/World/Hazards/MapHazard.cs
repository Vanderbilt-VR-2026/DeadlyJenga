using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    public abstract class MapHazard : MonoBehaviour, IMapHazard
    {
        [SerializeField] HazardType type;
        [SerializeField, Min(.01f)] float intensity = 1;
        [SerializeField, Min(.01f)] float duration = 10;
        public HazardType Type => type;
        public bool Active { get; private set; }
        public float Intensity => intensity;
        public float Duration => duration;

        public bool TryValidate(IWorldQuery world, out string error)
        {
            if (!TryValidatePrefab(out error)) return false;
            if (world == null) { error = "No active map is registered in Services.World."; return false; }
            if (!ValidBounds(world.PlayableBounds)) { error = "The map needs finite, positive PlayableBounds on all three axes."; return false; }
            return true;
        }

        public bool TryValidatePrefab(out string error)
        {
            error = null;
            if (!type) error = "Assign a HazardType.";
            else if (!Positive(intensity) || !Positive(duration)) error = "Intensity and duration must be finite and positive.";
            else if ((transform.lossyScale - Vector3.one).sqrMagnitude > .000001f) error = "Keep the disaster root at world scale (1, 1, 1).";
            return error == null && ValidateSettings(out error);
        }

        protected virtual bool ValidateSettings(out string error) { error = null; return true; }

        public static bool ValidBounds(Bounds bounds) => Finite(bounds.center.x) && Finite(bounds.center.y) &&
            Finite(bounds.center.z) && Positive(bounds.size.x) && Positive(bounds.size.y) && Positive(bounds.size.z) &&
            Finite(bounds.min.x) && Finite(bounds.min.y) && Finite(bounds.min.z) &&
            Finite(bounds.max.x) && Finite(bounds.max.y) && Finite(bounds.max.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Positive(float value) => Finite(value) && value > 0;

        [ContextMenu("Trigger disaster")]
        public void Begin()
        {
            if (Active) return;
            if (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority) return;
            var world = Services.World;
            if (!TryValidate(world, out var error)) { Debug.LogError($"{name}: {error}", this); return; }
            var bounds = world.PlayableBounds;
            transform.position = bounds.center;
            Active = true;
            GameEvents.RaiseHazardStarted(type, transform.position);
            try { BeginHazard(world, bounds, intensity, duration); }
            catch { End(); throw; }
        }

        protected abstract void BeginHazard(IWorldQuery world, Bounds bounds, float strength, float seconds);
        protected virtual void EndHazard() { }

        public void End()
        {
            if (!Active) return;
            Active = false;
            try { EndHazard(); }
            finally { GameEvents.RaiseHazardEnded(type, transform.position); }
        }

        protected virtual void OnDisable() => End();
    }
}
