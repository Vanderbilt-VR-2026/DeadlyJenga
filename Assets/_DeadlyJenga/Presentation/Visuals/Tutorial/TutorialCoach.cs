using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.CastlePrototype
{
    // The guided first match. A floating coach card (same style as the game's HUD cards) walks a new player
    // through looking, walking, jumping, climbing, vaulting, emoting and the dangers of the tower.
    // Each step completes when the player actually does it: checkmark pop, confetti, chime, next card.
    // Runs once (saved), skippable (hold Backspace, or hold B on the right controller). Reads Core only.
    public sealed class TutorialCoach : MonoBehaviour
    {
        const string DoneKey = "DeadlyJenga.TutorialDone";
        public static TutorialCoach Instance { get; private set; }

        sealed class Step
        {
            public string title, body, desktopKey, vrKey;
            public CandyHudIcon icon;
            public float auto;                // seconds; 0 = waits for the player
            public Func<bool> done;
            public Action begin;
        }

        Step[] steps;
        int index = -1;
        float stepStart, celebrateUntil, nextCheck, skipHeld;
        bool celebrating;

        // Card
        Canvas card; CanvasGroup group;
        Image iconDisc, iconImage, checkImage, chip;
        Image[] dots;
        Text titleText, bodyText, chipText, counterText, skipText;
        // World helpers
        Transform ring; Material ringMaterial;
        ParticleSystem confetti;
        // Tracking
        Vector3 walkStart, ringTarget;
        bool hasRing;
        float lookYaw, lookTurned;
        int vaultsAtStart;
        bool emoted;
        Vector3 anchor; bool placed;
        readonly InputAction vrSkip = new InputAction("Tutorial skip", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");

        public bool Running => index >= 0;

        // Editor preview: show step `step` (optionally in its completed state) at `position`, facing `viewer`.
        public void Preview(int step, bool completed, Vector3 position, Vector3 viewer, bool vr)
        {
            if (steps == null) { BuildSteps(); BuildCard(); BuildWorldHelpers(); }
            index = Mathf.Clamp(step, 0, steps.Length - 1);
            var s = steps[index];
            titleText.text = s.title; titleText.color = Color.white; bodyText.text = s.body;
            string key = vr ? s.vrKey : s.desktopKey;
            chip.gameObject.SetActive(!string.IsNullOrEmpty(key)); chipText.text = key ?? ""; Layout(chip.gameObject.activeSelf);
            iconImage.sprite = Sprite.Create(CandyHudVisuals.Icon(s.icon), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
            iconImage.gameObject.SetActive(!completed); checkImage.gameObject.SetActive(completed);
            iconDisc.color = completed ? CandyHudVisuals.Mint : CandyHudVisuals.Lavender;
            if (completed) { titleText.text = "NICE!"; titleText.color = CandyHudVisuals.Mint; }
            counterText.text = "STEP " + (index + 1) + " OF " + steps.Length;
            for (int d = 0; d < dots.Length; d++) dots[d].color = d < index || (completed && d == index) ? CandyHudVisuals.Mint : d == index ? Color.white : CandyHudVisuals.Card;
            skipText.text = vr ? "hold B to skip tutorial" : "hold BACKSPACE to skip tutorial";
            card.gameObject.SetActive(true); group.alpha = 1;
            card.transform.position = position; card.transform.rotation = Quaternion.LookRotation(position - viewer, Vector3.up);
            card.transform.localScale = Vector3.one * .0016f;
        }

        // Text column: title on top, instructions fill the space down to the key chip (or the bottom when there is no chip).
        void Layout(bool hasChip)
        {
            var r = bodyText.rectTransform;
            float top = 40, bottom = hasChip ? -66 : -134;
            r.sizeDelta = new Vector2(560, top - bottom); r.anchoredPosition = new Vector2(115, (top + bottom) * .5f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("Tutorial coach");
            DontDestroyOnLoad(go);
            go.AddComponent<TutorialCoach>();
        }

        // Plays the tutorial again from the start (menu or dev key).
        public static void Restart()
        {
            PlayerPrefs.DeleteKey(DoneKey);
            if (Instance) { Instance.Stop(false); Instance.nextCheck = 0; }
        }

        void Awake()
        {
            Instance = this;
            BuildSteps();
            BuildCard();
            BuildWorldHelpers();
            card.gameObject.SetActive(false);
        }
        void OnEnable() { vrSkip.Enable(); GameEvents.Emoted += OnEmoted; }
        void OnDisable() { vrSkip.Disable(); GameEvents.Emoted -= OnEmoted; }
        void OnDestroy() => vrSkip.Dispose();

        void OnEmoted(GameObject who, string emote)
        {
            var player = Services.Session?.Player;
            if (player != null && who && (who == player.gameObject || who.transform.IsChildOf(player.transform))) emoted = true;
        }

        static IMovementState Movement => Services.Session?.Player as IMovementState;
        static bool VR => Services.Session != null && Services.Session.IsVR;

        void BuildSteps()
        {
            steps = new[]
            {
                new Step { title = "WELCOME TO DEADLY JENGA", body = "Survive on top of the tower as long as you can. Let's learn the moves!", icon = CandyHudIcon.Play, auto = 5 },
                new Step { title = "LOOK AROUND", body = "Take a look around the tower.", desktopKey = "MOUSE", vrKey = "TURN YOUR HEAD", icon = CandyHudIcon.Look,
                    begin = () => { lookYaw = CameraYaw(); lookTurned = 0; }, done = () => lookTurned > 120 },
                new Step { title = "WALK", body = "Walk over to the glowing ring.", desktopKey = "W A S D", vrKey = "LEFT STICK", icon = CandyHudIcon.Move,
                    begin = PlaceRing, done = WalkedToRing },
                new Step { title = "JUMP", body = "Give it a hop!", desktopKey = "SPACE", vrKey = "A  ·  RIGHT HAND", icon = CandyHudIcon.Jump,
                    done = () => Movement != null && !Movement.Grounded && Movement.VerticalSpeed > 2 },
                new Step { title = "CLIMB", body = "Walk up to a wall and hold jump to grab it and scramble up.", desktopKey = "HOLD SPACE", vrKey = "HOLD A", icon = CandyHudIcon.Climb,
                    done = () => Movement != null && Movement.Climbing },
                new Step { title = "VAULT", body = "Jump at a ledge, then press jump again to pull yourself up onto it.", desktopKey = "SPACE  ·  THEN SPACE", vrKey = "A  ·  THEN A", icon = CandyHudIcon.Jump,
                    begin = () => vaultsAtStart = Movement?.VaultCount ?? 0, done = () => Movement != null && Movement.VaultCount > vaultsAtStart },
                new Step { title = "SAY HI", body = "Emotes let you wave, clap, or taunt the other players.", desktopKey = "PRESS 1", vrKey = "HOLD Y  ·  LOOK  ·  LET GO", icon = CandyHudIcon.Friends,
                    begin = () => emoted = false, done = () => emoted },
                new Step { title = "WATCH THE BLOCKS", body = "Blocks crack under weight and sift dust right before they snap. Cracked means move!", icon = CandyHudIcon.Help, auto = 7 },
                new Step { title = "DON'T FALL!", body = "Fall off and you come back as a GIANT HAND. Pull blocks and knock the survivors off!", icon = CandyHudIcon.Hand, auto = 7 },
                new Step { title = "YOU'RE READY!", body = "Last one standing wins. Good luck up there!", icon = CandyHudIcon.Play, auto = 5 },
            };
        }

        // ---------- Flow ----------

        void Update()
        {
            var session = Services.Session;
            if (index < 0)
            {
                if (Time.unscaledTime < nextCheck) return;
                nextCheck = Time.unscaledTime + .5f;
                if (PlayerPrefs.GetInt(DoneKey, 0) == 1 || session == null || session.Player == null || session.PlayingHand || session.MenuOpen) return;
                var world = Services.World;
                if (world == null || world.Blocks == null || world.Blocks.Count == 0 || Movement == null || !Movement.IsLocal) return;
                Begin(0);
                return;
            }
            if (session == null || session.Player == null) { Stop(false); return; }
            if (session.PlayingHand && index < 8) { Stop(true); return; }   // already learned the hard way

            bool hidden = session.MenuOpen;
            if (card.gameObject.activeSelf == hidden) card.gameObject.SetActive(!hidden);
            if (hidden) return;

            // Hold to skip.
            var k = Keyboard.current;
            bool skipping = (k != null && k.backspaceKey.isPressed) || vrSkip.IsPressed();
            skipHeld = skipping ? skipHeld + Time.unscaledDeltaTime : 0;
            skipText.text = skipHeld > 0 ? "SKIPPING TUTORIAL..." : VR ? "hold B to skip tutorial" : "hold BACKSPACE to skip tutorial";
            if (skipHeld > 1.2f) { Stop(true); return; }

            TrackLook();
            var step = steps[index];
            if (celebrating)
            {
                if (Time.unscaledTime >= celebrateUntil) { celebrating = false; if (index + 1 < steps.Length) Begin(index + 1); else Stop(true); }
            }
            else
            {
                float age = Time.unscaledTime - stepStart;
                bool finished = step.auto > 0 ? age >= step.auto : (step.done != null && step.done()) || age > 40;
                if (finished) Complete();
            }
            Animate();
        }

        void Begin(int i)
        {
            index = i; stepStart = Time.unscaledTime;
            var step = steps[i];
            step.begin?.Invoke();
            card.gameObject.SetActive(true);
            titleText.text = step.title; titleText.color = Color.white;
            bodyText.text = step.body;
            string key = VR ? step.vrKey : step.desktopKey;
            chip.gameObject.SetActive(!string.IsNullOrEmpty(key));
            chipText.text = key ?? "";
            iconImage.sprite = Sprite.Create(CandyHudVisuals.Icon(step.icon), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
            iconImage.gameObject.SetActive(true); checkImage.gameObject.SetActive(false);
            iconDisc.color = CandyHudVisuals.Lavender;
            counterText.text = "STEP " + (i + 1) + " OF " + steps.Length;
            for (int d = 0; d < dots.Length; d++) dots[d].color = d < i ? CandyHudVisuals.Mint : d == i ? Color.white : CandyHudVisuals.Card;
            ring.gameObject.SetActive(i == 2 && hasRing);
            Layout(chip.gameObject.activeSelf);
            Chime(1f);
        }

        Action PlaceRingAction => steps != null && steps.Length > 2 ? steps[2].begin : null;

        void Complete()
        {
            celebrating = true; celebrateUntil = Time.unscaledTime + (index == steps.Length - 1 ? 2.2f : 1.1f);
            var step = steps[index];
            if (step.auto <= 0 || index == steps.Length - 1)
            {
                titleText.text = index == steps.Length - 1 ? "LET'S GO!" : "NICE!";
                titleText.color = CandyHudVisuals.Mint;
                iconImage.gameObject.SetActive(false); checkImage.gameObject.SetActive(true);
                iconDisc.color = CandyHudVisuals.Mint;
                Confetti(index == steps.Length - 1 ? 160 : 70);
                Chime(1.45f);
            }
            dots[index].color = CandyHudVisuals.Mint;
            ring.gameObject.SetActive(false);
        }

        void Stop(bool finished)
        {
            if (finished) { PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save(); }
            index = -1; celebrating = false; placed = false;
            if (card) card.gameObject.SetActive(false);
            if (ring) ring.gameObject.SetActive(false);
        }

        // ---------- Step checks ----------

        float CameraYaw()
        {
            var cam = Services.Session?.ActiveCamera;
            return cam ? cam.transform.eulerAngles.y : 0;
        }

        void TrackLook()
        {
            float yaw = CameraYaw();
            lookTurned += Mathf.Abs(Mathf.DeltaAngle(lookYaw, yaw));
            lookYaw = yaw;
        }

void PlaceRing()
        {
            var player = Services.Session?.Player; var cam = Services.Session?.ActiveCamera;
            hasRing = false;
            if (player != null && cam)
            {
                walkStart = player.transform.position;
                var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
                if (forward.sqrMagnitude < .01f) forward = player.transform.forward;
                hasRing = TryFindWalkTarget(walkStart, forward.normalized, out ringTarget);
                if (hasRing) ring.position = ringTarget + Vector3.up * .04f;
            }
            steps[2].body = hasRing ? "Walk over to the glowing ring." : "Walk around a little.";
        }

        // Ground you can walk to without jumping: flat, at your height, room to stand, no wall in the way
        // and no gap to fall through along the path. Tries in front first, then fans out around you.
        public static bool TryFindWalkTarget(Vector3 feet, Vector3 forward, out Vector3 target)
        {
            int mask = ~((1 << Layers.Survivor) | (1 << 2));
            float[] distances = { 3.5f, 3f, 4.2f, 2.5f, 5f };
            float[] angles = { 0, 25, -25, 50, -50, 80, -80, 120, -120, 180 };
            foreach (float a in angles)
            foreach (float d in distances)
            {
                var dir = Quaternion.Euler(0, a, 0) * forward;
                if (!Ground(feet + dir * d, feet.y, mask, out var spot)) continue;
                if (Physics.CheckCapsule(spot + Vector3.up * .45f, spot + Vector3.up * 1.6f, .3f, mask, QueryTriggerInteraction.Ignore)) continue;
                var from = feet + Vector3.up * .7f; var to = spot + Vector3.up * .7f;
                if (Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore)) continue;
                bool solid = true;
                for (int k = 1; k <= 4 && solid; k++) solid = Ground(Vector3.Lerp(feet, spot, k / 5f), feet.y, mask, out _);
                if (!solid) continue;
                target = spot; return true;
            }
            target = feet; return false;
        }

        static bool Ground(Vector3 at, float feetY, int mask, out Vector3 point)
        {
            point = at;
            if (!Physics.Raycast(new Vector3(at.x, feetY + 1.2f, at.z), Vector3.down, out var hit, 2.4f, mask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < .8f || Mathf.Abs(hit.point.y - feetY) > .6f) return false;
            point = hit.point; return true;
        }

bool WalkedToRing()
        {
            var player = Services.Session?.Player;
            if (player == null) return false;
            var p = player.transform.position;
            if (!hasRing) return Vector3.ProjectOnPlane(p - walkStart, Vector3.up).magnitude > 3;
            return Vector3.ProjectOnPlane(p - ringTarget, Vector3.up).magnitude < 1.3f || Vector3.ProjectOnPlane(p - walkStart, Vector3.up).magnitude > 7;
        }

        // ---------- Look & feel ----------

        void Animate()
        {
            var cam = Services.Session?.ActiveCamera;
            if (!cam) return;
            var flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (flat.sqrMagnitude < .01f) flat = cam.transform.up;
            flat.Normalize();
            // Float about 2 m ahead, but come closer if a wall or block is in the way so the card never sits inside it.
            float distance = 2.1f;
            if (Physics.Raycast(cam.transform.position, flat, out var wall, 2.4f, ~((1 << Layers.Survivor) | (1 << 2)), QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(.9f, wall.distance - .35f);
            var target = cam.transform.position + flat * distance + Vector3.down * .2f;
            var toCard = anchor - cam.transform.position;
            // Stay put while you look around a little; glide back in front when you turn away or walk off.
            if (!placed || Vector3.Angle(Vector3.ProjectOnPlane(toCard, Vector3.up), flat) > 50 || toCard.magnitude > 4 || toCard.magnitude < .8f || toCard.magnitude > distance + .4f) { anchor = target; if (!placed) card.transform.position = target; placed = true; }
            var t = card.transform;
            t.position = Vector3.Lerp(t.position, anchor, 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime));
            t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);

            // Pop in on each new step, bounce on success.
            float age = Time.unscaledTime - stepStart;
            float s = age < .25f ? Mathf.SmoothStep(.7f, 1.06f, age / .25f) : Mathf.Lerp(1.06f, 1, Mathf.Clamp01((age - .25f) / .15f));
            if (celebrating) { float c = celebrateUntil - Time.unscaledTime; s *= 1 + .06f * Mathf.Sin(c * 20) * Mathf.Clamp01(c); }
            t.localScale = Vector3.one * .0016f * s;
            group.alpha = Mathf.Clamp01(age / .2f);
            iconDisc.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 2) * 4);

            if (ring.gameObject.activeSelf)
            {
                float pulse = 1 + .08f * Mathf.Sin(Time.unscaledTime * 5);
                ring.localScale = new Vector3(2.4f * pulse, 1, 2.4f * pulse);
                ring.Rotate(Vector3.up, 40 * Time.unscaledDeltaTime, Space.World);
            }
        }

        void Confetti(int count)
        {
            Color[] colors = { CandyHudVisuals.Mint, CandyHudVisuals.Lavender, CandyHudVisuals.Peach, new Color(1, .45f, .75f), new Color(1, .9f, .3f) };
            var origin = card.transform.position;
            for (int i = 0; i < count; i++)
            {
                var dir = (UnityEngine.Random.insideUnitSphere + Vector3.up * 1.4f).normalized;
                confetti.Emit(new ParticleSystem.EmitParams
                {
                    position = origin + UnityEngine.Random.insideUnitSphere * .25f,
                    velocity = dir * UnityEngine.Random.Range(1.5f, 3.5f),
                    startColor = colors[i % colors.Length],
                    startSize = UnityEngine.Random.Range(.025f, .05f),
                    startLifetime = UnityEngine.Random.Range(1.2f, 2f),
                    rotation3D = UnityEngine.Random.insideUnitSphere * 360,
                    applyShapeToPosition = false
                }, 1);
            }
        }

        void Chime(float pitch)
        {
            var e = CandyAudioEngine.Instance;
            if (e && e.Listener) e.Play(CandySound.UiClick, e.Listener.position, .7f, GetInstanceID(), pitch, true);
            if (e && e.Listener && pitch > 1.2f) e.Play(CandySound.Spawn, e.Listener.position, .6f, GetInstanceID() + 1, 1.6f, true);
        }

        // ---------- Building the card (same pieces as the game's HUD) ----------

        Image Box(Transform parent, string name, Vector2 size, Vector2 position, Color color, bool pill = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.sizeDelta = size; r.anchoredPosition = position;
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            if (pill) { img.sprite = CandyHudVisuals.Pill; img.type = Image.Type.Sliced; }
            return img;
        }

        Text Label(Transform parent, string value, Vector2 size, Vector2 position, int fontSize, Color color, TextAnchor align, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = value; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize; t.color = color; t.alignment = align; t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.sizeDelta = size; t.rectTransform.anchoredPosition = position;
            return t;
        }

void BuildCard()
        {
            var go = new GameObject("Tutorial card", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            card = go.AddComponent<Canvas>(); card.renderMode = RenderMode.WorldSpace; card.sortingOrder = 50;
            ((RectTransform)go.transform).sizeDelta = new Vector2(860, 360);
            group = go.AddComponent<CanvasGroup>();
            var root = go.transform;

            Box(root, "Shadow", new Vector2(860, 360), new Vector2(6, -10), new Color(0, 0, 0, .35f));
            Box(root, "Panel", new Vector2(860, 360), Vector2.zero, CandyHudVisuals.Ink);
            Box(root, "Accent", new Vector2(820, 12), new Vector2(0, 164), CandyHudVisuals.Lavender);

            iconDisc = Box(root, "Icon disc", new Vector2(200, 200), new Vector2(-300, 10), CandyHudVisuals.Lavender);
            iconImage = Box(iconDisc.transform, "Icon", new Vector2(140, 140), Vector2.zero, CandyHudVisuals.Ink, false);
            checkImage = Box(iconDisc.transform, "Check", new Vector2(160, 160), Vector2.zero, Color.white, false);
            checkImage.sprite = Sprite.Create(CheckTexture(), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));

            counterText = Label(root, "", new Vector2(300, 30), new Vector2(-15, 128), 22, CandyHudVisuals.Muted, TextAnchor.MiddleLeft, true);
            dots = new Image[10];
            for (int i = 0; i < dots.Length; i++) dots[i] = Box(root, "Dot", new Vector2(20, 20), new Vector2(178 + i * 24, 128), CandyHudVisuals.Card);
            titleText = Label(root, "", new Vector2(560, 76), new Vector2(115, 78), 60, Color.white, TextAnchor.MiddleLeft, true);
            // One line only: shrink long titles instead of wrapping into the text below.
            titleText.resizeTextForBestFit = true; titleText.resizeTextMinSize = 30; titleText.resizeTextMaxSize = 60;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap; titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.rectTransform.sizeDelta = new Vector2(560, 64);
            bodyText = Label(root, "", new Vector2(560, 106), new Vector2(115, -13), 32, new Color(.93f, .9f, 1f), TextAnchor.UpperLeft);
            bodyText.resizeTextForBestFit = true; bodyText.resizeTextMinSize = 22; bodyText.resizeTextMaxSize = 32; bodyText.verticalOverflow = VerticalWrapMode.Truncate; bodyText.lineSpacing = 1.05f;
            chip = Box(root, "Key chip", new Vector2(560, 64), new Vector2(115, -100), CandyHudVisuals.Card);
            chipText = Label(chip.transform, "", new Vector2(540, 60), Vector2.zero, 30, CandyHudVisuals.Mint, TextAnchor.MiddleCenter, true);
            skipText = Label(root, "", new Vector2(820, 28), new Vector2(0, -158), 20, CandyHudVisuals.Muted, TextAnchor.MiddleCenter);
        }

        void BuildWorldHelpers()
        {
            // Glowing target ring for the walk step.
            var go = new GameObject("Tutorial ring");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            ringMaterial = Glow(RingTexture());
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ringMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring = go.transform; ring.gameObject.SetActive(false);
            // A soft beacon of light rising from the ring, visible from across the tower.
            var beacon = new GameObject("Tutorial beacon");
            beacon.transform.SetParent(ring, false);
            beacon.transform.localPosition = new Vector3(0, 1.4f, 0); beacon.transform.localScale = new Vector3(.41f, 1.4f, .41f);
            beacon.AddComponent<MeshFilter>().sharedMesh = OpenCylinder();
            var br = beacon.AddComponent<MeshRenderer>(); br.sharedMaterial = Glow(BeaconTexture()); br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Confetti.
            var c = new GameObject("Tutorial confetti");
            c.transform.SetParent(transform, false);
            confetti = c.AddComponent<ParticleSystem>();
            confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = confetti.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = .5f; main.startRotation3D = true;
            var em = confetti.emission; em.enabled = false; var sh = confetti.shape; sh.enabled = false;
            var drag = confetti.limitVelocityOverLifetime; drag.enabled = true; drag.drag = .8f;
            var rot = confetti.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6, 6); rot.y = new ParticleSystem.MinMaxCurve(-6, 6); rot.z = new ParticleSystem.MinMaxCurve(-6, 6);
            var col = confetti.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var pr = c.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh; pr.mesh = Quad();
            var pm = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit")) { name = "Confetti" };
            pr.sharedMaterial = pm; pr.alignment = ParticleSystemRenderSpace.World;
            confetti.Play();
        }

        // Additive mint glow on URP's particle shader (the plain unlit shader rendered too faint here).
        static Material Glow(Texture2D texture)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit")) { name = "Tutorial glow" };
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
            m.SetTexture("_BaseMap", texture); m.mainTexture = texture;
            m.SetColor("_BaseColor", CandyHudVisuals.Mint);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);
            return m;
        }

        static Mesh cylinder;
        static Mesh OpenCylinder()
        {
            if (cylinder) return cylinder;
            const int sides = 32;
            var v = new Vector3[(sides + 1) * 2]; var uv = new Vector2[v.Length]; var t = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = p + Vector3.down; v[i * 2 + 1] = p + Vector3.up;
                uv[i * 2] = new Vector2(i / (float)sides, 0); uv[i * 2 + 1] = new Vector2(i / (float)sides, 1);
                if (i < sides) { int b = i * 2; t[i * 6] = b; t[i * 6 + 1] = b + 1; t[i * 6 + 2] = b + 2; t[i * 6 + 3] = b + 2; t[i * 6 + 4] = b + 1; t[i * 6 + 5] = b + 3; }
            }
            cylinder = new Mesh { name = "Beacon" }; cylinder.vertices = v; cylinder.uv = uv; cylinder.triangles = t; cylinder.RecalculateNormals(); cylinder.RecalculateBounds();
            return cylinder;
        }

        static Texture2D beaconTex;
        static Texture2D BeaconTexture()
        {
            if (beaconTex) return beaconTex;
            var px = new Color32[4 * 64];
            for (int y = 0; y < 64; y++) { float a = Mathf.Pow(1 - y / 63f, 1.6f) * .55f; for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(a, a, a, a); }
            beaconTex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { name = "Beacon fade", wrapMode = TextureWrapMode.Clamp };
            beaconTex.SetPixels32(px); beaconTex.Apply(false, true);
            return beaconTex;
        }

        static Mesh quad;
        static Mesh Quad()
        {
            if (quad) return quad;
            quad = new Mesh { name = "Flat quad" };
            quad.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };
            quad.RecalculateNormals(); quad.RecalculateBounds();
            return quad;
        }

        static Texture2D ringTex, checkTex;
        static Texture2D RingTexture()
        {
            if (ringTex) return ringTex;
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(S, S) * .5f) / (S * .5f);
                float band = Mathf.Clamp01(1 - Mathf.Abs(d - .82f) / .1f);
                float glow = Mathf.Clamp01(1 - Mathf.Abs(d - .82f) / .3f) * .35f;
                float fill = d < .78f ? .1f : 0;
                // Dashes around the ring.
                float a = Mathf.Atan2(y - S * .5f, x - S * .5f);
                float dash = Mathf.Sin(a * 12) > -.2f ? 1 : .35f;
                float alpha = Mathf.Clamp01(band * dash + glow + fill);
                px[y * S + x] = new Color(alpha, alpha, alpha, alpha);   // premultiplied: empty pixels add nothing
            }
            ringTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "Tutorial ring", wrapMode = TextureWrapMode.Clamp };
            ringTex.anisoLevel = 8; ringTex.SetPixels32(px); ringTex.Apply(false, true);
            return ringTex;
        }

        // A bold rounded checkmark, drawn like the HUD's own line icons.
        static Texture2D CheckTexture()
        {
            if (checkTex) return checkTex;
            const int S = 64; var px = new Color[S * S];
            void Dot(float cx, float cy, float r)
            {
                for (int y = Mathf.Max(0, (int)(cy - r - 1)); y < Mathf.Min(S, cy + r + 2); y++)
                for (int x = Mathf.Max(0, (int)(cx - r - 1)); x < Mathf.Min(S, cx + r + 2); x++)
                {
                    float a = Mathf.Clamp01(r + .5f - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)));
                    if (a > px[y * S + x].a) px[y * S + x] = new Color(1, 1, 1, a);
                }
            }
            void Line(float x0, float y0, float x1, float y1, float w)
            {
                int n = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2);
                for (int i = 0; i <= n; i++) { float t = i / (float)Mathf.Max(1, n); Dot(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), w); }
            }
            Line(14, 33, 26, 21, 5); Line(26, 21, 50, 46, 5);
            checkTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "Check", wrapMode = TextureWrapMode.Clamp };
            checkTex.SetPixels(px); checkTex.Apply(false, true);
            return checkTex;
        }
    }
}
