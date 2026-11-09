using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyHealthFeedback : MonoBehaviour
    {
        const int SplatCount = 5;
        const float SnapshotWait = .16f;
        const float LiveHitWindow = .45f;
        readonly Splat[] splats = new Splat[SplatCount];
        readonly Image[] splatImages = new Image[SplatCount];
        int nextSplat;

        struct Splat
        {
            public float born, strength;
            public Vector2 bearing;
            public bool active;
        }

        Canvas canvas;
        Image healingRim, bar;
        Text label;
        Texture2D texture;
        Sprite sprite;
        readonly Material[] paints = new Material[SplatCount];
        Camera eye;
        IMatchRuntime owner;
        string actor;
        float previous = -1, healing, lastHealthRise = -100;
        float lastLiveHit = -100, snapshotAt, snapshotDamage;
        bool vr;
        int pushFrame = -1;
        Vector3 pushDirection;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += go =>
        {
            if (!go.GetComponent<CandyHealthFeedback>()) go.AddComponent<CandyHealthFeedback>();
        };

        void OnEnable()
        {
            GameEvents.Damaged += Hit;
            GameEvents.Pushed += Push;
        }

        void OnDisable()
        {
            GameEvents.Damaged -= Hit;
            GameEvents.Pushed -= Push;
            ResetDisplay();
        }

        void ResetDisplay()
        {
            if (canvas) canvas.enabled = false;
            owner = null;
            actor = null;
            previous = -1;
            healing = snapshotDamage = 0;
            lastHealthRise = lastLiveHit = -100;
            pushFrame = -1;
            nextSplat = 0;
            System.Array.Clear(splats, 0, splats.Length);
        }

        void BindMatch(IMatchRuntime match)
        {
            if (ReferenceEquals(owner, match) && actor == match.LocalActorId) return;
            ResetDisplay();
            owner = match;
            actor = match.LocalActorId;
        }

        static bool LocalSurvivor(GameObject who, ISessionInfo session) =>
            who && session?.Player?.gameObject == who && !session.MenuOpen && !session.PlayingHand;

        void Push(GameObject who, Vector3 force)
        {
            var match = MatchRuntime.Current;
            if (match?.Frame?.phase != RoundPhase.Playing || !match.Rules.healthEnabled ||
                !LocalSurvivor(who, Services.Session) || force.sqrMagnitude <= .0004f) return;
            BindMatch(match);
            // Combat applies the impulse immediately before damage, including peer impacts.
            pushDirection = -force.normalized;
            pushFrame = Time.frameCount;
        }

        void Hit(GameObject who, float amount)
        {
            var match = MatchRuntime.Current;
            var session = Services.Session;
            if (match?.Frame?.phase != RoundPhase.Playing || !match.Rules.healthEnabled ||
                !LocalSurvivor(who, session) || !float.IsFinite(amount) || amount <= 0) return;
            BindMatch(match);
            var health = who.GetComponent<IDamageable>();
            lastLiveHit = Time.unscaledTime;
            snapshotDamage = 0;
            var view = session.ActiveCamera;
            var bearing = pushFrame == Time.frameCount && view
                ? SplatBearing(view.transform.InverseTransformDirection(pushDirection)) : Vector2.zero;
            pushFrame = -1;
            if (health != null && health.Health <= 0) return;
            AddSplat(amount / Mathf.Max(1, health?.MaxHealth ?? 100), bearing);
        }

        internal static Vector2 SplatBearing(Vector3 viewDirection)
        {
            var bearing = new Vector2(viewDirection.x, viewDirection.y);
            if (viewDirection.z < 0) bearing.y += viewDirection.z * .38f;
            float extent = Mathf.Max(Mathf.Abs(bearing.x), Mathf.Abs(bearing.y));
            return extent < .10f ? Vector2.up : bearing / extent;
        }

        internal static Vector3 SplatProfile(float fraction)
        {
            float strength = Mathf.Clamp01(fraction / .60f);
            return new Vector3(Mathf.Lerp(.085f, .235f, strength),
                Mathf.Lerp(.20f, .80f, strength), Mathf.Lerp(.40f, .80f, strength));
        }

        void AddSplat(float fraction, Vector2 bearing)
        {
            float now = Time.unscaledTime;
            ref var recent = ref splats[(nextSplat + SplatCount - 1) % SplatCount];
            healing = 0;
            lastHealthRise = -100;
            if (recent.active && now - recent.born < .10f &&
                (bearing == recent.bearing || Vector2.Dot(bearing.normalized, recent.bearing.normalized) > .94f))
            {
                recent.strength += fraction;
                return;
            }
            splats[nextSplat] = new Splat { active = true, born = now, strength = fraction, bearing = bearing };
            nextSplat = (nextSplat + 1) % SplatCount;
        }

        void Build()
        {
            var root = new GameObject("Local health response", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.sortingOrder = 65;

            texture = new Texture2D(256, 160, TextureFormat.RGBA32, false)
                { name = "Soft peripheral recovery", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 160; y++) for (int x = 0; x < 256; x++)
            {
                float nx = (x - 127.5f) / 128, ny = (y - 79.5f) / 80;
                float r = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                float streak = .65f + .35f * Mathf.Pow(Mathf.Sin(Mathf.Atan2(ny, nx) * 17 + nx * 4), 8);
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01((r - .68f) / .32f), 2) * streak * .65f));
            }
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 256, 160), Vector2.one * .5f);
            healingRim = MakeImage("Recovery glow", root.transform, CandyHudLayout.ReferenceSize, Vector2.zero);
            healingRim.sprite = sprite;

            var shader = Resources.Load<Shader>("CandyScenery/CandyDeathPaint");
            if (shader && shader.isSupported)
            {
                for (int i = 0; i < SplatCount; i++)
                {
                    var image = MakeImage("Damage splatter", root.transform, Vector2.zero, Vector2.zero);
                    var paint = new Material(shader) { name = "Directional candy damage paint" };
                    paint.SetFloat("_Spatter", 1);
                    image.material = paints[i] = paint;
                    splatImages[i] = image;
                }
            }

            var well = MakeImage("Health well", root.transform, new Vector2(256, 16), new Vector2(0, -270));
            well.color = new Color(.03f, .02f, .06f, .6f);
            bar = MakeImage("Health", well.transform, new Vector2(252, 12), Vector2.zero);
            bar.rectTransform.pivot = new Vector2(0, .5f);
            bar.rectTransform.anchoredPosition = new Vector2(-126, 0);
            var go = new GameObject("Health change", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(root.transform, false);
            label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 21;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(300, 36);
            label.rectTransform.anchoredPosition = new Vector2(0, -240);
        }

        static Image MakeImage(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = pos;
            return image;
        }

        Vector2 ViewSize()
        {
            var rect = (RectTransform)canvas.transform;
            if (!vr) return rect.rect.size;
            float depth = rect.localPosition.z;
            var min = rect.InverseTransformPoint(eye.ViewportToWorldPoint(new Vector3(0, 0, depth)));
            var max = rect.InverseTransformPoint(eye.ViewportToWorldPoint(new Vector3(1, 1, depth)));
            return new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }

        bool DrawSplats(Color color, float now)
        {
            Vector2 viewport = Vector2.zero;
            float shortSide = 0;
            bool active = false;
            for (int i = 0; i < SplatCount; i++)
            {
                ref var splat = ref splats[i];
                var image = splatImages[i];
                Vector3 profile = SplatProfile(splat.strength);
                float age = now - splat.born;
                if (splat.active && age >= profile.z) splat.active = false;
                active |= splat.active;
                if (!image) continue;
                image.enabled = splat.active;
                if (!splat.active) continue;

                if (shortSide <= 0)
                {
                    viewport = ViewSize();
                    shortSide = Mathf.Min(viewport.x, viewport.y);
                }
                bool neutral = splat.bearing == Vector2.zero;
                var bearing = neutral ? new Vector2(i % 2 == 0 ? -.52f : .52f, -.85f) : splat.bearing;
                image.rectTransform.anchoredPosition = Vector2.Scale(bearing, viewport) * (vr ? .36f : .385f);
                image.rectTransform.sizeDelta = Vector2.one * (shortSide * profile.x * (neutral ? .77f : 1));
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, i * 13 - 26);
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, profile.z, age));
                image.color = new Color(color.r, color.g, color.b, profile.y * fade * (neutral ? .74f : 1));
                paints[i].SetFloat("_Age", age);
            }
            return active;
        }

        void Update()
        {
            var match = MatchRuntime.Current;
            var session = Services.Session;
            if (match?.Frame?.phase != RoundPhase.Playing || session == null || !session.ActiveCamera ||
                !session.ActiveCamera.isActiveAndEnabled || session.MenuOpen || session.PlayingHand || !match.Rules.healthEnabled)
            {
                ResetDisplay();
                return;
            }
            BindMatch(match);
            var player = System.Array.Find(match.Frame.players, value => value.id == actor);
            if (player == null || !player.alive || player.dying)
            {
                ResetDisplay();
                return;
            }

            float now = Time.unscaledTime;
            if (previous >= 0)
            {
                float change = player.health - previous;
                if (change < -.001f && now - lastLiveHit >= LiveHitWindow)
                {
                    if (snapshotDamage <= 0) snapshotAt = now + SnapshotWait;
                    snapshotDamage -= change;
                }
                if (change > .001f)
                {
                    snapshotDamage = 0;
                    healing = Mathf.Max(.65f, Mathf.Min(1, healing + .25f + change));
                    if (now - lastHealthRise > 1.2f) CandySoundSuite.Local(CandySound.HealthRegenerating);
                    lastHealthRise = now;
                }
            }
            if (snapshotDamage > 0 && now >= snapshotAt)
            {
                AddSplat(snapshotDamage, Vector2.zero);
                snapshotDamage = 0;
            }
            previous = player.health;
            healing = Mathf.MoveTowards(healing, 0, Time.unscaledDeltaTime);
            if (!canvas) Build();
            if (eye != session.ActiveCamera || vr != session.IsVR)
            {
                eye = session.ActiveCamera;
                vr = session.IsVR;
                CandyHudLayout.Configure(canvas, transform, eye, vr);
                healingRim.rectTransform.sizeDelta = vr ? new Vector2(2800, 2000) : CandyHudLayout.ReferenceSize;
            }
            bool hit = DrawSplats(player.color, now);
            canvas.enabled = hit || healing > 0;
            healingRim.color = new Color(.18f, 1, .7f, healing * .45f);
            bar.rectTransform.sizeDelta = new Vector2(252 * Mathf.Clamp01(player.health), 12);
            bar.color = hit ? new Color(1, .15f, .18f) : new Color(.2f, 1, .7f);
            label.color = bar.color;
            label.text = (hit ? "HIT · " : "RECOVERING · ") + Mathf.CeilToInt(player.health * 100) + " HP";
        }

        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
            if (sprite) Destroy(sprite);
            if (texture) Destroy(texture);
            foreach (var paint in paints) if (paint) Destroy(paint);
        }
    }
}
