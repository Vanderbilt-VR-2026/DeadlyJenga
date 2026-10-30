using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastleBots
{
    public sealed partial class CastleBotEmotes
    {
        float contextUntil,nextWeaponLook;Emote pendingContext;int streak;
        static Emote PoseFamily(Emote e)
        {
            switch(e)
            {
                case Emote.BackOff:case Emote.YouScareMe:case Emote.StayAway:case Emote.HandsUp:case Emote.PleaseNo:case Emote.SawTheWeapon:case Emote.RunForIt:case Emote.HelpMe:case Emote.Shaking:case Emote.GoAway:return Emote.NopeNopeNope;
                case Emote.ThatWasClose:case Emote.BarelyEscaped:case Emote.DeepBreath:case Emote.SafeAtLast:case Emote.Exhausted:case Emote.NeedHelp:case Emote.Grieving:return Emote.Phew;
                case Emote.DontTrustYou:case Emote.WatchingYou:case Emote.WhoWasThat:case Emote.Confused:case Emote.Thinking:case Emote.Investigating:case Emote.Suspicious:case Emote.NotMe:case Emote.Oops:case Emote.MyBad:case Emote.FacePalm:case Emote.Disappointed:return Emote.WhatWasThat;
                case Emote.ReadyToFight:case Emote.BringItOn:case Emote.OnFire:case Emote.Unstoppable:case Emote.Angry:case Emote.Frustrated:case Emote.Avenging:case Emote.VictoryPose:return Emote.TotallyMeantThat;
                case Emote.NiceShot:case Emote.Respect:case Emote.ThankYou:case Emote.GoodGame:case Emote.ThumbsUp:case Emote.ThumbsDown:case Emote.Bow:case Emote.HighFive:case Emote.HeartHands:return Emote.GolfClap;
                case Emote.Laughing:case Emote.Cheering:case Emote.JumpForJoy:case Emote.AirGuitar:case Emote.RobotDance:case Emote.DiscoPoint:return Emote.TinyVictoryDance;
                case Emote.Stretch:return Emote.JustChilling;
                case Emote.OverThere:case Emote.HideHere:case Emote.FollowMe:case Emote.StayTogether:case Emote.TrustMe:case Emote.FoundSomething:case Emote.BodyFound:case Emote.Salute:case Emote.ComeHere:return Emote.FriendlyWave;
                default:return e;
            }
        }
        void ExpandedPose(float t,ref Vector3 l,ref Vector3 r,ref Vector3 lr,ref Vector3 rr,ref float lc,ref float rc,ref float pitch,ref float roll)
        {
            switch(current)
            {
                case Emote.Salute:r=new Vector3(.17f,1.56f,.14f);rr=new Vector3(0,0,80);rc=0;break;
                case Emote.Bow:pitch=30;l=new Vector3(-.11f,.84f,.25f);r=new Vector3(.11f,.84f,.25f);break;
                case Emote.FacePalm:r=new Vector3(0,1.55f,.23f);rr=new Vector3(-80,0,0);pitch=15;break;
                case Emote.Thinking:case Emote.Investigating:r=new Vector3(.03f,1.31f,.26f);rr=new Vector3(-45,0,0);pitch=10;break;
                case Emote.HeartHands:l=new Vector3(-.045f,1.15f,.3f);r=new Vector3(.045f,1.15f,.3f);lr=new Vector3(-40,40,-65);rr=new Vector3(-40,-40,65);break;
                case Emote.HighFive:r=new Vector3(.22f,1.80f,.30f);rr=new Vector3(-85,0,0);rc=0;break;
                case Emote.ThumbsUp:case Emote.ThumbsDown:r=new Vector3(.20f,1.28f,.40f);rr=new Vector3(0,0,current==Emote.ThumbsUp?90:-90);rc=.8f;break;
                case Emote.RobotDance:l=new Vector3(-.35f,1.2f,.1f);r=new Vector3(.35f,1.1f,.1f);lr=new Vector3((int)(t*4)%2==0?0:90,0,0);rr=-lr;roll=0;break;
                case Emote.DiscoPoint:r=new Vector3(.4f,1.65f+.08f*Mathf.Sin(t*6),.1f);l=new Vector3(-.18f,.8f,.1f);roll=Mathf.Sin(t*6)*5;break;
                case Emote.AirGuitar:l=new Vector3(-.32f,1.06f,.25f);r=new Vector3(.12f,.95f+.10f*Mathf.Sin(t*15),.32f);pitch=-8;break;
                case Emote.Laughing:l=new Vector3(-.12f,.98f,.19f);r=new Vector3(.12f,.98f,.19f);pitch=-12+Mathf.Sin(t*15)*3;break;
                case Emote.Grieving:pitch=28;l=new Vector3(-.12f,1.4f,.22f);r=new Vector3(.12f,1.4f,.22f);break;
                case Emote.NotMe:l=new Vector3(-.42f,1.26f,.2f);r=new Vector3(.42f,1.26f,.2f);roll=Mathf.Sin(t*7)*8;lc=rc=0;break;
            }
        }
        Emote Pick(params Emote[] values)=>values[random.Next(values.Length)];
        void QueueReaction(Emote e){pendingContext=e;contextUntil=Time.time+4;}
        void ReactDamage(GameObject who,float amount){if(who==gameObject&&amount>0&&Chance(.65f))QueueReaction(Pick(Emote.ThatWasClose,Emote.BarelyEscaped,Emote.BackOff,Emote.Angry,Emote.Frustrated,Emote.HelpMe,Emote.Exhausted,Emote.NeedHelp,Emote.GoAway,Emote.Oops,Emote.FacePalm));}
        void ReactElimination(GameObject who,GameObject source)
        {
            if(who==gameObject){streak=0;return;}
            if(source==gameObject){streak++;if(Chance(.7f))QueueReaction(streak>=3?Pick(Emote.Unstoppable,Emote.OnFire,Emote.VictoryPose,Emote.TinyVictoryDance):Pick(Emote.NiceShot,Emote.ReadyToFight,Emote.BringItOn,Emote.Cheering,Emote.TotallyMeantThat));return;}
            if(who&&(who.transform.position-transform.position).sqrMagnitude<144&&CanObserve(who.transform.position)&&Chance(.45f))QueueReaction(Pick(Emote.RunForIt,Emote.BodyFound,Emote.Grieving,Emote.HideHere,Emote.StayTogether,Emote.Investigating,Emote.WhoWasThat,Emote.Avenging,Emote.Disappointed));
        }
        bool CanObserve(Vector3 point)
        {
            var eye=transform.position+Vector3.up*1.4f;var delta=point-eye;
            return delta.sqrMagnitude<144&&Vector3.Dot(transform.forward,delta.normalized)>-.1f&&!Physics.Linecast(eye,point+Vector3.up*.8f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore);
        }
        bool TryContextReaction()
        {
            if(Time.time<contextUntil){contextUntil=0;Attempt(pendingContext,"Recent observed event");if(active)return true;}
            if(Time.time<nextWeaponLook)return false;nextWeaponLook=Time.time+Range(.8f,1.4f);
            var frame=MatchRuntime.Current?.Frame;if(frame==null)return false;
            int inspected=0;
            foreach(var equipment in frame.equipment)
            {
                if(++inspected>32)break;if(!equipment.drawn||equipment.kind!=EquipmentKind.MurderSword)continue;
                foreach(var player in frame.players)if(player.id==equipment.actor&&player.alive&&CanObserve(player.position))
                {if((player.position-transform.position).sqrMagnitude<.3f)return false;if(Chance(.45f)){Attempt(Pick(Emote.SawTheWeapon,Emote.YouScareMe,Emote.PleaseNo,Emote.HandsUp,Emote.StayAway,Emote.DontTrustYou,Emote.BackOff,Emote.RunForIt,Emote.GoAway),"Visible drawn sword");return active;}break;}
            }
            if(frame.rules.mode==GameModeKind.MurderMystery)
            {
                int people=0;
                foreach(var player in frame.players)
                {
                    if(++people>32)break;
                    var delta=transform.position+Vector3.up*1.3f-player.viewPosition;
                    if(!player.alive||delta.sqrMagnitude<1||delta.sqrMagnitude>36||Vector3.Dot(player.viewRotation*Vector3.forward,delta.normalized)<.97f||!CanObserve(player.position))continue;
                    if(Chance(.08f)){Attempt(Pick(Emote.YouScareMe,Emote.WatchingYou,Emote.Suspicious,Emote.DontTrustYou,Emote.NotMe),"Nearby observed gaze");return active;}
                }
            }
            return false;
        }
    }
}
