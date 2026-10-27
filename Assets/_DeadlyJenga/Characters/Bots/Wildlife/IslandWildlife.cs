using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Wildlife
{
    [DisallowMultipleComponent]
    public sealed class IslandWildlife : MonoBehaviour, IWildlifeControls
    {
        internal const int GroundMask=1|(1<<Layers.Block)|(1<<Layers.Surface);
        static readonly string[] species={"otter","fox","tortoise","jaguar","penguin","ray","crab","frog","boar","monkey","seal","beetle"};
        readonly List<IslandAnimal> animals=new List<IslandAnimal>();
        ISessionInfo session;
        IWorldQuery world;
        int serial;
        public IReadOnlyList<string> Species => species;
        public int Count { get { animals.RemoveAll(a=>!a);return animals.Count; } }
        public bool Available => session!=null&&session.Multiplayer?.CanLeave!=true;
        public string Status { get;private set; }="Local wildlife preview. Wild animals wander; companions follow you.";
        public IWorldQuery World => world;
        public ISessionInfo Session => session;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            GameEvents.SessionStarted+=Attach;GameEvents.LobbyStarted+=Attach;
        }
        static void Attach(GameObject root)
        {
            if(!root||root.GetComponent<IslandWildlife>())return;
            var population=root.AddComponent<IslandWildlife>();population.session=root.GetComponent<ISessionInfo>();population.world=Services.World;
        }
        public GameObject Spawn(string id,bool tame)
        {
            if(!Available){Status="Wildlife preview is local only; leave multiplayer to test animals.";return null;}
            if(System.Array.IndexOf(species,id)<0){Status="Unknown animal species.";return null;}
            if(Count>=24){Status="Wildlife limit reached (24). Clear some animals first.";return null;}
            var player=session.Player;
            if(player==null||!player.gameObject){Status="A player is needed to place wildlife safely.";return null;}
            Vector3 origin=player.transform.position;Vector3 forward=player.transform.forward;
            forward.y=0;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;forward.Normalize();
            var right=Vector3.Cross(Vector3.up,forward);
            Vector3 point=origin;
            bool found=false;
            for(int i=0;i<24;i++)
            {
                float angle=(serial*137.5f+i*47)*Mathf.Deg2Rad;
                float radius=2.2f+(i/8)*1.4f;
                Vector3 sample=origin+right*Mathf.Sin(angle)*radius+forward*Mathf.Cos(angle)*radius;
                if(TryGround(sample,origin.y+1.8f,8,out point)&&!Physics.CheckCapsule(point+Vector3.up*.36f,point+Vector3.up*.90f,.28f,GroundMask,QueryTriggerInteraction.Ignore)){found=true;break;}
            }
            if(!found){Status="No nearby supported ground. Stand on a wider platform or the island.";return null;}
            var root=new GameObject("Island wildlife - "+id);root.transform.SetParent(transform,true);root.transform.position=point;
            var animal=root.AddComponent<IslandAnimal>();animal.Initialize(this,id,++serial);
            if(tame)animal.SetCompanion(player.transform);
            animals.Add(animal);Status=(tame?"Companion: ":"Wild: ")+id+". "+Count+" / 24 animals.";return root;
        }
        public bool TryGround(Vector3 point,float top,float depth,out Vector3 floor)
        {
            int mask=GroundMask;
            if(Physics.Raycast(new Vector3(point.x,top,point.z),Vector3.down,out var hit,depth,mask,QueryTriggerInteraction.Ignore)&&hit.normal.y>.65f)
            {floor=hit.point;return true;}
            floor=point;return false;
        }
        public void Clear()
        {
            foreach(var animal in animals)if(animal)Destroy(animal.gameObject);
            animals.Clear();Status="Wildlife cleared.";
        }
        void Update()
        {
            if(ReferenceEquals(Services.Session,session))world=Services.World;
            // Retained lobby animals sleep while the original lobby player is parked.
            bool present=!(session is ILobbyControls)||(session?.Player!=null&&session.Player.gameObject&&session.Player.gameObject.activeInHierarchy);
            bool active=present&&Available;
            for(int i=animals.Count-1;i>=0;i--)
            {
                var animal=animals[i];if(!animal){animals.RemoveAt(i);continue;}
                animal.gameObject.SetActive(active);
            }
        }
    }
}
