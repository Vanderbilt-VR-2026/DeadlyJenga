using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CandyAudio
{
    // Observes the existing enemy/appearance vocabulary, never reads Characters classes.
    public sealed class CandyJaguarAudio : MonoBehaviour
    {
        sealed class Animal
        {
            public Component owner;public IMovementState movement;public IModeActor actor;public Vector3 previous;
            public float travel,nextVoice;public bool grounded,dead;
            public bool hasFrame,frameAlive,frameDying;
            public float frameHealth,frameSeenAt,lastDamageAt=-100;
            public bool damageAnnounced,deathAnnounced;
        }
        public int ConsumedActions {get;private set;}
        public int PlayedActions {get;private set;}
        static CandyJaguarAudio instance;
        readonly Dictionary<int,Animal> animals=new Dictionary<int,Animal>();
        readonly List<int> remove=new List<int>();
        readonly List<GameObject> sceneRoots=new List<GameObject>();
        float nextScan,nextAmbient;
        readonly EnemyActionPlayback actionPlayback=new EnemyActionPlayback();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(instance)return;var go=new GameObject("Jaguar audio observer");DontDestroyOnLoad(go);instance=go.AddComponent<CandyJaguarAudio>();}
        public static bool IsJaguar(Component component)
        {
            if(!component)return false;
            if(component.GetComponent<IEnemyActor>() is IEnemyActor enemy&&enemy.IsEnemy&&(enemy.ArchetypeId=="panther"||enemy.ArchetypeId=="jaguar"))return true;
            return component.GetComponent<IAvatarAppearance>() is IAvatarAppearance avatar&&(avatar.Appearance.accessories&AvatarAccessory.PantherHead)!=0;
        }
        void OnEnable(){GameEvents.Damaged+=Damage;GameEvents.Eliminated+=Death;}
        void OnDisable(){GameEvents.Damaged-=Damage;GameEvents.Eliminated-=Death;}
        void OnDestroy(){if(instance==this)instance=null;}
        Animal Track(Component owner,IMovementState movement=null,IModeActor actor=null)
        {
            int id=owner.gameObject.GetInstanceID();
            if(!animals.TryGetValue(id,out var animal))
            {
                actor??=owner.GetComponent<IModeActor>();movement??=owner.GetComponent<IMovementState>();
                animal=new Animal{owner=owner,movement=movement,actor=actor,previous=owner.transform.position,grounded=movement?.Grounded??actor?.Grounded??false,nextVoice=Time.time+3+Mathf.Abs(id%70)*.1f};animals.Add(id,animal);
            }
            else{if(movement!=null)animal.movement=movement;if(actor!=null)animal.actor=actor;}
            return animal;
        }
        void ObserveMatch()
        {
            var runtime=MatchRuntime.Current;var frame=runtime?.Frame;if(frame?.players==null)return;
            foreach(var player in frame.players)
            {
                if(player==null||string.IsNullOrEmpty(player.id))continue;
                var actor=runtime.FindActor(player.id);
                if(actor==null){var root=runtime.FindParticipantRoot(player.id);if(root)actor=root.GetComponent<IModeActor>();}
                if(actor==null||!actor.gameObject||!IsJaguar(actor.gameObject.transform))continue;
                var animal=Track(actor.gameObject.transform,actor.gameObject.GetComponent<IMovementState>(),actor);
                float now=Time.unscaledTime,health=Mathf.Clamp01(player.health);
                bool death=!player.alive||player.dying;
                // Remote actors have no IMovementState. Their first snapshot is a silent baseline,
                // including an already hurt/dead actor encountered while joining a match.
                if(animal.movement==null&&animal.hasFrame)
                {
                    bool becameDead=(!player.alive&&animal.frameAlive)||(player.dying&&!animal.frameDying);
                    if(becameDead)
                    {
                        if(!animal.deathAnnounced)Play(CandySound.JaguarDeath,actor.gameObject);
                        animal.deathAnnounced=true;
                    }
                    else if(!death&&animal.frameAlive&&!animal.frameDying&&health<animal.frameHealth-.001f)
                    {
                        if(!animal.damageAnnounced)Play(CandySound.JaguarHurt,actor.gameObject,Mathf.Clamp((animal.frameHealth-health)*4,.4f,1));
                        animal.damageAnnounced=false;
                    }
                }
                if(!animal.hasFrame)animal.deathAnnounced=death;
                else if(!death&&(!animal.frameAlive||animal.frameDying))animal.deathAnnounced=false;
                if(!animal.hasFrame||health>animal.frameHealth+.001f||death||now-animal.lastDamageAt>2)animal.damageAnnounced=false;
                if(animal.movement==null)animal.dead=death;
                animal.hasFrame=true;animal.frameAlive=player.alive;animal.frameDying=player.dying;animal.frameHealth=health;animal.frameSeenAt=now;
            }
        }
        void DiscoverRemoteEnemies()
        {
            // NPCs are intentionally absent from the public participant frame. Network copies
            // are scene roots, so inspect only roots rather than walking the world hierarchy.
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);if(!scene.isLoaded)continue;
                sceneRoots.Clear();scene.GetRootGameObjects(sceneRoots);
                foreach(var root in sceneRoots)
                {
                    if(!root||!root.activeInHierarchy)continue;
                    var enemy=root.GetComponent<IEnemyActor>();
                    if(enemy==null||!enemy.IsEnemy||(enemy.ArchetypeId!="panther"&&enemy.ArchetypeId!="jaguar"))continue;
                    var actor=root.GetComponent<IModeActor>();if(actor!=null)Track(root.transform,root.GetComponent<IMovementState>(),actor);
                }
            }
            // Combat uses the authoritative event history, including events for already removed NPCs.
        }
        void Update()
        {
            var engine=CandyAudioEngine.Instance;if(!engine||!engine.bank)return;
            actionPlayback.Consume(MatchRuntime.Current,MatchRuntime.Current?.Frame,PlayEnemyAction);
            ObserveMatch();
            if(Time.time>=nextScan)
            {
                nextScan=Time.time+.5f;
                DiscoverRemoteEnemies();
                foreach(var mover in Tracked.Movers)
                {
                    if(!(mover is Component c)||!c||!IsJaguar(c))continue;
                    Track(c,mover);
                }
                remove.Clear();foreach(var pair in animals)if(!pair.Value.owner||!pair.Value.owner.gameObject.activeInHierarchy||!IsJaguar(pair.Value.owner)||pair.Value.movement==null&&pair.Value.hasFrame&&Time.unscaledTime-pair.Value.frameSeenAt>2)remove.Add(pair.Key);
                foreach(int id in remove)animals.Remove(id);
            }
            foreach(var animal in animals.Values)
            {
                var owner=animal.owner;if(!owner)continue;var p=owner.transform.position;
                float distance=Vector3.ProjectOnPlane(p-animal.previous,Vector3.up).magnitude;animal.previous=p;
                bool alive=animal.actor?.Alive??true;
                if(alive&&animal.movement!=null)animal.dead=false;
                if(!alive||animal.dead||animal.movement?.InputPaused==true||Services.Session?.MenuOpen==true)continue;
                bool nearby=CandyAudioWorld.Near(p,45);var motor=animal.movement;
                bool grounded=motor?.Grounded??animal.actor?.Grounded??false;
                if(nearby&&grounded&&!animal.grounded)Play(CandySound.JaguarLand,owner.gameObject,.7f);
                animal.grounded=grounded;
                if(distance>3){animal.travel=0;continue;}
                if(nearby&&grounded&&motor?.Climbing!=true&&motor?.Vaulting!=true)
                {
                    animal.travel+=distance;
                    if(animal.travel>.7f){animal.travel=0;Play(CandySound.JaguarFootfall,owner.gameObject,Mathf.Clamp(distance/Mathf.Max(.001f,Time.deltaTime)/5,.3f,.8f));}
                }
                // One distant creature voice at a time; variation comes from alternating breath, chuff and growl.
                if(nearby&&Time.time>=animal.nextVoice&&Time.time>=nextAmbient)
                {
                    int index=Mathf.Abs(owner.GetInstanceID()+(int)Time.time)%3;
                    Play(index==0?CandySound.JaguarGrowl:index==1?CandySound.JaguarChuff:CandySound.JaguarIdle,owner.gameObject,.55f);
                    animal.nextVoice=Time.time+6+Mathf.Abs(owner.GetInstanceID()%65)*.1f;nextAmbient=Time.time+1.25f;
                }
            }
        }
        void Play(CandySound cue,GameObject who,float gain=1)
        {
            if(!who)return;var engine=CandyAudioEngine.Instance;if(!engine)return;
            var motor=who.GetComponent<IMovementState>();var actor=who.GetComponent<IModeActor>();float height=motor?.Height??(actor!=null?Mathf.Clamp(actor.ViewPose.position.y-who.transform.position.y,.3f,3):1);
            engine.Play(cue,who.transform.position+Vector3.up*(cue==CandySound.JaguarFootfall?0:height*.7f),gain,who.GetInstanceID(),.96f+Mathf.Abs(who.GetInstanceID()%9)*.01f);
        }
        void Damage(GameObject who,float amount)
        {
            if(!who||amount<=0||who.GetComponent<IEnemyActor>()?.IsEnemy==true||!IsJaguar(who.transform))return;
            var animal=Track(who.transform);animal.lastDamageAt=Time.unscaledTime;animal.damageAnnounced=true;Play(CandySound.JaguarHurt,who,Mathf.Clamp(amount/25,.4f,1));
        }
        void Death(GameObject who,GameObject source)
        {
            if(!who||who.GetComponent<IEnemyActor>()?.IsEnemy==true||!IsJaguar(who.transform))return;
            var animal=Track(who.transform);if(animal.dead||animal.deathAnnounced)return;animal.dead=true;animal.deathAnnounced=true;
            Play(CandySound.JaguarDeath,who);
        }
        void PlayEnemyAction(EnemyActionFrame item)
        {
            if(item.archetype!="panther"&&item.archetype!="jaguar")return;
            CandySound cue;
            switch(item.action)
            {
                case EnemyActionKind.Alert:cue=CandySound.JaguarAlert;break;
                case EnemyActionKind.Attack:cue=CandySound.JaguarAttack;break;
                case EnemyActionKind.Hurt:cue=CandySound.JaguarHurt;break;
                case EnemyActionKind.Death:cue=CandySound.JaguarDeath;break;
                default:return;
            }
            ConsumedActions++;
            foreach(var animal in animals.Values)if(animal.actor?.ActorId==item.actor&&item.action==EnemyActionKind.Death)animal.dead=true;
            int owner=17;foreach(char c in item.actor??"")owner=unchecked(owner*31+c);
            var engine=CandyAudioEngine.Instance;
            if(engine&&engine.Play(cue,item.position,item.strength,owner,.96f+(uint)owner%9*.01f))PlayedActions++;
        }
    }
}
