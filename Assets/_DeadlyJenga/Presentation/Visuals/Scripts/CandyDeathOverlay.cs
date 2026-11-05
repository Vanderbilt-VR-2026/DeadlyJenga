using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(660)]
    public sealed class CandyDeathOverlay : MonoBehaviour
    {
        const float SpreadSeconds = .28f;
        const float WipeSeconds = .65f;
        static CandyDeathOverlay active;

        ISessionInfo deathSession;
        IMatchRuntime deathMatch;
        Camera deathView;
        string actorId, deathRole, deathSpectating;
        int initialDeaths, confirmedDeaths;
        bool sawDeadFrame, sawAliveAfterDeath, localEventSeen, menuAtDeath;
        float began, wipeAt = -1;
        Color tint;
        Canvas canvas;
        Image paint;
        Material material;

        public static void Show(Color color) => Request(color, false);
        public static void ShowLocalDeath(Color color) => Request(color, true);

        static void Request(Color color, bool localEvent)
        {
            if (active)
            {
                if (!active.IsAnotherDeath(localEvent)) return;
                active.canvas.enabled = false;
                Destroy(active.gameObject);
                active = null;
            }
            var root = new GameObject("Candy death overlay", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(root);
            active = root.AddComponent<CandyDeathOverlay>();
            active.Begin(color);
            active.localEventSeen = localEvent;
        }

        bool IsAnotherDeath(bool localEvent)
        {
            if (!ReferenceEquals(Services.Session, deathSession) || !ReferenceEquals(MatchRuntime.Current, deathMatch)) return true;
            if (localEvent)
            {
                // The character raises this once per life, after setting its eliminated flag.
                bool another = localEventSeen || sawAliveAfterDeath;
                localEventSeen = true;
                return another;
            }
            var player = FindPlayer();
            if (player == null || !player.dying) return false;
            if (localEventSeen && !sawAliveAfterDeath)
            {
                confirmedDeaths = Mathf.Max(confirmedDeaths, player.deaths);
                sawDeadFrame = true;
                return false;
            }
            if (!sawDeadFrame)
            {
                confirmedDeaths = player.deaths;
                sawDeadFrame = true;
                return false;
            }
            return player.deaths > confirmedDeaths || sawAliveAfterDeath && LocalCharacterDead();
        }

        void Begin(Color color)
        {
            began = Time.unscaledTime;
            deathSession = Services.Session;
            deathView = deathSession?.ActiveCamera;
            if (!deathView) deathView = Camera.main;
            menuAtDeath = deathSession?.MenuOpen == true;
            deathMatch = MatchRuntime.Current;
            actorId = deathMatch?.LocalActorId;
            var player = FindPlayer();
            initialDeaths = confirmedDeaths = player?.deaths ?? 0;
            deathRole = player?.role;
            deathSpectating = player?.spectating;
            sawDeadFrame = player != null && (player.dying || !player.alive);

            tint = new Color(color.r, color.g, color.b, 1);
            canvas = GetComponent<Canvas>();
            canvas.sortingOrder = 500;
            var image = new GameObject("Splat", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(transform, false);
            paint = image.GetComponent<Image>();
            paint.raycastTarget = false;
            paint.color = tint;
            var rect = paint.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var shader = Resources.Load<Shader>("CandyScenery/CandyDeathPaint");
            if (shader && shader.isSupported)
            {
                material = new Material(shader);
                paint.material = material;
            }
        }

        MatchPlayerFrame FindPlayer()
        {
            var frame = deathMatch?.Frame;
            if (frame == null || string.IsNullOrEmpty(actorId)) return null;
            foreach (var player in frame.players)
                if (player.id == actorId) return player;
            return null;
        }

        bool LocalCharacterDead()
        {
            var player = deathSession?.Player;
            return NetworkActors.Alive(player) && player.gameObject.GetComponent<IDamageable>()?.Health <= 0;
        }

        bool LocalCharacterAlive()
        {
            var player = deathSession?.Player;
            return NetworkActors.Alive(player) && player.gameObject.GetComponent<IDamageable>()?.Health > 0;
        }

        bool IsNextView(ISessionInfo session, Camera camera)
        {
            if (!ReferenceEquals(session, deathSession) || !ReferenceEquals(MatchRuntime.Current, deathMatch) ||
                !ReferenceEquals(deathView, camera)) return true;
            if (session?.MenuOpen != true) menuAtDeath = false;
            else if (!menuAtDeath) return true;

            var frame = deathMatch?.Frame;
            if (frame?.phase == RoundPhase.Results || frame?.phase == RoundPhase.Victory) return true;

            var player = FindPlayer();
            if (player == null) return false;
            bool dead = player.dying || !player.alive;
            if (!sawDeadFrame && (dead || player.deaths > initialDeaths))
            {
                confirmedDeaths = player.deaths;
                sawDeadFrame = true;
            }
            bool revived = !dead && LocalCharacterAlive();
            if (sawDeadFrame && revived) sawAliveAfterDeath = true;
            // A local elimination can arrive before the host has published its dying frame.
            return (revived && (sawDeadFrame || player.deaths > initialDeaths))
                || (player.role == "spectator" && deathRole != "spectator")
                || (!string.IsNullOrEmpty(player.spectating) && player.spectating != deathSpectating);
        }

        void LateUpdate()
        {
            var session = Services.Session;
            var camera = session?.ActiveCamera;
            if (!camera) camera = Camera.main;
            if (!camera)
            {
                canvas.enabled = false;
                return;
            }

            float now = Time.unscaledTime;
            float age = now - began;
            bool nextView = IsNextView(session, camera);
            if (wipeAt < 0 && age >= SpreadSeconds && nextView) wipeAt = now;
            float wipe = wipeAt < 0 ? 0 : Mathf.Clamp01((now - wipeAt) / WipeSeconds);
            if (wipe >= 1)
            {
                canvas.enabled = false;
                Destroy(gameObject);
                return;
            }

            bool vr = session?.IsVR ?? camera.stereoEnabled;
            var renderMode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            if (canvas.renderMode != renderMode) canvas.renderMode = renderMode;
            if (canvas.worldCamera != (vr ? camera : null)) canvas.worldCamera = vr ? camera : null;
            float aspect = Mathf.Max(.1f, camera.aspect);
            if (vr) aspect = FitStereoPlane((RectTransform)transform, camera);

            if (material)
            {
                material.SetFloat("_Age", Mathf.Min(age, 4));
                material.SetFloat("_Wipe", wipe);
                material.SetFloat("_Aspect", aspect);
            }
            else paint.color = new Color(tint.r, tint.g, tint.b, 1 - wipe);
            canvas.enabled = true;
        }

        static float FitStereoPlane(RectTransform plane, Camera camera)
        {
            float depth = camera.nearClipPlane + Mathf.Min(.12f, (camera.farClipPlane - camera.nearClipPlane) * .5f);
            Vector2 extent = Vector2.zero;
            bool stereo = camera.stereoEnabled;
            for (int eye = 0; eye < (stereo ? 2 : 1); eye++)
            {
                var projection = !stereo ? Camera.MonoOrStereoscopicEye.Mono :
                    eye == 0 ? Camera.MonoOrStereoscopicEye.Left : Camera.MonoOrStereoscopicEye.Right;
                for (int corner = 0; corner < 4; corner++)
                {
                    var point = camera.ViewportToWorldPoint(new Vector3(corner & 1, corner >> 1, depth), projection);
                    var local = Quaternion.Inverse(camera.transform.rotation) * (point - camera.transform.position);
                    extent = Vector2.Max(extent, new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y)));
                }
            }

            plane.SetPositionAndRotation(camera.transform.position + camera.transform.forward * depth, camera.transform.rotation);
            plane.sizeDelta = extent * 2.2f + Vector2.one * .03f;
            return plane.sizeDelta.x / plane.sizeDelta.y;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
            if (material) Destroy(material);
        }
    }
}
