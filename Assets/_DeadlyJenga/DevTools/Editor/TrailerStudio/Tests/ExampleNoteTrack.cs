using System.Text;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // EXAMPLE: a whole track type in one new file. Nothing else in the Studio was edited to add it.
    // It records one line of text per frame (the frame number and how many blocks are awake).
    // Copy this file as the starting point for a real track type, and see progress/EXTENSION-POINTS.md.
    public sealed class ExampleNoteTrack : IStudioTrackType
    {
        public const string Id = "example-note";
        public string TypeId => Id;
        public string Label => "Examples";
        public Color Color => new Color(.6f, .6f, .6f);
        public int Order => 90;

        public static string LastShown = "";   // what playback showed last (the acceptance test reads this)

        public void EnsureTracks(StudioSet set, StudioTape tape) => tape.GetOrAdd("example-note", Id, "Example notes");
        public StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track) => new Recorder(stage);
        public StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take) => new Player(take);

        sealed class Recorder : StudioRecorder
        {
            readonly StudioStage stage;
            public Recorder(StudioStage stage) { this.stage = stage; }

            public override byte[] Capture(int frame, bool key)
            {
                int awake = 0;
                foreach (var block in stage.blocks) if (block && !block.IsSleeping()) awake++;
                return Encoding.UTF8.GetBytes($"frame {frame}: {awake} blocks awake");
            }
        }

        sealed class Player : StudioPlayer
        {
            readonly StudioTake take;
            public Player(StudioTake take) { this.take = take; }

            public override void Show(int frame, bool running)
            {
                if (take.Covers(frame)) LastShown = Encoding.UTF8.GetString(take.frames[frame - take.start]);
            }
        }
    }
}
