using System.Collections.Generic;
using DeadlyJenga.Core;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime:IMysteryReplaySource
    {
        readonly Dictionary<string,MysteryReplayCue> victimReplays=new Dictionary<string,MysteryReplayCue>();
        MysteryReplayCue finalReplay;int replaySequence;string sheriffFinisher="";
        public MysteryReplayCue ReplayCue
        {
            get
            {
                if(!Active)return null;
                var value=Authority?finalReplay??(victimReplays.TryGetValue(LocalActorId,out var cue)?cue:null):frame.mysteryReplay?.sequence>0?frame.mysteryReplay:personal.replay;
                return value?.sequence>0?value:null;
            }
        }
        void ResetMysteryReplay(){victimReplays.Clear();finalReplay=null;sheriffFinisher="";}
        void RecordMysteryReplay(string attacker,string victim,bool murdered)
        {
            if(attacker==victim||string.IsNullOrEmpty(attacker))return;
            if(murdered&&victimReplays.TryGetValue(victim,out var previous)&&previous.elapsed==simulation.Elapsed)return;
            if(murdered)victimReplays[victim]=new MysteryReplayCue{sequence=++replaySequence,round=mysteryRound,murderer=attacker,victim=victim,elapsed=simulation.Elapsed};
            else if(mystery.Role(attacker)==MysteryRole.Sheriff&&mystery.Role(victim)==MysteryRole.Murderer)sheriffFinisher=attacker;
        }
        void FinishMysteryReplay()
        {
            if(!MysteryActive)return;
            string murderer="";foreach(var p in simulation.Participants)if(mystery.Role(p.frame.id)==MysteryRole.Murderer){murderer=p.frame.id;break;}
            if(murderer.Length>0)finalReplay=new MysteryReplayCue{sequence=++replaySequence,round=mysteryRound,murderer=murderer,sheriff=sheriffFinisher,roundEnd=true,elapsed=simulation.Elapsed};
        }
        MysteryPrivateFrame ReplaySecret(string id)
        {
            var value=MysteryActive?mystery.Personal(id):new MysteryPrivateFrame();
            value.replay=victimReplays.TryGetValue(id,out var cue)?cue:null;return value;
        }
    }
}
