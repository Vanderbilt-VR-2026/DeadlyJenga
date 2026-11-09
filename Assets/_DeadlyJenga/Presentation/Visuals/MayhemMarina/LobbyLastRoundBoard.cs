using System.Linq;
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

        sealed class Result
        {
            public string headline, mode;
            public string[] standings, infections;
        }
        static Result latest;
        static IMatchRuntime observed;
        static RoundPhase observedPhase;
        Result displayed;
        Text title, details, heading, infectionHeading, footer;
        readonly Text[] left = new Text[PageSize], right = new Text[PageSize];
        int page;
        float nextPage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        { latest = null; observed = null; }

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
            { observed = null; return; }
            if (ReferenceEquals(match, observed) && frame.phase == observedPhase) return;

            var mode = frame.rules.mode;
            var players = CandyMatchHud.StandingsParticipants(frame, session as IGameExperienceControls, out _);
            if (!players.Any()) return;
            bool split = mode == GameModeKind.Infection;
            string[] Rows(bool infections) => CandyResultsHud.Rank(players, split, infections)
                .Select((player, rank) => $"{rank + 1}.  {CandyHudContent.Safe(player.name)}   ·   {CandyResultsHud.RankStats(player, mode, infections)}")
                .ToArray();
            latest = new Result
            {
                headline = string.IsNullOrEmpty(frame.winner) ? "Round complete"
                    : CandyHudContent.Safe(frame.winner) + (frame.winner == "Draw" ? "" : " wins"),
                mode = CandyHudContent.Mode(mode) + "  ·  " + CandyHudContent.Safe(frame.metricLabel),
                standings = Rows(false),
                infections = split ? Rows(true) : null
            };
            observed = match;
            observedPhase = frame.phase;
        }

        void Build()
        {
            var root = new GameObject("Last match billboard", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(13.65f, 3.05f, 8.72f);
            root.transform.localScale = Vector3.one * .0045f;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(940, 430);
            var border = CandyResultsHud.Panel("Billboard frame", root.transform, new Vector2(940, 430), Vector2.zero, Mint);
            var panel = CandyResultsHud.Panel("Scoreboard screen", border.transform, new Vector2(922, 412), Vector2.zero, Ink).transform;
            Text Label(string name, float y, int size) => CandyResultsHud.Label(name, panel, new Vector2(890, 42), new Vector2(0, y), size);
            Label("Last match", 177, 29).text = "LAST MATCH";
            title = Label("Winner", 133, 32);
            title.color = Mint;
            details = Label("Mode", 96, 21);
            heading = Label("Standings", 59, 20);
            infectionHeading = Label("Infection standings", 59, 20);
            footer = Label("Page", -180, 18);
            for (int i = 0; i < PageSize; i++)
            {
                left[i] = Label("Standing " + (i + 1), 19 - i * 37, 23);
                right[i] = Label("Infection standing " + (i + 1), 19 - i * 37, 23);
            }
            Draw();
        }

        void Update()
        {
            if (!title) return;
            if (displayed != latest) { page = 0; Draw(); }
            else if (latest != null && Time.unscaledTime >= nextPage)
            { page = (page + 1) % Pages; Draw(); }
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
            for (int i = 0; i < PageSize; i++)
            {
                int index = page * PageSize + i;
                left[i].text = latest != null && index < latest.standings.Length ? latest.standings[index] : "";
                right[i].text = split && index < latest.infections.Length ? latest.infections[index] : "";
                SetColumn(left[i], split ? -225 : 0, split ? 430 : 870);
                SetColumn(right[i], 225, 430);
            }
            footer.text = Pages > 1 ? $"{page + 1} / {Pages}" : "";
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
