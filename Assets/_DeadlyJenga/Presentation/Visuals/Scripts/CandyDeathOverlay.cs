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
        int environment;
        Canvas canvas;
        Image paint;
        Material material;

        public static void Show(Color color) => Request(color, false);
        public static void ShowLocalDeath(Color color) => Request(color, true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.MatchSound += EnvironmentDeath;

        static void EnvironmentDeath(MatchSoundCue cue, Vector3 point)
        {
            int kind = cue == MatchSoundCue.IslandDeath ? 1 : cue == MatchSoundCue.DrowningDeath ? 2 : 0;
            var session = Services.Session;
            if (kind == 0 || session?.Player == null || session.PlayingHand ||
                (session as ILobbyMatchVisitControls)?.VisitingLobby == true ||
                MatchRuntime.Current?.Frame?.phase != RoundPhase.Playing) return;
            // These cues are addressed to the victim; bystanders receive NearbyDeath instead.
            var actor = session.Player.gameObject.GetComponent<IModeActor>();
            Request(actor?.Color ?? Color.cyan, false, kind);
        }

        static void Request(Color color, bool localEvent, int kind = 0)
        {
            if (active)
            {
                if (!active.IsAnotherDeath(localEvent))
                {
                    if (kind > 0) active.SetEnvironment(kind);
                    return;
                }
                active.canvas.enabled = false;
                Destroy(active.gameObject);
                active = null;
            }
            var root = new GameObject("Candy death overlay", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(root);
            active = root.AddComponent<CandyDeathOverlay>();
            active.Begin(color);
            if (kind > 0) active.SetEnvironment(kind);
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
            int kind = 0;
            if (player?.sinking == true && player.sinkProgress >= .98f) kind = 1;
            var world = Services.World;
            if (deathView && world != null && deathView.transform.position.y <= world.WaterHeight(deathView.transform.position) + .25f) kind = 2;
            SetEnvironment(kind);
        }

        void SetEnvironment(int kind)
        {
            if (material && environment == kind) return;
            environment = kind;
            if (material) Destroy(material);
            var shader = Resources.Load<Shader>(kind == 0 ? "CandyScenery/CandyDeathPaint" : "CandyScenery/CandyBodyFeedback");
            if (!shader || !shader.isSupported) return;
            material = new Material(shader);
            if (kind > 0)
            {
                material.SetTexture("_SandTex", Resources.Load<Texture2D>("CandyScenery/CatwalksSandGrain"));
                material.SetFloat("_DeathKind", kind);
                material.SetVector("_State", new Vector4(0, 0, 0, kind == 1 ? 1 : 0));
            }
            paint.material = material;
            paint.color = kind == 0 ? tint : Color.white;
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
            if (vr) aspect = CandyScreenPlane.Fit((RectTransform)transform, camera);

            if (material)
            {
                material.SetFloat("_Age", Mathf.Min(age, 4));
                material.SetFloat("_Wipe", wipe);
                if (environment == 0) material.SetFloat("_Aspect", aspect);
                else material.SetFloat("_FeedbackTime", Time.unscaledTime % 120);
            }
            else paint.color = new Color(tint.r, tint.g, tint.b, 1 - wipe);
            canvas.enabled = true;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
            if (material) Destroy(material);
        }
    }
}
