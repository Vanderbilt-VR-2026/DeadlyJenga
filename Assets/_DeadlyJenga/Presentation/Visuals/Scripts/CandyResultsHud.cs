using System;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using V = DeadlyJenga.CastlePrototype.CandyHudVisuals;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyResultsHud : MonoBehaviour
    {
        const int FullPage = 8, SplitPage = 5;
        const float AutoReturnSeconds = 45f;
        static readonly Color Gold = new Color(1f, .82f, .32f), Silver = new Color(.84f, .87f, .96f), Bronze = new Color(.96f, .62f, .42f);
        sealed class Row
        {
            public Image background, badge;
            public Text rank, name, stats, initial;
            public RawImage portrait;
        }

        readonly Row[] primary = new Row[FullPage], secondary = new Row[SplitPage];
        readonly Image[] buttons = new Image[4], lips = new Image[4];
        readonly Text[] labels = new Text[4], notes = new Text[4];
        readonly BoxCollider[] targets = new BoxCollider[4];
        readonly Vector2[] homes = new Vector2[4];
        MatchPlayerFrame[] sorted = Array.Empty<MatchPlayerFrame>(), infectors = Array.Empty<MatchPlayerFrame>();
        Canvas canvas;
        Image ribbon, shadow, frameImage, panelImage;
        RectTransform[] header = Array.Empty<RectTransform>();
        Vector2[] headerHomes = Array.Empty<Vector2>();
        float trim;
        Text title, summary, roomCode, primaryHeading, secondaryHeading, paging;
        CandyScorePortraits portraits;
        IMatchRuntime owner;
        MatchFrame rankedFrame;
        int rankedRevision, page, infectionPage, hoveredButton = -1, outcome;
        bool visible, wasVr, split, rankedCurated, returning;
        float openedAt;
        Camera camera;
        Vector3 openingForward;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += go =>
        {
            if (!go.GetComponent<CandyResultsHud>()) go.AddComponent<CandyResultsHud>();
        };

        void Update()
        {
            var match = MatchRuntime.Current;
            var session = Services.Session;
            var frame = match?.Frame;
            bool show = frame?.phase == RoundPhase.Results && session != null
                && (session as ILobbyMatchVisitControls)?.VisitingLobby != true;
            if (!show)
            {
                visible = false;
                rankedFrame = null;
                if (canvas) canvas.gameObject.SetActive(false);
                return;
            }

            if (!canvas) Build();
            var eye = session.ActiveCamera;
            bool opening = !visible || owner != match;
            if (opening)
            {
                owner = match;
                page = infectionPage = 0;
                rankedFrame = null;
                openedAt = Time.unscaledTime;
                returning = false;
                hoveredButton = -1;
                session.SetMenu(true);
                outcome = Outcome(frame, frame.players.FirstOrDefault(player => player.id == match.LocalActorId));
                CandyFeedback.Pulse(outcome > 0 ? .6f : .3f, outcome > 0 ? .18f : .08f);
                if (eye) CandyAudioControls.PlayUiClick(eye.transform.position);
            }
            if (opening || eye != camera || wasVr != session.IsVR)
            {
                camera = eye;
                wasVr = session.IsVR;
                CandyHudLayout.Configure(canvas, transform, eye, wasVr);
                var forward = eye ? Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up) : Vector3.forward;
                openingForward = forward.sqrMagnitude > .01f ? forward.normalized : Vector3.forward;
                foreach (var target in targets) target.enabled = wasVr;
            }

            visible = true;
            canvas.gameObject.SetActive(true);
            canvas.enabled = !wasVr || eye;
            if (wasVr && eye)
            {
                var rect = (RectTransform)canvas.transform;
                rect.SetParent(transform, false);
                rect.sizeDelta = CandyHudLayout.ReferenceSize;
                var position = eye.transform.position + openingForward * 1.6f + Vector3.down * .12f;
                rect.SetPositionAndRotation(position, Quaternion.LookRotation(position - eye.transform.position, Vector3.up));
                rect.localScale = Vector3.one * .0014f;
            }

            var participants = CandyMatchHud.StandingsParticipants(frame, session as IGameExperienceControls, out bool curated);
            if (rankedFrame != frame || rankedRevision != frame.revision || rankedCurated != curated)
            {
                rankedFrame = frame;
                rankedCurated = curated;
                rankedRevision = frame.revision;
                split = frame.rules.mode == GameModeKind.Infection;
                sorted = participants.OrderByDescending(p => split ? p.survivalSeconds : p.metric).ThenBy(p => p.id, StringComparer.Ordinal).ToArray();
                infectors = split ? participants.OrderByDescending(p => p.infections)
                    .ThenBy(p => p.id, StringComparer.Ordinal).ToArray() : Array.Empty<MatchPlayerFrame>();
                page = Mathf.Min(page, Pages(sorted.Length, PageSize) - 1);
                infectionPage = Mathf.Min(infectionPage, Pages(infectors.Length, SplitPage) - 1);
            }

            bool leader = CanStartRound(match.Authority, session as IGameExperienceControls);
            int remaining = Mathf.Max(0, Mathf.CeilToInt(AutoReturnSeconds - (Time.unscaledTime - openedAt)));
            Draw(match, frame);
            labels[1].text = MultiplayerPanel.LobbyAction(session.Multiplayer).ToUpperInvariant();
            notes[0].text = leader ? "Same map · same mode" : "";
            notes[1].text = leader ? "Auto in " + remaining + "s" : "Host decides · " + remaining + "s";
            if (leader && remaining == 0 && !returning)
            {
                returning = true;
                match.ReturnToLobby();
            }
            string code = MultiplayerPanel.JoinCode(session.Multiplayer);
            roomCode.text = code.Length > 0 ? "JOIN CODE   " + code + (wasVr ? "" : "   ·   COPY") : "";
            var mouse = Mouse.current;
            if (!wasVr && code.Length > 0 && mouse != null && mouse.leftButton.wasPressedThisFrame
                && RectTransformUtility.RectangleContainsScreenPoint(roomCode.rectTransform, mouse.position.ReadValue()))
                GUIUtility.systemCopyBuffer = code;

            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / .55f);
            ribbon.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(.55f, 1f, EaseOutBack(t));

            MenuPointer.Sample input = default;
            bool hasPointer = wasVr && MenuPointer.AimAt(eye, session.Player?.gameObject.GetComponent<IBlasterControls>()?.LeftHanded == true,
                ray => MenuPointer.PanelHit(ray, (RectTransform)canvas.transform), out input);
            var pointer = input.Ray;
            int hovering = -1;
            for (int i = 0; i < targets.Length; i++)
            {
                bool enabled = i == 0 ? leader : i == 1 ||
                    (split ? i == 2 ? Pages(sorted.Length, PageSize) > 1 : Pages(infectors.Length, SplitPage) > 1
                    : i == 2 ? page > 0 : page + 1 < Pages(sorted.Length, PageSize));
                bool hovered = enabled && (wasVr
                    ? hasPointer && targets[i].Raycast(pointer, out _, 4)
                    : mouse != null && RectTransformUtility.RectangleContainsScreenPoint(buttons[i].rectTransform, mouse.position.ReadValue()));
                Style(i, enabled, hovered);
                if (hovered) hovering = i;
                if (hovered && (wasVr ? input.Pressed : mouse.leftButton.wasPressedThisFrame))
                {
                    CandyAudioControls.PlayUiClick(eye ? eye.transform.position : transform.position);
                    CandyFeedback.Pulse(.35f, .05f);
                    Act(i);
                    break;
                }
            }
            if (hovering != hoveredButton)
            {
                hoveredButton = hovering;
                if (hovering >= 0 && wasVr) CandyFeedback.Pulse(.12f, .03f);
            }
        }

        void Style(int i, bool enabled, bool hovered)
        {
            Color face = i == 0 ? V.Mint : i == 1 ? V.Peach : new Color(.36f, .26f, .52f);
            if (!enabled) face = V.Card;
            else if (hovered) face = Color.Lerp(face, Color.white, .22f);
            buttons[i].color = face;
            lips[i].color = enabled ? Color.Lerp(face, Color.black, .45f) : Color.Lerp(V.Card, Color.black, .35f);
            Color ink = enabled && i < 2 ? V.Ink : enabled ? Color.white : V.Muted;
            labels[i].color = ink;
            notes[i].color = new Color(ink.r, ink.g, ink.b, .75f);
            float grow = hovered ? 1.04f : 1f;
            buttons[i].rectTransform.localScale = Vector3.one * grow;
            buttons[i].rectTransform.anchoredPosition = homes[i] + (hovered ? Vector2.up * 2 : Vector2.zero);
        }

        void Fit(int rows)
        {
            float next = (FullPage - rows) * 40;
            if (Mathf.Approximately(next, trim)) return;
            trim = next;
            shadow.rectTransform.sizeDelta = frameImage.rectTransform.sizeDelta = new Vector2(1012, 672 - trim);
            panelImage.rectTransform.sizeDelta = new Vector2(996, 656 - trim);
            for (int i = 0; i < header.Length; i++) header[i].anchoredPosition = headerHomes[i] + Vector2.down * trim * .5f;
            paging.rectTransform.anchoredPosition = new Vector2(0, -214 + trim * .5f);
            for (int i = 0; i < homes.Length; i++)
            {
                bool big = i < 2;
                homes[i] = new Vector2(big ? i == 0 ? -235 : 235 : i == 2 ? -365 : 365, (big ? -276 : -214) + trim * .5f);
                lips[i].rectTransform.anchoredPosition = homes[i] + Vector2.down * (big ? 7 : 4);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            float k = t - 1;
            return 1 + c3 * k * k * k + c1 * k * k;
        }

        static int Outcome(MatchFrame frame, MatchPlayerFrame local)
        {
            string winner = frame.winner ?? "";
            if (local == null || winner.Length == 0 || winner == "Draw" || winner == "Nobody") return 0;
            if (frame.winnerTeam >= 0) return local.team == frame.winnerTeam ? 1 : -1;
            if (winner == "Survivors") return local.role == "infected" ? -1 : 1;
            if (winner == "Infected") return local.role == "infected" ? 1 : -1;
            if (winner == "Innocents" || winner == "Murderer") return 0;
            return winner == local.name ? 1 : -1;
        }

        int PageSize => split ? SplitPage : FullPage;
        static int Pages(int count, int size) => Mathf.Max(1, (count + size - 1) / size);

        internal static bool CanStartRound(bool authority, IGameExperienceControls controls)
            => controls?.Experience == GameExperience.Game ? controls.CanConfigureExperience : authority;

        internal static string NextRoundLabel(bool authority, IGameExperienceControls controls)
        {
            if (CanStartRound(authority, controls)) return "PLAY AGAIN";
            return controls?.Experience == GameExperience.Game ? "LEADER STARTS NEXT" : "HOST STARTS NEXT";
        }

        void Act(int index)
        {
            var match = MatchRuntime.Current;
            var session = Services.Session;
            if (match == null || session == null) return;
            switch (index)
            {
                case 0: if (CanStartRound(match.Authority, session as IGameExperienceControls)) { match.StartRound(); session.SetMenu(false); } break;
                case 1: returning = true; match.ReturnToLobby(); break;
                case 2: page = split ? (page + 1) % Pages(sorted.Length, PageSize) : page - 1; break;
                case 3:
                    if (split) infectionPage = (infectionPage + 1) % Pages(infectors.Length, SplitPage);
                    else page++;
                    break;
            }
        }

        void Draw(IMatchRuntime match, MatchFrame frame)
        {
            string winner = CandyHudContent.Safe(frame.winner, 16).ToUpperInvariant();
            title.text = outcome > 0 ? "YOU WIN!" : string.IsNullOrEmpty(frame.winner) ? "ROUND OVER"
                : frame.winner == "Draw" ? "IT'S A DRAW" : winner + " WINS";
            ribbon.color = outcome > 0 ? V.Mint : outcome < 0 ? V.Peach : V.Lavender;
            int own = Array.FindIndex(sorted, player => player.id == match.LocalActorId);
            var local = frame.players.FirstOrDefault(player => player.id == match.LocalActorId);
            string place = CandyMatchHud.StandingsPlace(local, own, rankedCurated);
            summary.text = CandyHudContent.Mode(frame.rules.mode) + "  ·  " + CandyHudContent.Safe(frame.metricLabel)
                + "  ·  " + CandyMatchHud.StandingsCount(sorted.Length, rankedCurated)
                + (place.Length > 0 ? "  ·  " + place : "");
            int shown = Mathf.Max(Mathf.Min(PageSize, sorted.Length - page * PageSize), split ? Mathf.Min(SplitPage, infectors.Length - infectionPage * SplitPage) : 0);
            bool paged = split ? Pages(sorted.Length, PageSize) > 1 || Pages(infectors.Length, SplitPage) > 1 : Pages(sorted.Length, PageSize) > 1;
            if (!paged) Fit(Mathf.Clamp(shown, 3, PageSize));
            else Fit(PageSize);
            primaryHeading.text = split ? "LONGEST SURVIVORS" : "FINAL STANDINGS";
            primaryHeading.rectTransform.anchoredPosition = new Vector2(split ? -235 : 0, 136 - trim * .5f);
            secondaryHeading.gameObject.SetActive(split);
            secondaryHeading.text = "SURVIVORS INFECTED";
            DrawRows(primary, sorted, page, PageSize, split ? -235 : 0, split ? 452 : 940, match, frame.rules.mode, false);
            DrawRows(secondary, infectors, infectionPage, split ? SplitPage : 0, 235, 452, match, frame.rules.mode, true);
            paging.text = split
                ? $"Survivors {page + 1} / {Pages(sorted.Length, PageSize)}    ·    Infections {infectionPage + 1} / {Pages(infectors.Length, SplitPage)}"
                : $"Page {page + 1} / {Pages(sorted.Length, PageSize)}";
            paging.gameObject.SetActive(paged);
            for (int i = 2; i < 4; i++) { buttons[i].gameObject.SetActive(paging.gameObject.activeSelf); lips[i].gameObject.SetActive(paging.gameObject.activeSelf); }
            labels[0].text = NextRoundLabel(match.Authority, Services.Session as IGameExperienceControls);
            labels[2].text = split ? "MORE SURVIVORS" : "‹  PREVIOUS";
            labels[3].text = split ? "MORE INFECTORS" : "NEXT  ›";
        }

        void DrawRows(Row[] pool, MatchPlayerFrame[] players, int selectedPage, int pageSize, float x, float width,
            IMatchRuntime match, GameModeKind mode, bool infections)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                var row = pool[i];
                int rank = selectedPage * pageSize + i;
                bool show = i < pageSize && rank < players.Length;
                row.background.gameObject.SetActive(show);
                if (!show) continue;
                var player = players[rank];
                bool own = player.id == match.LocalActorId;
                var rect = row.background.rectTransform;
                rect.sizeDelta = new Vector2(width, 36);
                rect.anchoredPosition = new Vector2(x, 102 - trim * .5f - i * 40);
                row.background.color = own ? new Color(.13f, .38f, .34f) : i % 2 == 0 ? V.Panel : new Color(.15f, .095f, .25f);
                row.rank.text = (rank + 1).ToString();
                bool medal = rank < 3;
                row.badge.color = !medal ? new Color(1, 1, 1, 0) : rank == 0 ? Gold : rank == 1 ? Silver : Bronze;
                row.rank.color = medal ? V.Ink : V.Muted;
                row.name.text = CandyHudContent.Safe(player.name, split ? own ? 12 : 17 : 26) + (own ? "  ·  YOU" : "");
                row.name.color = own ? V.Mint : Color.white;
                row.name.rectTransform.sizeDelta = new Vector2(split ? 210 : 420, 36);
                float statsWidth = split ? 150 : 400;
                row.stats.rectTransform.sizeDelta = new Vector2(statsWidth, 36);
                row.stats.color = own ? V.Mint : V.Muted;
                row.stats.text = infections ? player.infections + " infected" : split ? player.survivalSeconds.ToString("0.0") + "s survived" : CandyHudContent.ResultStats(player, mode);
                CandyScorePortraits.Show(row.portrait, row.initial, portraits.Get(match, player), player);
            }
        }

        void Build()
        {
            var root = new GameObject("Visual - round results", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            root.layer = 5;
            canvas = root.GetComponent<Canvas>();
            canvas.sortingOrder = 80;
            portraits = CandyScorePortraits.For(gameObject);
            shadow = Panel("Results shadow", root.transform, new Vector2(1012, 672), new Vector2(0, -10), new Color(.03f, .015f, .07f, .9f));
            frameImage = Panel("Results frame", root.transform, new Vector2(1012, 672), Vector2.zero, new Color(.42f, .30f, .62f));
            panelImage = Panel("Results panel", root.transform, new Vector2(996, 656), Vector2.zero, V.Ink);
            var panel = panelImage.transform;

            var ribbonLip = Panel("Ribbon lip", panel, new Vector2(560, 84), new Vector2(0, 252), new Color(.05f, .03f, .1f, .6f));
            ribbon = Panel("Winner ribbon", panel, new Vector2(560, 84), new Vector2(0, 258), V.Lavender);
            title = Label("Winner", ribbon.transform, new Vector2(540, 72), Vector2.zero, 44);
            title.font = V.TitleFont;
            title.color = V.Ink;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            summary = Label("Mode and local rank", panel, new Vector2(940, 32), new Vector2(0, 194), 20);
            summary.color = V.Muted;
            roomCode = Label("Join code", panel, new Vector2(940, 28), new Vector2(0, 164), 18);
            roomCode.color = V.Mint;
            primaryHeading = Label("Standings heading", panel, new Vector2(452, 30), new Vector2(0, 136), 18);
            primaryHeading.color = V.Lavender;
            secondaryHeading = Label("Infection heading", panel, new Vector2(452, 30), new Vector2(235, 136), 18);
            secondaryHeading.color = V.Lavender;
            header = new[] { ribbonLip.rectTransform, ribbon.rectTransform, summary.rectTransform, roomCode.rectTransform, secondaryHeading.rectTransform };
            headerHomes = header.Select(r => r.anchoredPosition).ToArray();
            for (int i = 0; i < primary.Length; i++) primary[i] = BuildRow(panel);
            for (int i = 0; i < secondary.Length; i++) secondary[i] = BuildRow(panel);

            paging = Label("Page count", panel, new Vector2(420, 34), new Vector2(0, -214), 18);
            paging.color = V.Muted;
            string[] names = { "PLAY AGAIN", "RETURN TO LOBBY", "‹  PREVIOUS", "NEXT  ›" };
            for (int i = 0; i < buttons.Length; i++)
            {
                bool big = i < 2;
                Vector2 size = big ? new Vector2(452, 78) : new Vector2(210, 42);
                Vector2 position = big ? new Vector2(i == 0 ? -235 : 235, -276) : new Vector2(i == 2 ? -365 : 365, -214);
                homes[i] = position;
                lips[i] = Panel(names[i] + " lip", panel, size, position + Vector2.down * (big ? 7 : 4), V.Ink);
                buttons[i] = Panel(names[i], panel, size, position, V.Ink);
                labels[i] = Label(names[i], buttons[i].transform, new Vector2(size.x, big ? 40 : 36), big ? new Vector2(0, 10) : Vector2.zero, big ? 26 : 17);
                labels[i].text = names[i];
                if (big) labels[i].font = V.TitleFont;
                notes[i] = Label(names[i] + " note", buttons[i].transform, new Vector2(size.x, 24), new Vector2(0, -20), 15);
                notes[i].gameObject.SetActive(big);
                targets[i] = buttons[i].gameObject.AddComponent<BoxCollider>();
                targets[i].isTrigger = true;
                targets[i].size = new Vector3(size.x, size.y, 1);
            }
        }

        static Row BuildRow(Transform parent)
        {
            var background = Panel("Score row", parent, new Vector2(940, 36), Vector2.zero, V.Panel);
            var row = background.transform;
            var badge = Panel("Rank badge", row, new Vector2(30, 30), new Vector2(24, 0), Color.clear);
            Anchor(badge.rectTransform, 0);
            var rank = Label("Rank", badge.transform, new Vector2(30, 30), Vector2.zero, 17);
            rank.font = V.TitleFont;
            var portraitObject = new GameObject("Avatar portrait", typeof(RectTransform), typeof(RawImage));
            portraitObject.transform.SetParent(row, false);
            portraitObject.layer = 5;
            var portrait = portraitObject.GetComponent<RawImage>();
            portrait.raycastTarget = false;
            portrait.rectTransform.sizeDelta = new Vector2(30, 30);
            portrait.rectTransform.anchoredPosition = new Vector2(62, 0);
            Anchor(portrait.rectTransform, 0);
            var initial = Label("Initial", portrait.transform, new Vector2(30, 30), Vector2.zero, 16);
            var name = Label("Player name", row, new Vector2(420, 36), new Vector2(88, 0), 19);
            Anchor(name.rectTransform, 0);
            name.rectTransform.pivot = new Vector2(0, .5f);
            name.alignment = TextAnchor.MiddleLeft;
            var stats = Label("Mode stats", row, new Vector2(400, 36), new Vector2(-16, 0), 17);
            Anchor(stats.rectTransform, 1);
            stats.rectTransform.pivot = new Vector2(1, .5f);
            stats.alignment = TextAnchor.MiddleRight;
            return new Row { background = background, badge = badge, rank = rank, name = name, stats = stats, portrait = portrait, initial = initial };
        }

        static void Anchor(RectTransform rect, float x) => rect.anchorMin = rect.anchorMax = new Vector2(x, .5f);

        static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            var panel = go.GetComponent<Image>();
            panel.sprite = V.Pill;
            panel.type = Image.Type.Sliced;
            panel.color = color;
            panel.raycastTarget = false;
            panel.rectTransform.sizeDelta = size;
            panel.rectTransform.anchoredPosition = position;
            return panel;
        }

        static Text Label(string name, Transform parent, Vector2 size, Vector2 position, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            var label = go.GetComponent<Text>();
            label.font = V.TextFont;
            label.fontSize = fontSize;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = position;
            return label;
        }

        void OnDisable() { visible = false; if (canvas) canvas.gameObject.SetActive(false); }
        void OnDestroy() { if (canvas) Destroy(canvas.gameObject); }
    }
}
