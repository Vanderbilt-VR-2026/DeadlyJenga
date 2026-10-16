using System;
using UnityEngine;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    // Runs after the shared bot/avatar presentation. These are cosmetic poses only:
    // no movement commands, colliders, forces, root motion or changes to vault grips.
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class CastleBotEmotes : MonoBehaviour
    {
        public enum Emote { FriendlyWave, TotallyMeantThat, Phew, WhatWasThat, GolfClap, JustChilling, NopeNopeNope, TinyVictoryDance }
        [Range(0,2)] public float frequency=1;
        public float minimumCooldown=8;
        public bool contextualReactions=true;
        public string CurrentEmote=>active?current.ToString():"None";
        public string LastReason {get;private set;}="";
        public int EmotesPlayed {get;private set;}
        CastleBotBrain brain;
        CandyPlayerAvatar avatar;
        System.Random random;
        Emote current;
        bool active,wasNear,wasGrounded;
        float began,duration,nextAllowed,nextAmbient,nextSense,lastDanger=-100,nearMissUntil=-100,previousY,speed;
        int previousVaults;
        Vector3 previousFeet;

        void Awake()
        {
            brain=GetComponent<CastleBotBrain>();avatar=GetComponent<CandyPlayerAvatar>();
            random=new System.Random(unchecked(GetInstanceID()*397^Environment.TickCount));
            previousFeet=transform.position;previousY=transform.position.y;
            nextAllowed=Time.time+Range(2,6);nextAmbient=Time.time+Range(6,18);nextSense=Time.time+Range(0,.3f);
        }
        float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        bool Chance(float probability)=>random.NextDouble()<Mathf.Clamp01(probability);
        float Goofiness=>brain&&brain.Personality?brain.Personality.goofiness:.2f;
        bool IdleOnly(Emote emote)=>emote==Emote.WhatWasThat||emote==Emote.JustChilling||emote==Emote.TinyVictoryDance||emote==Emote.GolfClap;
        bool CanPose()=>brain&&brain.isActiveAndEnabled&&brain.Motor&&!brain.Respawning&&brain.Motor.Grounded&&!brain.Motor.Vaulting&&avatar&&avatar.Rig&&avatar.Rig.gameObject.activeInHierarchy;

        public bool TryPlay(Emote emote,bool ignoreCooldown=false)
        {
            if(!CanPose()||active||frequency<=0||(!ignoreCooldown&&Time.time<nextAllowed)||IdleOnly(emote)&&speed>.45f)return false;
            current=emote;began=Time.time;duration=emote==Emote.FriendlyWave?1.5f:emote==Emote.NopeNopeNope?1.1f:Range(1.6f,2.4f);
            active=true;EmotesPlayed++;GameEvents.RaiseEmoted(gameObject,current.ToString());nextAllowed=Time.time+duration+Mathf.Max(2,minimumCooldown)*Range(.85f,1.5f)/Mathf.Max(.25f,frequency);
            return true;
        }
        void Attempt(Emote emote,string reason)
        {if(TryPlay(emote))LastReason=reason;}
        void Update()
        {
            float dt=Mathf.Max(.001f,Time.deltaTime);
            speed=Vector3.ProjectOnPlane(transform.position-previousFeet,Vector3.up).magnitude/dt;previousFeet=transform.position;
            if(!brain||!brain.Motor)return;
            bool grounded=brain.Motor.Grounded;
            if(!wasGrounded&&grounded&&previousY-transform.position.y>.03f)nearMissUntil=Time.time+3;
            wasGrounded=grounded;previousY=transform.position.y;
            if(brain.Motor.VaultCount>previousVaults){nearMissUntil=Time.time+4;previousVaults=brain.Motor.VaultCount;}
            if(active&&(!CanPose()||IdleOnly(current)&&speed>.65f||Time.time-began>=duration))active=false;
            if(Time.time<nextSense)return;nextSense=Time.time+.25f;
            if(!CanPose()||active||Time.time<nextAllowed||frequency<=0)return;
            float danger=brain.Population?brain.Population.ThreatCost(transform.position):0;
            if(danger>.7f)lastDanger=Time.time;
            if(contextualReactions)
            {
                if(danger>.9f&&Chance(.08f+Goofiness*.14f)){Attempt(Emote.NopeNopeNope,"Mayhem nearby");return;}
                if(danger<.25f&&Time.time-lastDanger<3)nearMissUntil=Time.time+2;
                if(Time.time<nearMissUntil&&Chance(.15f+Goofiness*.25f))
                {
                    Attempt(Chance(.5f)?Emote.Phew:Emote.TotallyMeantThat,"Survived a scare, landing or vault");
                    if(active){nearMissUntil=-100;lastDanger=-100;return;}
                }
                bool near=NearSomeone();
                if(near&&!wasNear&&Chance(.3f+Goofiness*.4f)){Attempt(Emote.FriendlyWave,"Ran into another player");wasNear=near;if(active)return;}
                wasNear=near;
            }
            if(Time.time>=nextAmbient)
            {
                nextAmbient=Time.time+Range(12,30)/Mathf.Max(.25f,frequency);
                if(Chance(.25f+Goofiness*.6f))
                {
                    Emote emote=speed>.45f?Emote.FriendlyWave:(Emote)random.Next(0,8);
                    Attempt(emote,"Just vibing");
                }
            }
        }
        bool NearSomeone()
        {
            if(!brain.Population)return false;
            var session=brain.Population.Session;
            float radius=1.3f;
            if(session!=null&&session.Player&&session.Player.gameObject.activeInHierarchy&&(session.Player.transform.position-transform.position).sqrMagnitude<radius*radius)return true;
            foreach(var bot in brain.Population.Bots)
                if(bot&&bot!=brain&&!bot.Respawning&&(bot.transform.position-transform.position).sqrMagnitude<radius*radius)return true;
            return false;
        }
        void LateUpdate()
        {
            if(!active||!CanPose())return;
            float t=Time.time-began;
            float weight=Mathf.SmoothStep(0,1,Mathf.Min(Mathf.Clamp01(t/.2f),Mathf.Clamp01((duration-t)/.25f)));
            if(weight<=0)return;
            var frame=avatar.LastFrame;
            Vector3 l=new Vector3(-.27f,.83f,.06f),r=new Vector3(.27f,.83f,.06f);
            Vector3 lr=new Vector3(80,0,10),rr=new Vector3(80,0,-10);
            float lc=.12f,rc=.12f,pitch=0,roll=0;
            float wave=Mathf.Sin(t*12),bounce=Mathf.Sin(t*8);
            switch(current)
            {
                case Emote.FriendlyWave:
                    r=new Vector3(.34f+.07f*wave,1.65f,.12f);rr=new Vector3(-70,0,-15+wave*22);pitch=-3;break;
                case Emote.TotallyMeantThat:
                    l=new Vector3(-.28f,1.45f+.09f*bounce,.15f);r=new Vector3(.28f,1.45f+.09f*bounce,.15f);lr=rr=new Vector3(-65,0,0);lc=rc=.95f;pitch=-7;break;
                case Emote.Phew:
                    r=new Vector3(Mathf.Lerp(.20f,-.10f,Mathf.PingPong(t*.7f,1)),1.53f,.22f);rr=new Vector3(0,-60,70);pitch=8;break;
                case Emote.WhatWasThat:
                    l=new Vector3(-.43f,1.16f,.18f);r=new Vector3(.43f,1.16f,.18f);lr=new Vector3(-10,-35,-25);rr=new Vector3(-10,35,25);roll=9*Mathf.Sin(t*3);break;
                case Emote.GolfClap:
                    float gap=.035f+.055f*(.5f+.5f*Mathf.Cos(t*13));l=new Vector3(-gap,1.12f,.32f);r=new Vector3(gap,1.12f,.32f);lr=new Vector3(-15,60,-70);rr=new Vector3(-15,-60,70);pitch=4*Mathf.Sin(t*6);break;
                case Emote.JustChilling:
                    l=new Vector3(-.31f,1.73f,.02f);r=new Vector3(.31f,1.73f,.02f);lr=new Vector3(-80,-15,0);rr=new Vector3(-80,15,0);pitch=-10;lc=rc=.25f;break;
                case Emote.NopeNopeNope:
                    l=new Vector3(-.40f,1.27f+bounce*.12f,.08f);r=new Vector3(.40f,1.27f-bounce*.12f,.08f);lr=new Vector3(-35,0,-35);rr=new Vector3(-35,0,35);roll=wave*7;break;
                default:
                    l=new Vector3(-.35f,1.12f+bounce*.15f,.15f);r=new Vector3(.35f,1.12f-bounce*.15f,.15f);lr=new Vector3(-30,0,-20);rr=new Vector3(-30,0,20);roll=bounce*8;lc=rc=.7f;break;
            }
            frame.left=BlendHand(frame.left,l,lr,lc,true,frame,weight);frame.right=BlendHand(frame.right,r,rr,rc,false,frame,weight);
            frame.headRotation=Quaternion.Slerp(frame.headRotation,frame.headRotation*Quaternion.Euler(pitch,0,roll),weight);
            // deltaTime=0 retains the locomotion phase advanced by CastleBotBrain.
            avatar.Rig.ApplyFrame(frame,brain.Motor.Grounded,0);
        }
        CandyHandPose BlendHand(CandyHandPose hand,Vector3 local,Vector3 euler,float curl,bool left,CandyAvatarFrame frame,float weight)
        {
            hand.position=Vector3.Lerp(hand.position,frame.feet+transform.rotation*(local*frame.scale),weight);
            hand.rotation=Quaternion.Slerp(hand.rotation,transform.rotation*Quaternion.Euler(euler),weight);
            var skeleton=Flex(left,curl);
            var previous=hand.skeletonTracked?hand.skeleton:CandyHandSkeleton.Rest(left,.12f);
            for(int i=0;i<20;i++)previous.SetJoint(i,Vector3.Lerp(previous.Joint(i),skeleton.Joint(i),weight));
            hand.skeleton=previous;hand.skeletonTracked=true;return hand;
        }
        static CandyHandSkeleton Flex(bool left,float curl)
        {
            var rest=CandyHandSkeleton.Rest(left);var result=rest;
            for(int f=0;f<5;f++)
            {
                Vector3 p=rest.Joint(f*4);
                for(int j=0;j<3;j++)
                {
                    var rotation=f==0?Quaternion.AngleAxis((left?-1:1)*curl*(j+1)*15,Vector3.up):Quaternion.AngleAxis(curl*(j+1)*32,Vector3.right);
                    p+=rotation*(rest.Joint(f*4+j+1)-rest.Joint(f*4+j));result.SetJoint(f*4+j+1,p);
                }
            }
            return result;
        }
        void OnDisable(){active=false;}
        [ContextMenu("Preview/Wave (Play Mode)")]void PreviewWave()=>TryPlay(Emote.FriendlyWave,true);
        [ContextMenu("Preview/Totally Meant That (Play Mode)")]void PreviewCelebrate()=>TryPlay(Emote.TotallyMeantThat,true);
        [ContextMenu("Preview/Phew (Play Mode)")]void PreviewPhew()=>TryPlay(Emote.Phew,true);
        [ContextMenu("Preview/Shrug (Play Mode)")]void PreviewShrug()=>TryPlay(Emote.WhatWasThat,true);
        [ContextMenu("Preview/Golf Clap (Play Mode)")]void PreviewClap()=>TryPlay(Emote.GolfClap,true);
        [ContextMenu("Preview/Stretch (Play Mode)")]void PreviewStretch()=>TryPlay(Emote.JustChilling,true);
        [ContextMenu("Preview/Panic Flap (Play Mode)")]void PreviewPanic()=>TryPlay(Emote.NopeNopeNope,true);
        [ContextMenu("Preview/Tiny Victory Dance (Play Mode)")]void PreviewDance()=>TryPlay(Emote.TinyVictoryDance,true);
    }
}
