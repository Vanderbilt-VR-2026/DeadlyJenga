using System;
using System.Collections;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static class PlayerReactionChecks
    {
        public static IEnumerator Run(CastlePlaySession session,Action<bool,string> check)
        {
            foreach(var sound in CandyPlayerSounds.Sounds)
            {
                var cue=CandyPlayerSounds.DefaultCue(sound);bool valid=cue.variants.Length==4;
                foreach(var clip in cue.variants)valid&=clip&&clip.samples>2000&&clip.channels==1&&clip.frequency==32000;
                check(valid,sound+" has four imported original mono32k clips");
            }
            check(CandyPlayerSounds.DefaultCue(CandySound.SuitThruster).loop,"Suit thrust uses a pooled loop rather than repeated one-shots");
            var go=new GameObject("Player reaction snapshot fixture");var observer=go.AddComponent<CandyPlayerReactions>();observer.enabled=false;
            var player=session.Survivor();var actor=player.GetComponent<IModeActor>();var runtime=session.Modes;
            var frame=new MatchFrame{players=new[]{new MatchPlayerFrame{id=actor.ActorId,role="survivor",alive=true,health=.7f,position=player.transform.position}}};
            try
            {
                observer.Observe(runtime,frame);check(observer.PainCues==0&&observer.DeathCues==0,"First already-hurt snapshot is silent on join");
                frame.players[0].health=.5f;observer.Observe(runtime,frame);
                check(observer.PainCues==1,"Remote health decrease plays one spatial ow");
                observer.Observe(runtime,frame);check(observer.PainCues==1,"Repeated remote health snapshot is silent");
                frame.players[0].dying=true;observer.Observe(runtime,frame);observer.Observe(runtime,frame);
                check(observer.DeathCues==1,"Dying transition plays one death cue and duplicate snapshot is silent");
                yield return new WaitForSeconds(.8f);
                frame.players[0].alive=true;frame.players[0].dying=false;frame.players[0].health=1;observer.Observe(runtime,frame);
                frame.players[0].health=.6f;observer.Observe(runtime,frame);
                check(observer.PainCues==2,"Respawn health recovery rearms future damage reactions");
                frame.players[0].alive=false;observer.Observe(runtime,frame);
                check(observer.DeathCues==2,"Next life can produce its own death cue");
                observer.Observe(null,new MatchFrame{players=new[]{new MatchPlayerFrame{id="late",alive=false,dying=true}}});
                check(observer.DeathCues==2,"New runtime and already dead participant do not replay elimination");
            }
            finally{UnityEngine.Object.Destroy(go);}
        }
    }
}
