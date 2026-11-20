using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    [DisallowMultipleComponent, DefaultExecutionOrder(650)]
    public sealed class PowerupHud : MonoBehaviour
    {
        static readonly int Boosts = Shader.PropertyToID("_Boosts");
        static readonly int Phase = Shader.PropertyToID("_Phase");

        IPlayerView view;
        ITimedEffects effects;
        IDamageable health;
        EffectType speedType, jumpType;
        Canvas canvas;
        Material material;
        Camera attached;
        Vector2 strength;
        bool triedBuild;

        public float ShownSpeedSeconds { get; private set; }
        public float ShownJumpSeconds { get; private set; }

        void Awake()
        {
            view = GetComponent<IPlayerView>();
            effects = GetComponent<ITimedEffects>();
            health = GetComponent<IDamageable>();
            speedType = Resources.Load<EffectType>("Powerups/Speed2x");
            jumpType = Resources.Load<EffectType>("Powerups/Jump3x");
        }

        void LateUpdate() => Present(Time.unscaledDeltaTime, (float)(Time.unscaledTimeAsDouble % 120));

        void Present(float dt, float time)
        {
            var session = Services.Session;
            var camera = view?.ViewCamera;
            if (effects == null || view?.ControlledHere != true || !camera || !camera.isActiveAndEnabled ||
                (health != null && health.Health <= 0) || !OwnsView(session, camera))
            {
                Hide();
                return;
            }

            if (attached != camera)
            {
                strength = Vector2.zero;
                attached = camera;
            }

            ShownSpeedSeconds = effects.SecondsRemaining(speedType);
            ShownJumpSeconds = effects.SecondsRemaining(jumpType);
            strength.x = Ease(strength.x, ShownSpeedSeconds > 0, dt);
            strength.y = Ease(strength.y, ShownJumpSeconds > 0, dt);
            if (strength == Vector2.zero)
            {
                if (canvas) canvas.enabled = false;
                return;
            }

            if (!triedBuild) Build();
            if (!canvas) return;
            CandyScreenPlane.Configure(canvas, camera, view.IsVR || camera.stereoEnabled);
            material.SetVector(Boosts, new Vector4(strength.x, strength.y, 0, 0));
            material.SetFloat(Phase, time);
            canvas.enabled = true;
        }

        bool OwnsView(ISessionInfo session, Camera camera)
        {
            if (session == null) return true;
            if (session.MenuOpen || session.PlayingHand || session.Player?.gameObject != gameObject ||
                session.Player.InputSuspended || session.ActiveCamera != camera ||
                (session as ILobbyMatchVisitControls)?.VisitingLobby == true ||
                (session.Demo as ISpectatorControls)?.Spectating == true)
                return false;

            var match = MatchRuntime.Current;
            var frame = match?.Frame;
            if (frame == null) return true;
            if (frame.phase != RoundPhase.Playing) return false;
            foreach (var player in frame.players)
                if (player.id == match.LocalActorId)
                    return player.alive && !player.dying && player.role != "spectator" &&
                        string.IsNullOrEmpty(player.spectating);
            return false;
        }

        static float Ease(float value, bool active, float dt) =>
            Mathf.MoveTowards(value, active ? 1 : 0, dt * (active ? 6 : 3.5f));

        void Build()
        {
            triedBuild = true;
            var shader = Resources.Load<Shader>("CandyScenery/CandyPowerupScreenFx");
            if (!shader || !shader.isSupported) return;

            var root = new GameObject("Powerup screen effects", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.sortingOrder = 32;
            var image = new GameObject("Powerup light", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(root.transform, false);
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            material = new Material(shader) { name = "Local powerup screen effects" };
            image.material = material;
        }

        void Hide()
        {
            if (canvas) canvas.enabled = false;
            strength = Vector2.zero;
            attached = null;
            ShownSpeedSeconds = ShownJumpSeconds = 0;
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
            if (material) Destroy(material);
        }
    }
}
