using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    internal sealed class CandyEliminationFeed
    {
        const int Capacity = 4;
        const int PortraitSize = 76;
        const float Lifetime = 5, Fade = 1.2f;
        struct Notice
        {
            public MatchPlayerFrame victim, killer;
            public float expires;
        }

        sealed class Row
        {
            public CanvasGroup group;
            public MatchPlayerFrame shownVictim, shownKiller;
            public Text message, victimInitial, killerInitial;
            public RawImage victimPortrait, killerPortrait;
        }

        readonly Dictionary<string, MatchPlayerFrame> roster = new Dictionary<string, MatchPlayerFrame>();
        readonly List<Notice> notices = new List<Notice>(Capacity);
        readonly Row[] rows = new Row[Capacity];
        readonly CandyScorePortraits portraits;
        IMatchRuntime owner;
        GameModeKind mode;
        int sequence;
        string epoch;
        MatchFrame observed;

        public CandyEliminationFeed(RectTransform parent, CandyScorePortraits portraits)
        {
            this.portraits = portraits;
            for (int i = 0; i < Capacity; i++)
            {
                var text = CandyHudCards.Create(parent, "Elimination feed", new Vector2(-414, -65 - i * 86),
                    new Vector2(412, 82), 22, TextAnchor.MiddleLeft);
                var panel = text.transform.parent.gameObject;
                var group = panel.AddComponent<CanvasGroup>();
                group.blocksRaycasts = group.interactable = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.resizeTextForBestFit = false;
                text.supportRichText = false;
                var victim = Portrait(panel.transform, text.font, -161, out var victimInitial);
                var killer = Portrait(panel.transform, text.font, -161, out var killerInitial);
                rows[i] = new Row
                {
                    group = group, message = text, victimPortrait = victim, victimInitial = victimInitial,
                    killerPortrait = killer, killerInitial = killerInitial
                };
                panel.SetActive(false);
            }
        }

        static RawImage Portrait(Transform parent, Font font, float x, out Text initial)
        {
            var icon = new GameObject("Avatar portrait", typeof(RectTransform), typeof(RawImage));
            icon.transform.SetParent(parent, false);
            var portrait = icon.GetComponent<RawImage>();
            portrait.raycastTarget = false;
            portrait.rectTransform.sizeDelta = Vector2.one * PortraitSize;
            portrait.rectTransform.anchoredPosition = new Vector2(x, 0);
            var letter = new GameObject("Initial", typeof(RectTransform), typeof(Text));
            letter.transform.SetParent(icon.transform, false);
            initial = letter.GetComponent<Text>();
            initial.font = font;
            initial.fontStyle = FontStyle.Bold;
            initial.alignment = TextAnchor.MiddleCenter;
            initial.color = Color.white;
            initial.fontSize = 34;
            initial.raycastTarget = false;
            initial.rectTransform.sizeDelta = Vector2.one * PortraitSize;
            return portrait;
        }

        public void Update(IMatchRuntime match, bool visible)
        {
            var frame = match?.Frame;
            if (frame == null || frame.rules.mode == GameModeKind.MurderMystery)
            {
                owner = null;
                sequence = 0;
                epoch = null;
                roster.Clear();
                observed = null;
                notices.Clear();
                visible = false;
            }
            else
            {
                bool announce = owner == match && mode == frame.rules.mode;
                if (!announce)
                {
                    owner = match;
                    mode = frame.rules.mode;
                    sequence = 0;
                    epoch = null;
                    notices.Clear();
                    roster.Clear();
                }
                if (epoch != frame.eliminationEpoch)
                {
                    epoch = frame.eliminationEpoch;
                    sequence = 0;
                    notices.Clear();
                }
                bool active = frame.phase == RoundPhase.Playing || frame.phase == RoundPhase.Victory;
                visible &= active;
                if (!active) notices.Clear();
                if (observed != frame || !announce)
                {
                    Observe(match, frame, announce && active);
                    roster.Clear();
                    foreach (var player in frame.players) roster[player.id] = player;
                    observed = frame;
                }
            }

            float now = Time.unscaledTime;
            notices.RemoveAll(item => item.expires <= now);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                bool show = visible && i < notices.Count;
                row.group.gameObject.SetActive(show);
                if (!show) continue;
                var notice = notices[notices.Count - 1 - i];
                bool paired = notice.killer != null;
                row.group.alpha = Mathf.Clamp01((notice.expires - now) / Fade);
                row.victimPortrait.rectTransform.anchoredPosition = new Vector2(paired ? -77 : -161, 0);
                row.message.rectTransform.offsetMin = new Vector2(paired ? 176 : 92, 6);
                row.message.rectTransform.offsetMax = new Vector2(-12, -6);
                if (row.shownVictim != notice.victim || row.shownKiller != notice.killer)
                {
                    row.shownVictim = notice.victim;
                    row.shownKiller = notice.killer;
                    portraits.SetName(row.message, paired ? Name(notice.killer, match.LocalActorId) : Name(notice.victim, match.LocalActorId));
                    string first = row.message.text;
                    if (paired)
                    {
                        portraits.SetName(row.message, "› " + Name(notice.victim, match.LocalActorId));
                        row.message.text = first + "\n" + row.message.text;
                    }
                    else row.message.text = first + "\nwas eliminated";
                }
                row.killerPortrait.gameObject.SetActive(paired);
                if (paired)
                    CandyScorePortraits.Show(row.killerPortrait, row.killerInitial, portraits.Get(match, notice.killer), notice.killer);
                CandyScorePortraits.Show(row.victimPortrait, row.victimInitial, portraits.Get(match, notice.victim), notice.victim);
            }
        }

        static string Name(MatchPlayerFrame player, string local) =>
            player.id == local ? "YOU" : CandyHudContent.Safe(player.name, 64);

        void Observe(IMatchRuntime match, MatchFrame frame, bool announce)
        {
            if (frame.eliminations == null || frame.eliminations.Length == 0)
            {
                return;
            }

            foreach (var entry in frame.eliminations)
            {
                if (entry.sequence <= sequence) continue;
                sequence = entry.sequence;
                if (!announce) continue;
                var victim = Find(match, frame, entry.victim, "Player");
                var killer = string.IsNullOrEmpty(entry.killer) ? null : Find(match, frame, entry.killer, "Attacker");
                if (notices.Count == Capacity) notices.RemoveAt(0);
                notices.Add(new Notice { victim = victim, killer = killer, expires = Time.unscaledTime + Lifetime });
            }
        }

        MatchPlayerFrame Find(IMatchRuntime match, MatchFrame frame, string id, string fallback)
        {
            var player = System.Array.Find(frame.players, item => item.id == id);
            if (player != null) return player;
            if (roster.TryGetValue(id, out player)) return player;
            var actor = match.FindActor(id);
            return new MatchPlayerFrame { id = id, name = actor?.DisplayName ?? fallback, color = actor?.Color ?? Color.white };
        }
    }
}
