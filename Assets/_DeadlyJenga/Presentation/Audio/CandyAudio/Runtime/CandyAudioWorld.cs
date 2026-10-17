using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CandyArt;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CandyAudio
{
    // Scene discovery and ambience coordinator; observes the current demo only.
    public sealed class CandyAudioWorld : MonoBehaviour
    {
        public static CandyAudioWorld Instance {get;private set;}
        public float WaterHeight {get;private set;}=-.12f;
        public bool musicEnabled=true;
        CandyAudioBank ownedBank;
        ISessionInfo session;CandyOceanSurface ocean;float scanAt,windAt,exposure=1,tension;
        readonly System.Collections.Generic.Dictionary<Vector3Int,float> leanCells=new System.Collections.Generic.Dictionary<Vector3Int,float>();
        readonly System.Collections.Generic.Dictionary<Vector3Int,float> splashCells=new System.Collections.Generic.Dictionary<Vector3Int,float>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Instance)return;
            var go=new GameObject("Candy Audio — presentation only");DontDestroyOnLoad(go);
            var engine=go.AddComponent<CandyAudioEngine>();var bank=Resources.Load<CandyAudioBank>("CandyAudio/Default");var copy=bank?Instantiate(bank):null;engine.Configure(copy);go.AddComponent<CandyAudioWorld>().ownedBank=copy;go.AddComponent<CandyAudioStudio>();
        }
        void Awake(){Instance=this;}
        public static bool Near(Vector3 p,float range=80)
        {var e=CandyAudioEngine.Instance;return e&&e.Listener&&(e.Listener.position-p).sqrMagnitude<range*range;}
        void Update()
        {
            var engine=CandyAudioEngine.Instance;if(!engine||!engine.bank)return;
            if(Time.time>=scanAt)
            {
                scanAt=Time.time+.75f;
                session=Services.Session;var map=Services.World;
                if(!ocean)ocean=FindFirstObjectByType<CandyOceanSurface>();
                if(ocean)WaterHeight=ocean.transform.position.y;
                if(session==null||map==null)return;
                foreach(var body in map.Blocks)if(body&&!body.TryGetComponent<CandyBlockAudio>(out _))body.gameObject.AddComponent<CandyBlockAudio>();
                for(int i=0;i<Tracked.Movers.Count;i++)if(Tracked.Movers[i] is Component mover&&mover&&!mover.TryGetComponent<CandyPlayerAudio>(out _))mover.gameObject.AddComponent<CandyPlayerAudio>();
                for(int i=0;i<Tracked.Flyers.Count;i++)if(Tracked.Flyers[i] is Component flyer&&flyer&&!flyer.TryGetComponent<CandyHandAudio>(out _))flyer.gameObject.AddComponent<CandyHandAudio>();
                if(leanCells.Count>128)leanCells.Clear();if(splashCells.Count>128)splashCells.Clear();
            }
            if(session==null||!engine.Listener)return;
            // Desktop demo menu feedback. Future world-space buttons call the
            // public Play(UiClick, ..., local:true) API on their actual activation.
            if(session.MenuOpen&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)
                engine.Play(CandySound.UiClick,engine.Listener.position,.4f,GetInstanceID(),1,true);
            if(Time.time>=windAt)
            {
                windAt=Time.time+.55f;int blocked=0;var p=engine.Listener.position;
                const int mask=~((1<<Layers.Survivor)|(1<<Layers.Block));
                for(int i=0;i<4;i++)if(Physics.Raycast(p,Quaternion.Euler(0,i*90,0)*Vector3.forward,3.5f,mask,QueryTriggerInteraction.Ignore))blocked++;
                if(Physics.Raycast(p,Vector3.up,4,mask,QueryTriggerInteraction.Ignore))blocked++;
                exposure=1-blocked/5f;
            }
            float height=Mathf.Max(0,engine.Listener.position.y-WaterHeight);
            engine.Loop(CandySound.BeachWind,engine.Listener,Mathf.Lerp(.12f,.7f,exposure)*Mathf.Lerp(.7f,1,Mathf.Clamp01(height/35)),true,1,1-exposure);
            engine.Loop(CandySound.Surf,engine.Listener,Mathf.Lerp(.65f,.16f,Mathf.Clamp01(height/45)),true,1,(1-exposure)*.6f);
            if(musicEnabled)engine.Loop(CandySound.Music,transform,Mathf.Lerp(1,.78f,tension),true);
            tension=Mathf.MoveTowards(tension,0,Time.deltaTime*.1f);
        }
        public void Lean(Vector3 position,float amount)
        {
            var cell=Vector3Int.FloorToInt(position/12);
            if(leanCells.TryGetValue(cell,out float until)&&Time.time<until)return;
            leanCells[cell]=Time.time+3;tension=Mathf.Max(tension,amount);
            CandyAudioEngine.Instance.Play(CandySound.StructureLean,position,Mathf.Lerp(.4f,1,amount),cell.GetHashCode(),Mathf.Lerp(1.1f,.8f,amount));
        }
        public void Splash(Vector3 position,float strength,int owner,bool player=false)
        {
            position.y=WaterHeight;var cell=Vector3Int.FloorToInt(position/5);
            if(!player&&splashCells.TryGetValue(cell,out float until)&&Time.time<until)return;
            splashCells[cell]=Time.time+.13f;
            CandyAudioEngine.Instance.Play(player?CandySound.PlayerSplash:CandySound.BlockSplash,position,strength,owner,Mathf.Lerp(1.15f,.8f,strength));
        }
        void OnDestroy(){if(Instance==this)Instance=null;if(ownedBank)Destroy(ownedBank);}
    }
}
