using UnityEngine;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastleSocial
{
    // Adapter around the legal existing navigator/motor. Social goals are offered
    // to its route planner; the adapter never teleports or applies block forces.
    [DefaultExecutionOrder(210)]
    public sealed class SocialBotAgent : MonoBehaviour,ICastleSurvivorDriver
    {
        public SocialState State {get;private set;}
        public CastleBotBrain Brain {get;private set;}
        public SocialWorld World {get;private set;}
        CastleBotPersonality personal;float nextGroup,phase,hesitateUntil;int respawns,vaults;bool grounded;Vector3 previousMove;
        public string NetworkId=>"bot:"+Brain.GetInstanceID();
        public void Initialize(SocialWorld owner,CastleBotBrain brain,SocialState state)
        {
            World=owner;Brain=brain;State=state;
            var template=brain.Personality;var previous=personal;personal=Instantiate(template);if(previous)Destroy(previous);personal.name="Generated social personality";brain.Personality=personal;brain.Motor.InputDriver=this;
            var b=state.identity;gameObject.name=b.name+" [AI]";
            personal.bravery=(float)b.Behavior(owner.Config,"risk",b.Trait("boldness"));personal.goofiness=(float)b.Trait("playfulness")*owner.Config.silliness;
            personal.sociability=(float)b.Trait("sociability");personal.exploration=(float)(.3+.7*b.Trait("boldness"));
            personal.survivalPriority=(float)(.4+.6*b.Trait("strategy"));personal.pace=(float)(.65+.35*b.skill/100);
            personal.decisionInterval=(float)(.18+.55*b.Behavior(owner.Config,"delay",1-b.Trait("impulsiveness")));
            personal.journeyDrive=(float)(.4+.4*b.Trait("competitiveness")+.2*b.Trait("strategy"));personal.hidePreference=(float)(.65*(1-b.Trait("boldness")));personal.restSeconds=(float)(10-8*b.Trait("impulsiveness"));
            phase=(float)(b.age*.71+b.skill);grounded=brain.Motor.Grounded;respawns=brain.Respawns;vaults=brain.Motor.VaultCount;
            if(!GetComponent<CastleBotEmotes>())gameObject.AddComponent<CastleBotEmotes>();
        }
        public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
        {
            var command=Brain.ReadCommand(motor,dt);if(State==null)return command;
            // Preserve ballistic/ledge commands exactly. Imprecision affects safe
            // grounded walking only, with coherent timing rather than random inputs.
            if(motor.Grounded&&!motor.Vaulting&&!command.jump&&!motor.Climbing)
            {
                float skill=(float)(State.identity.skill/100);float smooth=1-Mathf.Exp(-dt*Mathf.Lerp(7,18,skill));
                previousMove=Vector3.Lerp(previousMove,command.move,smooth);command.move=previousMove;
                if(Time.time<hesitateUntil&&World.Session.Bots.ThreatCost(transform.position)<.2f)command.move*=.2f;
                if(State.focus!=null&&command.move.sqrMagnitude<.1f&&World.TryPosition(State.focus,out var target))command.look=target-transform.position;
            }
            return command;
        }
        void Update()
        {
            if(!Brain||State==null||!World.IsAuthority)return;
            var motor=Brain.Motor;
            if(Brain.Respawns!=respawns){respawns=Brain.Respawns;World.Publish(SocialEventKind.Elimination,State.identity.id,1);}
            if(motor.VaultCount!=vaults){vaults=motor.VaultCount;World.Publish(SocialEventKind.Success,State.identity.id,.7);}
            if(grounded&&!motor.Grounded&&motor.VerticalSpeed< -3)World.Publish(SocialEventKind.NearMiss,State.identity.id,.7);grounded=motor.Grounded;
            if(Time.time<nextGroup)return;nextGroup=Time.time+3+Mathf.Repeat(phase,3);
            personal.bravery=Mathf.Clamp01((float)State.identity.Behavior(World.Config,"risk",State.identity.Trait("boldness"))-(float)State.emotion.fear*.18f);
            if(!Brain.Respawning&&motor.Grounded&&State.identity.group>=0&&Mathf.PerlinNoise(phase,Time.time*.1f)<World.Config.cohesion*(.5+State.identity.Behavior(World.Config,"help",State.identity.Trait("cooperativeness"))))
            {
                var friend=World.NearestFriend(this);
                if(friend&&Vector3.Distance(friend.transform.position,transform.position)>3&&World.Session.Bots.ThreatCost(friend.transform.position)<.7f)
                {Brain.SetJourneyDestination(friend.transform.position,12);State.reason="Seeking friend "+friend.State.identity.name;}
            }
            if(State.focus!=null&&State.identity.Trait("sociability")>.65&&World.Session.Bots.ThreatCost(transform.position)<.1f)
                hesitateUntil=Time.time+(float)(.15+.35*(1-State.identity.skill/100));
            var emotes=GetComponent<CastleBotEmotes>();if(emotes)emotes.frequency=Mathf.Lerp(.3f,1.4f,(float)(State.identity.Trait("reactivity")*World.Config.reactions));
        }
        void OnDestroy()
        {if(Brain&&Brain.Motor&&ReferenceEquals(Brain.Motor.InputDriver,this))Brain.Motor.InputDriver=Brain;if(personal)Destroy(personal);if(World)World.Detach(this);}
    }
}
