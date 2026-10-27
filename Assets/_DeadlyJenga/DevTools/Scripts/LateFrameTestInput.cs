using System;
using UnityEngine;

namespace DeadlyJenga.DevTools
{
    [DefaultExecutionOrder(410)]
    public sealed class LateFrameTestInput:MonoBehaviour
    {
        public Action Apply;
        public int AppliedFrames {get;private set;}
        void LateUpdate(){AppliedFrames++;Apply?.Invoke();}
    }
}
