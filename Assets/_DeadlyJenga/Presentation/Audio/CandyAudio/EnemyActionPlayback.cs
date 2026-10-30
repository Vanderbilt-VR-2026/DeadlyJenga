using System;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyAudio
{
    // A first snapshot is a baseline, not a request to replay earlier combat.
    public sealed class EnemyActionPlayback
    {
        IMatchRuntime runtime;
        string epoch;
        int sequence;
        public int Consume(IMatchRuntime owner,MatchFrame frame,Action<EnemyActionFrame> play)
        {
            if(owner==null||frame==null||string.IsNullOrEmpty(frame.enemyActionEpoch))return 0;
            if(!ReferenceEquals(runtime,owner)||epoch!=frame.enemyActionEpoch)
            {runtime=owner;epoch=frame.enemyActionEpoch;sequence=frame.enemyActionSequence;return 0;}
            if(frame.enemyActionSequence<=sequence)return 0;
            int count=0;
            foreach(var item in frame.enemyActions??Array.Empty<EnemyActionFrame>())
            {
                if(item==null||item.id<=sequence||item.id>frame.enemyActionSequence||item.remaining<=0)continue;
                play?.Invoke(item);count++;
            }
            sequence=frame.enemyActionSequence;return count;
        }
    }
}
