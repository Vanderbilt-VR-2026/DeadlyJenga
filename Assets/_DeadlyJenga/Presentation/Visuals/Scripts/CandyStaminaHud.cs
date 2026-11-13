using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;

namespace DeadlyJenga.CastlePrototype
{
    // One cosmetic pass for the local body. No camera FOV, physics or gameplay writes.
    [DisallowMultipleComponent, DefaultExecutionOrder(650)]
    public sealed class CandyStaminaHud : MonoBehaviour
    {
        Canvas canvas;
        Material material;
        IMovementState owner;
        CandyHealthFeedback health;
        Vector4 state;
        public float Opacity => canvas && canvas.enabled ? state.x : 0;
        public static float TargetOpacity(bool climbing, float grip, bool hidden) =>
            hidden ? 0 : Mathf.SmoothStep(0, 1, 1 - Mathf.Clamp01(grip));
        void Build()
        {
            var shader = Resources.Load<Shader>("CandyScenery/CandyBodyFeedback");
            if (!shader || !shader.isSupported) return;
            var root = new GameObject("Body feedback - no bars", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.sortingOrder = 60;
            var go = new GameObject("Peripheral feedback", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            var image = go.GetComponent<Image>(); image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            material = new Material(shader) { name = "Local body feedback" };
            material.SetTexture("_Masks", Resources.Load<Texture2D>("CandyScenery/CandyBodyMasks"));
            material.SetTexture("_SandTex", Resources.Load<Texture2D>("CandyScenery/CatwalksSandGrain"));
            image.material = material;
        }
        void LateUpdate()
        {
            if (!(owner is Object alive && alive)) owner = GetComponent<IMovementState>();
            Present(owner, Time.unscaledDeltaTime);
        }
        public void Present(IMovementState motor, float dt)
        {
            var view = motor as IPlayerView; var body = motor as Behaviour;
            var session = Services.Session; var match = MatchRuntime.Current;
            if (view == null || !body || !body.isActiveAndEnabled || !view.ControlledHere ||
                !view.ViewCamera || !view.ViewCamera.isActiveAndEnabled || motor.InputPaused ||
                session?.MenuOpen == true || session?.PlayingHand == true ||
                (session != null && (session.Player?.gameObject != body.gameObject || session.ActiveCamera != view.ViewCamera)) ||
                (session as ILobbyMatchVisitControls)?.VisitingLobby == true ||
                body.GetComponent<IDamageable>()?.Health <= 0)
            { Hide(); return; }
            MatchPlayerFrame player = null;
            if (session?.Player?.gameObject == body.gameObject && match?.Frame != null)
            {
                foreach (var candidate in match.Frame.players)
                    if (candidate.id == match.LocalActorId) { player = candidate; break; }
                if (match.Frame.phase != RoundPhase.Playing || player == null || !player.alive ||
                    player.dying || player.role == "spectator" || !string.IsNullOrEmpty(player.spectating))
                { Hide(); return; }
            }
            if (!health) health = GetComponent<CandyHealthFeedback>() ?? gameObject.AddComponent<CandyHealthFeedback>();
            health.Sample(player, match?.Rules?.healthEnabled == true, dt);
            string action = (motor as IPlayableCharacter)?.StatusText;
            bool sprinting = action == "SPRINTING" || action == "CLIMB SPRINT";
            float sand = player?.sinking == true && !motor.Climbing && !motor.Vaulting
                ? Mathf.Lerp(.08f, 1, Mathf.Clamp01(player.sinkProgress)) : 0;
            var target = new Vector4(TargetOpacity(motor.Climbing, motor.Stamina, false), health.Damage, sprinting ? 1 : 0, sand);
            state.x = Mathf.MoveTowards(state.x, target.x, Mathf.Max(0, dt) * (target.x > state.x ? 2.5f : .9f));
            state.y = health.Damage;
            state.z = Mathf.MoveTowards(state.z, target.z, Mathf.Max(0, dt) * 5);
            state.w = Mathf.MoveTowards(state.w, target.w, Mathf.Max(0, dt) * 3);
            if (state.sqrMagnitude < .00001f && health.Healing <= 0 && health.Impact <= 0)
            { if (canvas) canvas.enabled = false; return; }
            if (!canvas) Build(); if (!canvas) return;
            CandyScreenPlane.Configure(canvas, view.ViewCamera, view.IsVR);
            material.SetVector("_State", state);
            material.SetColor("_Ink", player?.color ?? (body.GetComponent<IModeActor>()?.Color ?? Color.cyan));
            material.SetFloat("_Recovery", health.Healing); material.SetFloat("_Impact", health.Impact);
            material.SetVector("_Bearing", health.Bearing);
            material.SetFloat("_FeedbackTime", Time.unscaledTime % 120);
            canvas.enabled = true;
        }
        void Hide() { if (canvas) canvas.enabled = false; state = Vector4.zero; if (health) health.ResetDisplay(); }
        void OnDisable() => Hide();
        void OnDestroy() { if (canvas) Destroy(canvas.gameObject); if (material) Destroy(material); }
    }
}
