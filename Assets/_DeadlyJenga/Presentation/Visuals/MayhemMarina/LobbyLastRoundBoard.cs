using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class LobbyLastRoundBoard : MonoBehaviour
    {
        const int PageSize = 5;
        static readonly Color Ink = new(.045f, .095f, .12f);
        static readonly Color Mint = new(.56f, 1f, .83f);
        static readonly Vector3[] StepTops = MayhemMarina.FameSteps
            .Select(step => step + Vector3.up * MayhemMarina.FameStepCap).ToArray();

        sealed class Standing
        {
            public MatchPlayerFrame player;
            public string text;
        }

        sealed class Result
        {
            public string headline, mode;
            public Standing[] standings, infections;
            public GameObject statues;
            public CandyScorePodium podium;
            public int nextPortrait, portraitPass;
            public readonly Dictionary<string, Texture> portraits = new();

            public void Dispose()
            {
                podium.Dispose();
                foreach (var texture in portraits.Values) Destroy(texture);
            }
        }

        sealed class Row
        {
            public Text text, stats, initial;
            public RawImage portrait;
        }

        static Result latest;
        static IMatchRuntime observed;
        static RoundPhase observedPhase;
        Result displayed;
        Text title, details, heading, infectionHeading, footer;
        readonly Row[] left = new Row[PageSize], right = new Row[PageSize];
        int page, drawnPortraits;
        float nextPage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            latest?.Dispose();
            latest = null;
            observed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.LobbyStarted += Attach;

        static void Attach(GameObject root)
        {
            if (!(root.GetComponent<IWorldQuery>() is IWorldQuery world) || world.Radius < 30
                || root.GetComponent<LobbyLastRoundBoard>()) return;
            root.AddComponent<LobbyLastRoundBoard>().Build();
        }

        internal static void Observe(IMatchRuntime match, ISessionInfo session)
        {
            var frame = match?.Frame;
            if (frame?.phase != RoundPhase.Victory && frame?.phase != RoundPhase.Results)
            {
                observed = null;
                return;
            }
            if (!ReferenceEquals(match, observed) || frame.phase != observedPhase)
            {
                var mode = frame.rules.mode;
                var players = CandyMatchHud.StandingsParticipants(frame, session as IGameExperienceControls, out _);
                if (!players.Any()) return;
                bool split = mode == GameModeKind.Infection;
                var ranked = CandyResultsHud.Rank(players, split, false);
                Standing[] Rows(MatchPlayerFrame[] order, bool infections) => order.Select((player, rank) => new Standing
                {
                    player = player.Copy(),
                    text = $"{rank + 1}.  {CandyHudContent.Safe(player.name)}   \u00B7   {CandyResultsHud.RankStats(player, mode, infections)}"
                }).ToArray();

                if (!ReferenceEquals(match, observed))
                {
                    latest?.Dispose();
                    var statues = new GameObject("Hall of Fame avatars");
                    DontDestroyOnLoad(statues);
                    statues.SetActive(false);
                    latest = new Result { statues = statues, podium = new CandyScorePodium(statues.transform) };
                }

                latest.headline = string.IsNullOrEmpty(frame.winner) ? "Round complete"
                    : CandyHudContent.Safe(frame.winner) + (frame.winner == "Draw" ? "" : " wins");
                latest.mode = CandyHudContent.Mode(mode) + "  \u00B7  " + CandyHudContent.Safe(frame.metricLabel);
                latest.standings = Rows(ranked, false);
                latest.infections = split ? Rows(CandyResultsHud.Rank(players, true, true), true) : null;
                latest.nextPortrait = 0;
                latest.portraitPass = 0;
                latest.podium.SetWorldPlayers(match, ranked, StepTops);
                observed = match;
                observedPhase = frame.phase;

                var board = FindFirstObjectByType<LobbyLastRoundBoard>();
                if (board) { board.page = 0; board.Draw(); }
            }

            // The match owns the original textures. Retain a copy across scene changes.
            if (session is not Component source || latest.portraitPass >= 2 ||
                latest.portraits.Count == latest.standings.Length) return;
            var cache = CandyScorePortraits.For(source.gameObject);
            for (int i = 0; i < 2 && latest.nextPortrait < latest.standings.Length; i++)
            {
                var player = latest.standings[latest.nextPortrait++].player;
                if (latest.portraits.ContainsKey(player.id)) continue;
                var portrait = cache.Get(match, player);
                if (!portrait) continue;
                var snapshot = new RenderTexture(portrait.width, portrait.height, 0) { name = "Head portrait - Hall of Fame" };
                var previous = RenderTexture.active;
                try { Graphics.Blit(portrait, snapshot); }
                finally { RenderTexture.active = previous; }
                latest.portraits.Add(player.id, snapshot);
            }
            if (latest.nextPortrait == latest.standings.Length)
            {
                latest.nextPortrait = 0;
                latest.portraitPass++;
            }
        }

        void Build()
        {
            var root = new GameObject("Last match billboard", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            root.transform.SetLocalPositionAndRotation(MayhemMarina.FameBoard, MayhemMarina.FameFacing);
            root.transform.localScale = Vector3.one * .0045f;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(940, 480);
            var border = CandyResultsHud.Panel("Billboard frame", root.transform, new Vector2(940, 480), Vector2.zero, Mint);
            var panel = CandyResultsHud.Panel("Scoreboard screen", border.transform, new Vector2(922, 462), Vector2.zero, Ink).transform;
            Text Label(string name, float y, int size) => CandyResultsHud.Label(name, panel, new Vector2(890, 42), new Vector2(0, y), size);
            Row MakeRow(string name, float y)
            {
                var text = Label(name, y + 10, 21);
                text.rectTransform.sizeDelta=new Vector2(890,26);
                var stats=Label(name+" stats",y-13,16);stats.rectTransform.sizeDelta=new Vector2(890,20);
                stats.alignment=TextAnchor.MiddleLeft;stats.color=new Color(.76f,.88f,.89f);
                text.alignment = TextAnchor.MiddleLeft;
                var image = new GameObject(name + " headshot", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(panel, false);
                image.rectTransform.sizeDelta = new Vector2(33, 33);
                image.rectTransform.anchoredPosition = new Vector2(0, y);
                image.raycastTarget = false;
                var initial = CandyResultsHud.Label("Initial", image.transform, new Vector2(33, 33), Vector2.zero, 21);
                return new Row { text = text, stats=stats, portrait = image, initial = initial };
            }

            Label("Last match", 205, 25).text = "LAST MATCH";
            title = Label("Winner", 160, 30);
            title.color = Mint;
            details = Label("Mode", 121, 20);
            heading = Label("Standings", 78, 19);
            infectionHeading = Label("Infection standings", 78, 19);
            footer = Label("Page", -207, 17);
            for (int i = 0; i < PageSize; i++)
            {
                left[i] = MakeRow("Standing " + (i + 1), 32 - i * 43);
                right[i] = MakeRow("Infection standing " + (i + 1), 32 - i * 43);
            }
            title.resizeTextForBestFit=true;title.resizeTextMinSize=20;title.resizeTextMaxSize=30;
            DeadlyJenga.CastlePrototype.TerminalUiStyle.Apply(root.transform);
            Draw();
        }

        void OnDestroy()
        {
            if (displayed?.statues) displayed.statues.SetActive(false);
        }

        void Update()
        {
            if (!title) return;
            if (displayed != latest) { page = 0; Draw(); }
            else if (latest != null && Time.unscaledTime >= nextPage)
            {
                page = (page + 1) % Pages;
                Draw();
            }
            else if (latest != null && drawnPortraits != latest.portraits.Count) DrawRows();

            if (displayed?.statues)
            {
                displayed.statues.transform.SetPositionAndRotation(transform.TransformPoint(MayhemMarina.FamePodium),
                    transform.rotation * MayhemMarina.FameFacing);
                if (!displayed.statues.activeSelf) displayed.statues.SetActive(true);
            }
        }

        int Pages => latest == null ? 1 : Mathf.Max(1, (latest.standings.Length + PageSize - 1) / PageSize);

        void Draw()
        {
            displayed = latest;
            nextPage = Time.unscaledTime + 7;
            bool split = latest?.infections != null;
            title.text = latest?.headline ?? "No completed match yet";
            details.text = latest?.mode ?? "";
            heading.text = latest == null ? "" : split ? "LONGEST SURVIVORS" : "FINAL STANDINGS";
            infectionHeading.text = split ? "SURVIVORS INFECTED" : "";
            SetColumn(heading, split ? -225 : 0, split ? 430 : 870);
            SetColumn(infectionHeading, 225, 430);
            DrawRows();
            footer.text = latest==null?"Your last completed match appears here":$"PAGE {page + 1} / {Pages}"+(Pages>1?"  ·  Changes every 7 seconds":"");
        }

        void DrawRows()
        {
            drawnPortraits = latest?.portraits.Count ?? 0;
            bool split = latest?.infections != null;
            for (int i = 0; i < PageSize; i++)
            {
                int index = page * PageSize + i;
                SetRow(left[i], latest?.standings, index, split ? -225 : 0, split ? 430 : 680);
                SetRow(right[i], latest?.infections, index, 225, 430);
            }
        }

        static void SetRow(Row row, Standing[] standings, int index, float x, float width)
        {
            var entry = standings != null && index < standings.Length ? standings[index] : null;
            row.text.text = entry==null?"":$"{index+1}.  {CandyHudContent.Safe(entry.player.name,36)}";
            row.stats.text=entry==null?"":entry.text.Substring(entry.text.IndexOf("·",System.StringComparison.Ordinal)+1).Trim();
            row.portrait.gameObject.SetActive(entry != null);
            float leftEdge = x - width * .5f;
            SetColumn(row.text, x + 23, width - 46);
            SetColumn(row.stats,x+23,width-46);
            var rect = row.portrait.rectTransform;
            rect.anchoredPosition = new Vector2(leftEdge + 17, rect.anchoredPosition.y);
            if (entry == null) { row.portrait.texture = null; return; }
            latest.portraits.TryGetValue(entry.player.id, out var portrait);
            CandyScorePortraits.Show(row.portrait, row.initial, portrait, entry.player);
        }

        static void SetColumn(Text label, float x, float width)
        {
            var rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 15;
            label.resizeTextMaxSize = label.fontSize;
        }
    }
}
