using System;
using System.Collections.Generic;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    // Developer tab: type part of a tool's name, then pick it from the results.
    public sealed partial class CastleWorldMenu
    {
        readonly List<(string label, Action action, Func<bool> allowed, bool opensDetails, Func<string> title)> developerTiles = new();
        string toolSearch = "";
        Text toolSearchQuery, toolSearchPreview;

        void OpenToolSearch()
        {
            OpenPlayerTabletPage(TabletPage.ToolSearch);
        }

        void BuildTabletToolSearch(RectTransform page)
        {
            TabletText(page, "SEARCH DEVELOPER TOOLS", new(0, 225), new(930, 56), 38);
            toolSearchQuery = TabletText(page, "", new(0, 165), new(930, 56), 34);
            toolSearchPreview = TabletText(page, "", new(0, 108), new(950, 48), 20);
            const string keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i].ToString();
                TabletControl(tabletActions, page, () => key, new(-420 + i % 10 * 94, 41 - i / 10 * 58), new(84, 50),
                    () => { if (toolSearch.Length < 24) toolSearch += key.ToLowerInvariant(); });
            }
            TabletControl(tabletActions, page, () => "Space", new(50, -133), new(175, 50), () => { if (toolSearch.Length < 24) toolSearch += " "; });
            TabletControl(tabletActions, page, () => "Delete", new(235, -133), new(175, 50),
                () => { if (toolSearch.Length > 0) toolSearch = toolSearch.Substring(0, toolSearch.Length - 1); });
            TabletControl(tabletActions, page, () => "Clear", new(420, -133), new(175, 50), () => toolSearch = "");
            TabletControl(tabletActions, page, () => "Back", new(-244, -213), new(456, 62), () => OpenPlayerTabletPage(TabletPage.Developer));
            TabletControl(tabletActions, page, () => "Show results", new(244, -213), new(456, 62), ShowToolSearchResults,
                allowed: () => DeveloperTabAvailable);
        }

        void ShowToolSearchResults()
        {
            OpenPlayerTabletPage(TabletPage.Developer);
            ShowDeveloperDetails(() => OpenDeveloperRoute("Search"));
        }

        void RefreshToolSearch()
        {
            if (tabletPage != TabletPage.ToolSearch || !toolSearchQuery) return;
            toolSearchQuery.text = (toolSearch.Length > 0 ? toolSearch : "type a tool name") + "  |";
            var matches = new List<string>();
            foreach (var tile in developerTiles)
            {
                string name = tile.title?.Invoke() ?? tile.label;
                if (Matches(name)) matches.Add(name);
            }
            toolSearchPreview.text = matches.Count > 0 ? "Quick tools: " + string.Join(", ", matches) : "Show results also searches every Bots and More tools option.";
        }

        bool Matches(string name) =>
            toolSearch.Trim().Length == 0 || name.IndexOf(toolSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        // Keeps only rows whose label contains the query, and adds the matching quick tools first.
        void FilterToolSearch(List<string> names, List<Action> actions, List<bool> enabled, List<CandyHudIcon> art,
            List<string> captions, List<Texture> pictures)
        {
            var keep = new List<int>();
            for (int i = 0; i < names.Count; i++) if (Matches(names[i])) keep.Add(i);
            var n = new List<string>(); var a = new List<Action>(); var e = new List<bool>();
            var r = new List<CandyHudIcon>(); var c = new List<string>(); var p = new List<Texture>();
            n.Add("Search: " + (toolSearch.Length > 0 ? toolSearch : "(everything)") + "  — edit");
            a.Add(OpenToolSearch); e.Add(true); r.Add(CandyHudIcon.Help); c.Add(""); p.Add(null);
            foreach (var tile in developerTiles)
            {
                string name = tile.title?.Invoke() ?? tile.label;
                if (tile.label == "Search tools" || !Matches(name)) continue;
                var run = tile.action; bool details = tile.opensDetails;
                n.Add(name); a.Add(() => { if (details) { ResetPages(); run(); } else run(); });
                e.Add(tile.allowed?.Invoke() != false); r.Add(CandyHudIcon.Settings); c.Add(""); p.Add(null);
            }
            foreach (int i in keep) { n.Add(names[i]); a.Add(actions[i]); e.Add(enabled[i]); r.Add(art[i]); c.Add(captions[i]); p.Add(pictures[i]); }
            names.Clear(); names.AddRange(n); actions.Clear(); actions.AddRange(a); enabled.Clear(); enabled.AddRange(e);
            art.Clear(); art.AddRange(r); captions.Clear(); captions.AddRange(c); pictures.Clear(); pictures.AddRange(p);
            heading.text = "DEVELOPER / SEARCH";
            help.text = names.Count > 1 ? "Pick a tool. Select the first row to change the search." : "No tools match. Select the first row to search again.";
        }
    }
}
