using System;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyResultsHud : MonoBehaviour
    {
        const int FullPage = 5, SplitPage = 5, PortraitSize = 74;
        static readonly Color Ink = new Color(.065f, .032f, .135f, .98f);
        static readonly Color Mint = new Color(.58f, 1f, .83f);
        sealed class Row
        {
            public Image background;
            public Text rank, name, stats, initial;
            public RawImage portrait;
        }

        readonly Row[] primary = new Row[FullPage], secondary = new Row[SplitPage];
        readonly Image[] buttons = new Image[4];
        readonly Text[] labels = new Text[4];
        readonly BoxCollider[] targets = new BoxCollider[4];
        MatchPlayerFrame[] sorted = Array.Empty<MatchPlayerFrame>(), infectors = Array.Empty<MatchPlayerFrame>();
        Canvas canvas;
        Text title, summary, roomCode, primaryHeading, secondaryHeading, paging;
        CandyScorePortraits portraits;
        CandyScorePodium podium;
        IMatchRuntime owner;
        MatchFrame rankedFrame;
        int rankedRevision, page, infectionPage;
        bool visible, wasVr, split, rankedCurated, modalHidden;
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
                modalHidden = false;
                rankedFrame = null;
                podium?.Hide();
                if (canvas) canvas.gameObject.SetActive(false);
                return;
            }

            bool modal = CandyComfortPanel.Showing(session) || CharacterPicker.Showing(session)
                || CandyWildlifePanel.Showing(session) || GameExperiencePanel.Showing(session);
            if (modal || modalHidden)
            {
                modalHidden = modal;
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
                session.SetMenu(true);
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
                rect.SetPositionAndRotation(eye.transform.position + openingForward * 1.8f,
                    Quaternion.LookRotation(openingForward, Vector3.up));
                rect.localScale = Vector3.one * .0015f;
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

            Draw(match, frame);
            if (canvas.enabled) podium.Draw();
            labels[1].text = MultiplayerPanel.LobbyAction(session.Multiplayer).ToUpperInvariant();
            string code = MultiplayerPanel.JoinCode(session.Multiplayer);
            roomCode.text = code.Length > 0 ? "JOIN CODE   " + code + (wasVr ? "" : "   ·   COPY") : "";
            var mouse = Mouse.current;
            if (!wasVr && code.Length > 0 && mouse != null && mouse.leftButton.wasPressedThisFrame
                && RectTransformUtility.RectangleContainsScreenPoint(roomCode.rectTransform, mouse.position.ReadValue()))
                GUIUtility.systemCopyBuffer = code;
            var ray = eye ? new Ray(eye.transform.position, eye.transform.forward) : default;
            for (int i = 0; i < targets.Length; i++)
            {
                bool enabled = i == 0 ? CanStartRound(match.Authority, session as IGameExperienceControls) : i == 1 ||
                    (split ? i == 2 ? Pages(sorted.Length, PageSize) > 1 : Pages(infectors.Length, SplitPage) > 1
                    : i == 2 ? page > 0 : page + 1 < Pages(sorted.Length, PageSize));
                bool hovered = enabled && (wasVr
                    ? eye && targets[i].Raycast(ray, out _, 4)
                    : mouse != null && RectTransformUtility.RectangleContainsScreenPoint(buttons[i].rectTransform, mouse.position.ReadValue()));
                buttons[i].color = !enabled ? new Color(.17f, .12f, .24f) : hovered ? new Color(.23f, .39f, .46f)
                    : i < 2 ? new Color(.19f, .25f, .36f) : new Color(.20f, .13f, .33f);
                labels[i].color = enabled ? (i == 0 ? Mint : Color.white) : new Color(.69f, .65f, .76f);
                if (hovered && (wasVr ? session.MenuSelectPressed : mouse.leftButton.wasPressedThisFrame))
                {
                    Act(i);
                    break;
                }
            }
        }

        int PageSize => split ? SplitPage : FullPage;
        static int Pages(int count, int size) => Mathf.Max(1, (count + size - 1) / size);

        internal static bool CanStartRound(bool authority, IGameExperienceControls controls)
            => controls?.Experience == GameExperience.Game ? controls.CanConfigureExperience : authority;

        internal static string NextRoundLabel(bool authority, IGameExperienceControls controls)
        {
            if (controls?.Experience==GameExperience.Game&&controls.AutomaticRounds&&
                controls.GamePhase==RoomGamePhase.Results)
                return "NEXT LOBBY IN "+Mathf.CeilToInt(controls.CountdownSeconds)+" S";
            if (CanStartRound(authority, controls)) return "STAY / NEXT ROUND";
            return controls?.Experience == GameExperience.Game ? "STAY · WAIT FOR ROOM LEADER" : "STAY · WAIT FOR HOST";
        }

        void Act(int index)
        {
            var match = MatchRuntime.Current;
            var session = Services.Session;
            if (match == null || session == null) return;
            switch (index)
            {
                case 0: if (CanStartRound(match.Authority, session as IGameExperienceControls)) { match.StartRound(); session.SetMenu(false); } break;
                case 1: match.ReturnToLobby(); break;
                case 2: page = split ? (page + 1) % Pages(sorted.Length, PageSize) : page - 1; break;
                case 3:
                    if (split) infectionPage = (infectionPage + 1) % Pages(infectors.Length, SplitPage);
                    else page++;
                    break;
            }
        }

        void Draw(IMatchRuntime match, MatchFrame frame)
        {
            title.text = string.IsNullOrEmpty(frame.winner) ? "ROUND COMPLETE"
                : CandyHudContent.Safe(frame.winner).ToUpperInvariant() + (frame.winner == "Draw" ? "" : " WINS");
            int own = Array.FindIndex(sorted, player => player.id == match.LocalActorId);
            var local = frame.players.FirstOrDefault(player => player.id == match.LocalActorId);
            string place = CandyMatchHud.StandingsPlace(local, own, rankedCurated);
            summary.text = CandyHudContent.Mode(frame.rules.mode) + "  ·  " + CandyHudContent.Safe(frame.metricLabel)
                + "  ·  " + CandyMatchHud.StandingsCount(sorted.Length, rankedCurated)
                + (place.Length > 0 ? "  ·  " + place : "");
            bool showPodium = frame.rules.mode != GameModeKind.MurderMystery && sorted.Length > 0;
            float standingsX = showPodium ? -202 : 0;
            primaryHeading.text = split ? "LONGEST SURVIVORS" : "FINAL STANDINGS";
            primaryHeading.rectTransform.anchoredPosition = new Vector2(standingsX - (split ? 200 : 0), 219);
            secondaryHeading.rectTransform.anchoredPosition = new Vector2(standingsX + 200, 219);
            secondaryHeading.gameObject.SetActive(split);
            secondaryHeading.text = "SURVIVORS INFECTED";
            DrawRows(primary, sorted, page, PageSize, standingsX - (split ? 200 : 0), split ? 380 : 780, match, frame.rules.mode, false);
            DrawRows(secondary, infectors, infectionPage, split ? SplitPage : 0, standingsX + 200, 380, match, frame.rules.mode, true);
            podium.SetPlayers(match, sorted, showPodium && canvas.enabled);
            paging.rectTransform.anchoredPosition = new Vector2(standingsX, -232);
            buttons[2].rectTransform.anchoredPosition = new Vector2(standingsX - 290, -232);
            buttons[3].rectTransform.anchoredPosition = new Vector2(standingsX + 290, -232);
            paging.text = split
                ? $"Survivors {page + 1} / {Pages(sorted.Length, PageSize)}    ·    Infections {infectionPage + 1} / {Pages(infectors.Length, SplitPage)}"
                : $"Page {page + 1} / {Pages(sorted.Length, PageSize)}";
            labels[0].text = NextRoundLabel(match.Authority, Services.Session as IGameExperienceControls);
            labels[2].text = split ? "NEXT SURVIVORS" : "PREVIOUS";
            labels[3].text = split ? "NEXT INFECTORS" : "NEXT";
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
                rect.sizeDelta = new Vector2(width, 80);
                rect.anchoredPosition = new Vector2(x, 162 - i * 83);
                row.background.color = own ? new Color(.12f, .31f, .32f) : new Color(.145f, .09f, .24f);
                row.rank.text = (rank + 1).ToString();

                row.name.color = own ? Mint : Color.white;
                float nameWidth = split ? 248 : 360;
                row.name.rectTransform.sizeDelta = new Vector2(nameWidth, 36);
                row.name.rectTransform.anchoredPosition = new Vector2(120 + nameWidth * .5f, split ? 15 : 0);
                portraits.SetName(row.name, (own ? "YOU · " : "") + player.name);
                float statsWidth = split ? 248 : 274;
                row.stats.rectTransform.sizeDelta = new Vector2(statsWidth, 26);
                row.stats.rectTransform.anchoredPosition = new Vector2(-statsWidth * .5f - (split ? 12 : 14), split ? -19 : 0);
                row.stats.resizeTextForBestFit = true;
                row.stats.resizeTextMinSize = 18;
                row.stats.resizeTextMaxSize = 20;
                row.stats.alignment = split ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
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
            var panel = Panel("Results panel", root.transform, new Vector2(1220, 728), Vector2.zero, Ink).transform;
            podium = new CandyScorePodium(transform, panel, new Vector2(405, -22));
            title = Label("Winner", panel, new Vector2(982, 48), new Vector2(0, 322), 34);
            summary = Label("Mode and local rank", panel, new Vector2(982, 26), new Vector2(0, 281), 19);
            summary.color = Mint;
            roomCode = Label("Join code", panel, new Vector2(982, 26), new Vector2(0, 252), 20);
            roomCode.color = Mint;
            primaryHeading = Label("Standings heading", panel, new Vector2(380, 28), new Vector2(0, 219), 19);
            secondaryHeading = Label("Infection heading", panel, new Vector2(380, 28), new Vector2(200, 219), 19);
            for (int i = 0; i < primary.Length; i++) primary[i] = BuildRow(panel);
            for (int i = 0; i < secondary.Length; i++) secondary[i] = BuildRow(panel);

            paging = Label("Page count", panel, new Vector2(360, 34), new Vector2(-202, -232), 18);
            string[] names = { "STAY / NEXT ROUND", "RETURN TO LOBBY", "PREVIOUS", "NEXT" };
            for (int i = 0; i < buttons.Length; i++)
            {
                Vector2 size = i < 2 ? new Vector2(474, 61) : new Vector2(194, 36);
                Vector2 position = i < 2 ? new Vector2(i == 0 ? -246 : 246, -324) : new Vector2(i == 2 ? -492 : 88, -232);
                buttons[i] = Panel(names[i], panel, size, position, Ink);
                labels[i] = Label(names[i], buttons[i].transform, size, Vector2.zero, i < 2 ? 23 : 16);
                labels[i].text = names[i];
                targets[i] = buttons[i].gameObject.AddComponent<BoxCollider>();
                targets[i].isTrigger = true;
                targets[i].size = new Vector3(size.x, size.y, 1);
            }
        }

        static Row BuildRow(Transform parent)
        {
            var background = Panel("Score row", parent, new Vector2(780, 80), Vector2.zero, Ink);
            var row = background.transform;
            var rank = Label("Rank", row, new Vector2(40, 48), new Vector2(22, 0), 21);
            Anchor(rank.rectTransform, 0);
            var portraitObject = new GameObject("Avatar portrait", typeof(RectTransform), typeof(RawImage));
            portraitObject.transform.SetParent(row, false);
            portraitObject.layer = 5;
            var portrait = portraitObject.GetComponent<RawImage>();
            portrait.raycastTarget = false;
            portrait.rectTransform.sizeDelta = Vector2.one * PortraitSize;
            portrait.rectTransform.anchoredPosition = new Vector2(77, 0);
            Anchor(portrait.rectTransform, 0);
            var initial = Label("Initial", portrait.transform, Vector2.one * PortraitSize, Vector2.zero, 36);
            var name = Label("Player name", row, new Vector2(420, 32), new Vector2(316, 0), 26);
            Anchor(name.rectTransform, 0);
            name.alignment = TextAnchor.MiddleLeft;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            var stats = Label("Mode stats", row, new Vector2(410, 26), new Vector2(-219, 0), 20);
            Anchor(stats.rectTransform, 1);
            stats.alignment = TextAnchor.MiddleRight;
            return new Row { background = background, rank = rank, name = name, stats = stats, portrait = portrait, initial = initial };
        }

        static void Anchor(RectTransform rect, float x) => rect.anchorMin = rect.anchorMax = new Vector2(x, .5f);

        static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            var panel = go.GetComponent<Image>();
            panel.sprite = CandyHudVisuals.Pill;
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
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = position;
            return label;
        }

        void OnDisable() { visible = false; podium?.Hide(); if (canvas) canvas.gameObject.SetActive(false); }
        void OnDestroy() { podium?.Dispose(); if (canvas) Destroy(canvas.gameObject); }
    }
}
