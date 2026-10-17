using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleSocial
{
    // Gives one survivor bot a social identity: turns its traits into a personality, nudges how it
    // walks and reports what happens to it. The bot itself (Characters, through Core's ISocialBot)
    // does the moving; this never teleports or applies block forces.
    [DefaultExecutionOrder(210)]
    public sealed class SocialBotAgent : MonoBehaviour
    {
        public SocialState State {get;private set;}
        public ISocialBot Bot {get;private set;}
        public SocialWorld World {get;private set;}
        float nextGroup,phase;int respawns,vaults;bool grounded;
        public string NetworkId=>Bot.NetworkId;
        bool BotAlive=>NetworkActors.Alive(Bot);
        public void Initialize(SocialWorld owner,ISocialBot bot,SocialState state)
        {
            World=owner;Bot=bot;State=state;
            var b=state.identity;gameObject.name=b.name+" [AI]";
            bot.Tune(new SocialBotTuning{
                bravery=(float)b.Behavior(owner.Config,"risk",b.Trait("boldness")),goofiness=(float)b.Trait("playfulness")*owner.Config.silliness,
                sociability=(float)b.Trait("sociability"),exploration=(float)(.3+.7*b.Trait("boldness")),
                survivalPriority=(float)(.4+.6*b.Trait("strategy")),pace=(float)(.65+.35*b.skill/100),
                decisionInterval=(float)(.18+.55*b.Behavior(owner.Config,"delay",1-b.Trait("impulsiveness"))),
                journeyDrive=(float)(.4+.4*b.Trait("competitiveness")+.2*b.Trait("strategy")),hidePreference=(float)(.65*(1-b.Trait("boldness"))),restSeconds=(float)(10-8*b.Trait("impulsiveness"))});
            bot.Skill=(float)(b.skill/100);bot.LookTarget=Focus;
            phase=(float)(b.age*.71+b.skill);grounded=bot.Grounded;respawns=bot.Respawns;vaults=bot.VaultCount;
        }
        Vector3? Focus()=>State?.focus!=null&&World&&World.TryPosition(State.focus,out var target)?target:(Vector3?)null;
        void Update()
        {
            if(!BotAlive||State==null||!World.IsAuthority)return;
            if(Bot.Respawns!=respawns){respawns=Bot.Respawns;World.Publish(SocialEventKind.Elimination,State.identity.id,1);}
            if(Bot.VaultCount!=vaults){vaults=Bot.VaultCount;World.Publish(SocialEventKind.Success,State.identity.id,.7);}
            if(grounded&&!Bot.Grounded&&Bot.VerticalSpeed< -3)World.Publish(SocialEventKind.NearMiss,State.identity.id,.7);grounded=Bot.Grounded;
            if(Time.time<nextGroup)return;nextGroup=Time.time+3+Mathf.Repeat(phase,3);
            Bot.SetBravery((float)State.identity.Behavior(World.Config,"risk",State.identity.Trait("boldness"))-(float)State.emotion.fear*.18f);
            if(!Bot.Respawning&&Bot.Grounded&&State.identity.group>=0&&Mathf.PerlinNoise(phase,Time.time*.1f)<World.Config.cohesion*(.5+State.identity.Behavior(World.Config,"help",State.identity.Trait("cooperativeness"))))
            {
                var friend=World.NearestFriend(this);
                if(friend&&Vector3.Distance(friend.transform.position,transform.position)>3&&World.Session.SurvivorBots.ThreatCost(friend.transform.position)<.7f)
                {Bot.SeekPoint(friend.transform.position,12);State.reason="Seeking friend "+friend.State.identity.name;}
            }
            if(State.focus!=null&&State.identity.Trait("sociability")>.65&&World.Session.SurvivorBots.ThreatCost(transform.position)<.1f)
                Bot.HesitateUntil=Time.time+(float)(.15+.35*(1-State.identity.skill/100));
            Bot.EmoteFrequency=Mathf.Lerp(.3f,1.4f,(float)(State.identity.Trait("reactivity")*World.Config.reactions));
        }
        void OnDestroy(){if(BotAlive)Bot.Release();if(World)World.Detach(this);}
    }
}
