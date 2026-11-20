using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    public enum StudioRole { Survivor = 0, Hand = 1, Bot = 2 }

    // A spawn point for one actor. Positions are in metres from the stage anchor.
    [Serializable] public sealed class StudioMark
    {
        public string id = "";            // also the actor's track id on the tape
        public string actor = "Actor";    // the name shown in the Studio
        public StudioRole role = StudioRole.Survivor;
        public int color;                 // index into the avatar colors, and the Studio's color for this actor
        public Vector3 position;
        public float yaw;                 // facing, degrees
        public bool customLook;           // false = the bot's usual random outfit
        public AvatarAppearance appearance;
        public string equipment = "";     // an EquipmentKind name, or empty
    }

    // Something the world does at a time: a collapse, a disaster...
    [Serializable] public sealed class StudioCue
    {
        public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        public float at;
        public string action = "collapse";
        public string actor = "", target = "", hazard = "", weapon = "";
        public float strength = 9, heading, height;
        public Vector3 point;             // metres from the stage anchor, for cues that are aimed
        public bool optional, lethal;
        // Phase 3: directing the world.
        public List<int> blocks = new List<int>();   // which blocks an aimed cue acts on (index in the map's block list)
        public Vector3 direction = Vector3.right;    // which way a push goes
        public float spin;                // turns per second given to pushed blocks
        public float duration;            // how long a disaster, guide or slow-time range lasts (0 = until it ends by itself)
        public float ripple;              // seconds of delay per metre from the first block, for a wave of pushes
        public string link = "";          // id of the cue that sets this one off (empty = it runs at its own time)
        public bool linkOnHit;            // true = runs when the linked cue's first block hits something
        public float radius = 3;          // reach of a guide, in metres
        public float[] values = new float[0];   // a disaster's own settings, in the order the disaster lists them
        public float jitter;              // set by variation runs: this cue's strength is multiplied by (1 + jitter)
        public bool aimed;                // a disaster that arrives from a side comes from 'heading' instead of a random side
        // Directed hand pull (action "handPull"): 'actor' = hand mark, blocks[0] = block, 'at' = the yank,
        // 'duration' = flight time before the grab, 'point' = where the flight starts, 'height' = arc of the flight,
        // 'direction' = which way the block is pulled, 'strength' = how far it slides out.
        public float pullTime = .9f;      // seconds the yank takes
        public float grip = .45f;         // seconds the fingers take to close before the yank
        public Vector3 carry = new Vector3(0, 5, 0);   // where the hand takes the block after the yank (metres, over 1.5 s)
    }

    // A camera shot. Phase 1 only keeps these (imported from Theatre); Phase 5 edits them.
    [Serializable] public sealed class StudioShot
    {
        public string name = "Shot", subject = "", second = "";
        public float start, duration = 2, fov = 52, roll;
        public bool avoidWalls, autoFocus, firstPerson;
        public Vector3 from = new Vector3(6, 3, -8), to = new Vector3(6, 3, -8), look;
    }

    // A saved scene setup: map, time, marks, cues. Saved as JSON by StudioLibrary.
    [Serializable] public sealed class StudioSet
    {
        public string id = "", title = "Untitled set", notes = "";
        public string map = "castle";
        public int fps = 30, seed = 148;
        public float length = 10, warmup = .75f;
        public bool useMapAnchor;         // false = the stage sits on the highest block near the middle of the map
        public Vector3 mapAnchor, stageOffset;
        public float timeOfDay = -1;      // hours, -1 = whatever the map uses
        public string weather = "";       // empty = whatever the map uses
        public string startFromSet = "";  // empty = start as the map loads
        public int startFromFrame;
        public string thumbnail = "";
        public bool handBot;              // true = hand marks are flown by the game's hand bot instead of performed
        public List<StudioMark> marks = new List<StudioMark>();
        public List<StudioCue> cues = new List<StudioCue>();
        public List<StudioShot> shots = new List<StudioShot>();
        public List<string> shotNotes = new List<string>();
        // Phase 5: cameras and the cut.
        public List<StudioCameraTrack> cameras = new List<StudioCameraTrack>();
        public List<StudioCut> cuts = new List<StudioCut>();
        public StudioRenderSettings render = new StudioRenderSettings();
        public string music = "";         // path of a music bed (wav or mp3), or empty
        public float bpm = 120, beatOffset; // beat markers for snapping cuts
        // Phase 4: overrides, reactions and extras layered on recorded takes.
        public List<StudioOverride> overrides = new List<StudioOverride>();

        public int Frames => Mathf.Max(1, Mathf.RoundToInt(length * fps));
        public StudioMark Mark(string markId) => marks.FirstOrDefault(m => m.id == markId);

        public StudioMark AddMark(StudioRole role, Vector3 position)
        {
            string label = role == StudioRole.Hand ? "Hand" : role == StudioRole.Bot ? "Bot" : "Survivor";
            int number = marks.Count(m => m.role == role) + 1;
            var mark = new StudioMark { role = role, position = position, color = marks.Count % 8, actor = label + " " + number };
            mark.id = UniqueId(label.ToLowerInvariant() + "-" + number);
            marks.Add(mark);
            return mark;
        }

        // Every cue needs its own id (links and variation recipes find cues by it). Sets saved before
        // cues had ids, or with a copied cue, get new ones here. Returns how many ids were added.
        public int EnsureCueIds()
        {
            int added = 0;
            var seen = new HashSet<string>();
            foreach (var cue in cues)
            {
                if (string.IsNullOrEmpty(cue.id) || !seen.Add(cue.id))
                {
                    cue.id = Guid.NewGuid().ToString("N").Substring(0, 8);
                    seen.Add(cue.id);
                    added++;
                }
            }
            return added;
        }

        public string UniqueId(string wanted)
        {
            string id = wanted;
            for (int n = 2; marks.Any(m => m.id == id); n++) id = wanted + "-" + n;
            return id;
        }

        // Plain-language problems, shown in the Set mode before a run.
        public List<string> Problems()
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(map)) problems.Add("Pick a map.");
            if (fps < 12 || fps > 60) problems.Add("Frame rate must be 12 to 60.");
            if (length < .5f || length > 120) problems.Add("Length must be half a second to two minutes.");
            if (marks.Select(m => m.id).Distinct().Count() != marks.Count) problems.Add("Two marks share an id.");
            foreach (var cue in cues) if (cue.at < 0 || cue.at >= length) problems.Add($"Cue \"{cue.action}\" is outside the set's length.");
            return problems;
        }

        public StudioSet Clone() => JsonUtility.FromJson<StudioSet>(JsonUtility.ToJson(this));
    }

    // One color per actor, used everywhere the actor appears (list, timeline, marks, paths).
    public static class StudioColors
    {
        static readonly Color[] Palette =
        {
            new Color(1f, .42f, .47f), new Color(.34f, .72f, 1f), new Color(.45f, .85f, .52f), new Color(1f, .78f, .32f),
            new Color(.72f, .55f, 1f), new Color(1f, .58f, .82f), new Color(.38f, .86f, .84f), new Color(.95f, .62f, .38f)
        };
        public static Color For(int index) => Palette[((index % Palette.Length) + Palette.Length) % Palette.Length];
        public static readonly Color World = new Color(.62f, .66f, .74f), Hazard = new Color(.98f, .55f, .30f),
            Effects = new Color(.80f, .72f, .98f), Audio = new Color(.55f, .82f, .70f), Camera = new Color(.95f, .92f, .62f);
    }
}
