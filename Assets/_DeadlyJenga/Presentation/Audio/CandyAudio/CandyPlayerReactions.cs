using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Local events respond immediately. Remote health/death transitions come from match snapshots.
    public sealed class CandyPlayerReactions : MonoBehaviour
    {
        sealed class Reaction
        {
            public bool baseline,alive,dying;
            public float health,lastPain=-100,lastDeath=-100,lastSeen;
        }
        static CandyPlayerReactions instance;
        readonly Dictionary<string,Reaction> players=new Dictionary<string,Reaction>();
        readonly List<string> expired=new List<string>();
        IMatchRuntime owner;float nextPrune;
        public int PainCues {get;private set;}
        public int DeathCues {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>instance=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(instance)return;var go=new GameObject("Player pain and death audio");DontDestroyOnLoad(go);instance=go.AddComponent<CandyPlayerReactions>();}
        void OnEnable(){GameEvents.Damaged+=Damage;GameEvents.Eliminated+=Death;}
        void OnDisable(){GameEvents.Damaged-=Damage;GameEvents.Eliminated-=Death;}
        void OnDestroy(){if(instance==this)instance=null;}
        Reaction Get(string id)
        {
            if(!players.TryGetValue(id,out var value)){value=new Reaction();players.Add(id,value);}
            value.lastSeen=Time.unscaledTime;return value;
        }
        static string Id(GameObject who)
        {string id=who.GetComponent<IModeActor>()?.ActorId;return string.IsNullOrEmpty(id)?"object:"+who.GetInstanceID():id;}
        static bool Human(GameObject who)=>who&&(who.GetComponent<IMovementState>()!=null||who.GetComponent<IModeActor>()!=null)&&!CandyJaguarAudio.IsJaguar(who.transform)&&who.GetComponent<IEnemyActor>()?.IsEnemy!=true;
        void Damage(GameObject who,float amount)
        {
            if(!Human(who)||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0)return;
            var health=who.GetComponent<IDamageable>();if(health!=null&&health.Health<=0)return;
            Cue(Get(Id(who)),false,who.transform.position+Vector3.up*.9f,Mathf.Clamp(amount/30,.4f,1),who);
        }
        void Death(GameObject who,GameObject source)
        {if(Human(who))Cue(Get(Id(who)),true,who.transform.position+Vector3.up*.6f,1,who);}
        void Cue(Reaction state,bool death,Vector3 position,float gain,GameObject who)
        {
            float now=Time.unscaledTime;
            if(death?now-state.lastDeath<.7f:now-state.lastPain<.38f||now-state.lastDeath<.7f)return;
            if(death)state.lastDeath=now;else state.lastPain=now;
            bool local=who&&Services.Session?.Player!=null&&who.transform.root==Services.Session.Player.transform.root;
            int seed=who?who.GetInstanceID():0;
            if(CandyAudioEngine.Instance&&CandyAudioEngine.Instance.Play(death?CandySound.PlayerDeath:CandySound.PlayerPain,position,gain,seed,.94f+Mathf.Abs(seed%13)*.01f,local))
            {if(death)DeathCues++;else PainCues++;}
        }
        void Update()=>Observe(MatchRuntime.Current,MatchRuntime.Current?.Frame);
        public void Observe(IMatchRuntime runtime,MatchFrame frame)
        {
            if(!ReferenceEquals(owner,runtime)){owner=runtime;players.Clear();}
            if(frame?.players==null)return;
            foreach(var player in frame.players)
            {
                if(player==null||string.IsNullOrEmpty(player.id)||player.role=="hand")continue;
                var actor=runtime?.FindActor(player.id);var who=actor?.gameObject;
                if(who&&!Human(who))continue;
                var state=Get(player.id);bool dead=!player.alive||player.dying;
                if(state.baseline)
                {
                    if(dead&&state.alive&&!state.dying)Cue(state,true,player.position+Vector3.up*.6f,1,who);
                    else if(!dead&&state.alive&&!state.dying&&player.health<state.health-.001f)Cue(state,false,player.position+Vector3.up*.9f,Mathf.Clamp((state.health-player.health)*3,.4f,1),who);
                }
                state.baseline=true;state.alive=player.alive;state.dying=player.dying;state.health=player.health;
            }
            if(Time.unscaledTime<nextPrune)return;nextPrune=Time.unscaledTime+5;
            expired.Clear();foreach(var pair in players)if(Time.unscaledTime-pair.Value.lastSeen>10)expired.Add(pair.Key);
            foreach(var id in expired)players.Remove(id);
        }
    }
}
