using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(300)]
    public sealed class PlayerSpawnArrival : MonoBehaviour
    {
        const float DropMeters=10,DropSeconds=1.35f;
        readonly RaycastHit[] hits=new RaycastHit[64];
        readonly Collider[] overlaps=new Collider[64];
        CastlePlaySession session;
        IPlayableCharacter player;
        ITeleportable teleport;
        bool pending,used,active;
        float started,radius,height,seconds,elapsed,lastTick;
        Vector3 previous,capsuleOffset;
        Quaternion rotation;
        public bool Active=>active;
        public bool Dropping=>active&&DropHeight>0;
        public float DropHeight {get;private set;}
        public Vector3 Destination {get;private set;}
        public float Progress=>Mathf.Clamp01(elapsed/Mathf.Max(.01f,seconds));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen(){GameEvents.SessionStarted-=Attach;GameEvents.SessionStarted+=Attach;}
        static void Attach(GameObject root)
        {if(root&&root.GetComponent<CastlePlaySession>()&&!root.GetComponent<PlayerSpawnArrival>())root.AddComponent<PlayerSpawnArrival>();}
        void Awake(){session=GetComponent<CastlePlaySession>();}
        void OnEnable(){GameEvents.Emoted+=Emoted;}
        void OnDisable()
        {
            GameEvents.Emoted-=Emoted;
            if(active&&session&&ReferenceEquals(Services.Session,session)&&NetworkActors.Alive(player))Finish();
            else active=false;
        }
        void Emoted(GameObject who,string cue)
        {if(!used&&who==gameObject&&cue=="TravelArrived")pending=true;}
        void Begin()
        {
            pending=false;used=true;
            if(!session||session.PlayingHand||!NetworkActors.Alive(session.Player))return;
            player=session.Player;teleport=player.gameObject.GetComponent<ITeleportable>();
            if(!player.gameObject.activeInHierarchy||(player.gameObject.GetComponent<IDamageable>()?.Health??1)<=0)return;
            var state=player.gameObject.GetComponent<IMovementState>();
            radius=Mathf.Max(.2f,.32f*(state?.Scale??1));height=Mathf.Max(radius*2,state?.Height??1.8f);capsuleOffset=Vector3.zero;
            if(player.IsVR&&player.ViewCamera)
            {
                var head=player.ViewCamera.transform.position-player.transform.position;
                capsuleOffset=Vector3.ProjectOnPlane(head,Vector3.up);height=Mathf.Max(height,head.y+.2f);
            }
            Destination=player.transform.position;rotation=player.transform.rotation;
            DropHeight=AvailableDrop(Destination);seconds=DropHeight>0?Mathf.Lerp(.85f,DropSeconds,DropHeight/DropMeters):.75f;
            started=lastTick=Time.unscaledTime;elapsed=0;active=true;player.InputSuspended=true;
            Move(Destination+Vector3.up*DropHeight);
            GameEvents.RaiseEmoted(player.gameObject,"SpawnArrival");
        }
        void Update()
        {
            if(pending){Begin();return;}
            if(!active)return;
            if(!NetworkActors.Alive(player)||session.PlayingHand||(player.gameObject.GetComponent<IDamageable>()?.Health??1)<=0){Finish();return;}
            var displacement=player.transform.position-previous;
            if(Vector3.ProjectOnPlane(displacement,Vector3.up).sqrMagnitude>1||Mathf.Abs(displacement.y)>3){Finish();return;}
            player.InputSuspended=true;
            float now=Time.unscaledTime;elapsed+=Mathf.Clamp(now-lastTick,0,.05f);lastTick=now;
            float p=Progress;
            var next=Destination+Vector3.up*(DropHeight*(1-p*p));
            float distance=previous.y-next.y;
            if(distance>.0001f&&Closest(previous,Vector3.down,distance,out var contact))
            {
                Move(previous+Vector3.down*Mathf.Max(0,contact.distance-.085f));Finish();return;
            }
            if(Occupied(next)){Finish();return;}
            Move(next);if(p>=1)Finish();
        }
        void Move(Vector3 feet)
        {
            if(teleport!=null)teleport.Teleport(feet,rotation);else player.Place(feet,rotation);
            previous=player.transform.position;
        }
        void Finish()
        {
            if(!active)return;active=false;
            if(NetworkActors.Alive(player))GameEvents.RaiseEmoted(player.gameObject,"SpawnArrivalSettled");
            if(session&&ReferenceEquals(Services.Session,session))session.SetMenu(session.MenuOpen);
        }
        int Mask=>(1<<Layers.Block)|(1<<Layers.Surface)|1;
        void Capsule(Vector3 feet,out Vector3 bottom,out Vector3 top)
        {bottom=feet+capsuleOffset+Vector3.up*(radius+.06f);top=feet+capsuleOffset+Vector3.up*Mathf.Max(radius+.06f,height-radius+.06f);}
        bool Solid(Collider collider)
        {return collider&&!collider.transform.IsChildOf(player.transform)&&collider.GetComponentInParent<IModeActor>()==null;}
        bool Occupied(Vector3 feet)
        {
            Capsule(feet,out var bottom,out var top);
            int count=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,overlaps,Mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return true;
            for(int i=0;i<count;i++)if(Solid(overlaps[i]))return true;
            return false;
        }
        bool Closest(Vector3 feet,Vector3 direction,float distance,out RaycastHit closest)
        {
            Capsule(feet,out var bottom,out var top);closest=default;float nearest=float.PositiveInfinity;
            int count=Physics.CapsuleCastNonAlloc(bottom,top,radius,direction,hits,distance,Mask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length){closest.distance=0;return true;}
            for(int i=0;i<count;i++)if(Solid(hits[i].collider)&&hits[i].distance<nearest){nearest=hits[i].distance;closest=hits[i];}
            return nearest<float.PositiveInfinity;
        }
        float AvailableDrop(Vector3 feet)
        {
            if(Occupied(feet))return 0;
            return Closest(feet,Vector3.up,DropMeters+.08f,out var ceiling)?Mathf.Clamp(ceiling.distance-.12f,0,DropMeters):DropMeters;
        }
    }
}
