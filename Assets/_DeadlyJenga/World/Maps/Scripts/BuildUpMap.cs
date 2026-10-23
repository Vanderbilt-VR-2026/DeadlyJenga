using System;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class BuildUpMap : MonoBehaviour,IWorldQuery,ISyncedWorld,IRebuildable,IMapFootprint,IBuildUpControls
    {
        public Scaffold scaffoldPrefab;
        public ScaffoldModule[] modulePrefabs;
        [Min(.25f)] public float cellSize=2;
        [Range(2,6)] public int tiers=4;
        [Range(4,12)] public int tierHeightCells=8;
        [Range(5,90)] public float climbSeconds=20;
        [Range(1,10)] public float deliverySeconds=4;
        public float waterHeight=.08f;
        public Transform foundation;
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly List<Scaffold> scaffolds=new List<Scaffold>();
        readonly List<ScaffoldModule> modules=new List<ScaffoldModule>();
        Transform generated;
        int stage,phase;
        float phaseAt;
        bool replica;
        public int Revision{get;private set;}
        public Transform Root=>transform;
        public IReadOnlyList<Rigidbody> Blocks=>bodies;
        public Vector3 Center=>transform.position;
        public float TopHeight=>transform.position.y+stage*tierHeightCells*cellSize+14;
        public float Radius=>24;
        public float StepHeight=>cellSize;
        public Bounds PlayableBounds=>new Bounds(Center+Vector3.up*(tiers*tierHeightCells*cellSize*.5f),new Vector3(48,tiers*tierHeightCells*cellSize+56,48));
        public bool Available=>true;
        public int Stage=>stage;
        public int StageCount=>tiers;
        public bool Delivering=>phase==1;
        public float ClimbSecondsRemaining=>phase==2?Mathf.Max(0,climbSeconds-(Time.time-phaseAt)):0;
        public bool CanAdvance=>!replica&&(!(Services.Session is IWorldAuthority authority)||authority.HasWorldAuthority)&&phase!=1&&phase!=2&&stage<tiers-1;
        public IReadOnlyList<Scaffold> Scaffolds=>scaffolds;
        public float WaterHeight(Vector3 position)=>waterHeight;
        public bool IsSheltered(Vector3 position)=>Physics.Raycast(position+Vector3.up*.1f,Vector3.up,500,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        void OnEnable(){Services.World=this;}
        void OnDisable(){if(ReferenceEquals(Services.World,this))Services.World=null;}
        void Start(){if(!generated)Rebuild();}
        public void Rebuild()
        {
            if(!scaffoldPrefab||modulePrefabs==null||modulePrefabs.Length==0)throw new InvalidOperationException("Assign scaffold and building module prefabs.");
            if(generated){generated.gameObject.SetActive(false);if(Application.isPlaying)Destroy(generated.gameObject);else DestroyImmediate(generated.gameObject);}
            bodies.Clear();scaffolds.Clear();modules.Clear();stage=phase=0;phaseAt=Time.time;
            generated=new GameObject("Generated build-up grid").transform;generated.SetParent(transform,false);
            for(int level=0;level<tiers;level++)
            {
                int y=level*tierHeightCells;
                AddScaffold(new Vector3Int(-6,y,-1),new Vector3Int(12,1,2));
                AddScaffold(new Vector3Int(-1,y,-6),new Vector3Int(2,1,12));
                // Broad socket pads keep modules entirely supported, even when player collision is off.
                AddScaffold(new Vector3Int(-4,y,-4),new Vector3Int(4,1,4));
                AddScaffold(new Vector3Int(0,y,0),new Vector3Int(4,1,4));
                if(level<tiers-1)foreach(var corner in new[]{new Vector2Int(-5,-5),new Vector2Int(4,4),new Vector2Int(-5,4),new Vector2Int(4,-5)})
                {
                    AddScaffold(new Vector3Int(corner.x,y+1,corner.y),new Vector3Int(1,tierHeightCells-1,1));
                    int restX=corner.x<0?corner.x:corner.x-2,restZ=corner.y<0?corner.y:corner.y-2;
                    // Keep the outside climbing faces flush; outward collars would trap climbers under an overhang.
                    for(int h=2;h<=tierHeightCells;h+=2)AddScaffold(new Vector3Int(restX,y+h,restZ),new Vector3Int(3,1,3));
                }
                for(int slot=0;slot<2;slot++)
                {
                    var module=Instantiate(modulePrefabs[(level+slot)%modulePrefabs.Length],generated);
                    module.socket=new Vector3Int(slot==0?-4:0,y+1,slot==0?-4:0);
                    module.Place((Vector3)module.socket*cellSize);module.Initialize();
                    module.Structure.Changed+=Changed;
                    foreach(var block in module.Blocks)bodies.Add(block.Body);
                    modules.Add(module);module.gameObject.SetActive(level==0);
                }
            }
            SetGrates(false);Physics.SyncTransforms();Revision++;
        }
        void Changed()=>Revision++;
        void AddScaffold(Vector3Int origin,Vector3Int dimensions)
        {
            var scaffold=Instantiate(scaffoldPrefab,generated);scaffold.name="Scaffold "+origin;
            scaffold.cellSize=cellSize;scaffold.gridOrigin=origin;scaffold.dimensions=dimensions;scaffold.Configure();scaffolds.Add(scaffold);
        }
        void SetGrates(bool solid){foreach(var scaffold in scaffolds)scaffold.SetPlayerSolid(solid);}
        public void AdvanceStage()
        {
            if(!CanAdvance)return;stage++;phase=1;phaseAt=Time.time;SetGrates(true);
            for(int slot=0;slot<2;slot++)
            {
                var module=modules[stage*2+slot];module.gameObject.SetActive(true);module.SetFlight(true,replica);
                module.Place((Vector3)module.socket*cellSize+Vector3.up*24);
            }
            Physics.SyncTransforms();Revision++;
        }
        void Update()
        {
            if(replica)return;
            if(phase==1)
            {
                float t=Mathf.Clamp01((Time.time-phaseAt)/deliverySeconds),ease=t*t*(3-2*t);
                for(int slot=0;slot<2;slot++)
                {
                    var module=modules[stage*2+slot];module.Place((Vector3)module.socket*cellSize+Vector3.up*(24*(1-ease)));
                }
                if(t>=1){phase=2;phaseAt=Time.time;foreach(var module in modules)module.SetFlight(false,replica);Revision++;}
            }
            if(phase==2&&ClimbSecondsRemaining<=0){phase=3;SetGrates(false);Revision++;}
        }
        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();Collider best=null;float top=float.NegativeInfinity;
            // Never respawn a player onto a module that is still arriving.
            for(int i=0;i<modules.Count;i++)
            {
                if(i/2>stage||phase==1&&i/2==stage)continue;
                foreach(var block in modules[i].Blocks){var col=block.GetComponent<Collider>();if(block.gameObject.activeInHierarchy&&col.bounds.max.y>top){top=col.bounds.max.y;best=col;}}
            }
            return best?new Vector3(best.bounds.center.x,best.bounds.max.y+.08f,best.bounds.center.z):Center+Vector3.up*4;
        }
        [Serializable] public sealed class ModuleState{public int[] broken;}
        [Serializable] public sealed class State
        {
            public int version=1,tiers,height,stage,phase;
            public float cell,climb,delivery,elapsed;
            public ModuleState[] modules;
        }
        const string Prefix="buildup-grid-v1:";
        State Snapshot()
        {
            var state=new State{tiers=tiers,height=tierHeightCells,cell=cellSize,climb=climbSeconds,delivery=deliverySeconds,stage=stage,phase=phase,elapsed=Time.time-phaseAt,modules=new ModuleState[modules.Count]};
            for(int i=0;i<modules.Count;i++)state.modules[i]=new ModuleState{broken=modules[i].Structure.SaveBroken()};return state;
        }
        public string SaveLayout()=>Prefix+JsonUtility.ToJson(Snapshot());
        public bool LoadLayout(string text,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(text==null||!text.StartsWith(Prefix,StringComparison.Ordinal)||text.Length>65536||scale!=Vector3.one)return false;
            State state;try{state=JsonUtility.FromJson<State>(text.Substring(Prefix.Length));}catch(ArgumentException){return false;}
            if(state==null||state.version!=1||state.tiers!=tiers||state.height!=tierHeightCells||state.cell!=cellSize||state.stage<0||state.stage>=tiers||state.phase<0||state.phase>3||state.modules==null||state.modules.Length!=tiers*2||!float.IsFinite(state.elapsed)||state.elapsed<0||!float.IsFinite(state.climb)||state.climb<5||state.climb>90||!float.IsFinite(state.delivery)||state.delivery<1||state.delivery>10)return false;
            int count=0;
            for(int i=0;i<state.modules.Length;i++)
            {
                int size=modulePrefabs[(i/2+i%2)%modulePrefabs.Length].GetComponentsInChildren<CastleBlock>(true).Length;
                if(state.modules[i]?.broken==null)return false;foreach(int index in state.modules[i].broken)if(index<0||index>=size)return false;count+=size;
            }
            if(count!=expectedBlocks)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);Rebuild();replica=true;
            stage=state.stage;phase=state.phase;climbSeconds=state.climb;deliverySeconds=state.delivery;phaseAt=Time.time-state.elapsed;
            for(int i=0;i<modules.Count;i++)
            {
                var module=modules[i];module.Structure.Restore(state.modules[i].broken);module.gameObject.SetActive(i/2<=stage);module.SetFlight(phase==1&&i/2==stage,true);
            }
            SetGrates(phase==1||phase==2);return true;
        }
        public void SetReplica(bool value)
        {
            replica=value;
            for(int i=0;i<modules.Count;i++)modules[i].SetFlight(phase==1&&i/2==stage,value);
        }
        public int FootprintVersion=>1;
        public float FootprintHalfSize=>22;
        public void GetGroundFootprints(List<Footprint> into)=>into.Add(new Footprint{center=Vector2.zero,halfSize=Vector2.one*20,rotation=Quaternion.identity});
    }
}
