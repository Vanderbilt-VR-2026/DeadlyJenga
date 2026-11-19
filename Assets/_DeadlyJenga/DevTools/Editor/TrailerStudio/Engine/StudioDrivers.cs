using DeadlyJenga.DevTools.TrailerStudio;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Connects the Studio to the game loop. The two driver components live in the DevTools runtime
    // assembly (Unity cannot attach editor scripts to objects); they call back into the Studio here.
    public static class StudioDrivers
    {
        static GameObject host;
        static StudioWristPanel wrist;

        public static bool Alive => host;

        public static void Ensure()
        {
            StudioHooks.FixedStep = StudioRun.FixedStep;
            StudioHooks.FrameStart = () => { StudioRun.FrameStart(); StudioHud.Update(); };
            StudioHooks.FrameEnd = () => { StudioRun.FrameEnd(); (wrist ??= new StudioWristPanel()).LateUpdate(); };
            StudioHooks.Gui = StudioHud.OnGUI;
            if (host) return;
            host = new GameObject("Trailer Studio driver");
            Object.DontDestroyOnLoad(host);   // map rebuilds and scene changes must not take the Studio's clock away
            host.AddComponent<StudioDriverEarly>();
            host.AddComponent<StudioDriverLate>();
        }

        public static StudioWristPanel Wrist => wrist ??= new StudioWristPanel();

        public static void Destroy()
        {
            StudioHooks.FixedStep = StudioHooks.FrameStart = StudioHooks.FrameEnd = StudioHooks.Gui = null;
            wrist?.Dispose();
            wrist = null;
            if (host) Object.Destroy(host);
            host = null;
        }
    }
}
