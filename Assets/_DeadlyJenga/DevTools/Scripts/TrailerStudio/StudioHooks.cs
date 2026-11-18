#if UNITY_EDITOR
using System;

namespace DeadlyJenga.DevTools.TrailerStudio
{
    // Trailer Studio's code lives in the editor assembly, and Unity does not let editor scripts be
    // components. These hooks are the bridge: two tiny components (StudioDriverEarly/Late) call them
    // from the game loop, and the Studio fills them in. Editor only; none of this is in a player build.
    public static class StudioHooks
    {
        public static Action FixedStep;    // every physics step, before other scripts
        public static Action FrameStart;   // every frame, before other scripts' Update
        public static Action FrameEnd;     // every frame, after other scripts' LateUpdate
        public static Action Gui;          // OnGUI, for the performer's on-screen prompts
    }
}
#endif