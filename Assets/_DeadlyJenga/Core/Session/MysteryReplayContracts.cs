using System;

namespace DeadlyJenga.Core
{
    [Serializable] public sealed class MysteryReplayCue
    {
        public int sequence,round;
        public string murderer="",sheriff="",victim="";
        public bool roundEnd;
        public float elapsed;
    }
    public interface IMysteryReplaySource { MysteryReplayCue ReplayCue {get;} }
}
