using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    [DisallowMultipleComponent,RequireComponent(typeof(BoxCollider),typeof(Rigidbody))]
    public sealed class Scaffold : MonoBehaviour, IScaffold, IClimbable
    {
        [Min(.25f)] public float cellSize=2;
        public Vector3Int gridOrigin;
        public Vector3Int dimensions=new Vector3Int(4,1,4);
        [SerializeField] bool playerSolid;
        [SerializeField] Transform visual;
        BoxCollider blockCollider,playerCollider;
        Rigidbody body;
        public Transform Visual=>visual;
        public Vector3Int Cells=>dimensions;
        public float CellSize=>cellSize;
        public bool PlayerSolid=>playerSolid;
        public bool CanClimb=>isActiveAndEnabled&&playerSolid;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>body?body:body=GetComponent<Rigidbody>();
        public Vector3 Socket(Vector3Int cell)=>transform.TransformPoint((Vector3)cell*cellSize);
        void Awake(){Configure();}
        void OnEnable(){if(playerCollider)playerCollider.gameObject.SetActive(true);}
        void OnDisable(){if(playerCollider)playerCollider.gameObject.SetActive(false);}
        void OnValidate()
        {
            cellSize=Mathf.Max(.25f,cellSize);
            dimensions=new Vector3Int(Mathf.Clamp(dimensions.x,1,64),Mathf.Clamp(dimensions.y,1,64),Mathf.Clamp(dimensions.z,1,64));
            transform.localScale=Vector3.one;transform.localPosition=(Vector3)gridOrigin*cellSize;
            transform.localRotation=Quaternion.Euler(0,Mathf.Round(transform.localEulerAngles.y/90)*90,0);
            var box=GetComponent<BoxCollider>();if(box){box.size=(Vector3)dimensions*cellSize;box.center=box.size*.5f;}
            var fallback=visual?visual.Find("Gray-box fallback"):null;
            if(fallback){fallback.localPosition=(Vector3)dimensions*cellSize*.5f;fallback.localScale=(Vector3)dimensions*cellSize;}
        }
        public void Configure()
        {
            OnValidate();body=Body;body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.None;
            gameObject.layer=Layers.Block;blockCollider=GetComponent<BoxCollider>();blockCollider.isTrigger=false;
            blockCollider.excludeLayers=~((1<<Layers.Block)|(playerSolid?1:0));blockCollider.layerOverridePriority=20;
            if(!visual){visual=new GameObject("Visual").transform;visual.SetParent(transform,false);}
            if(Application.isPlaying&&!playerCollider)
            {
                var go=new GameObject("Scaffold survivor collision");go.transform.SetParent(transform,false);go.layer=Layers.Surface;
                var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
                playerCollider=go.AddComponent<BoxCollider>();playerCollider.excludeLayers=~(1<<Layers.Survivor);playerCollider.layerOverridePriority=20;
                Climbables.Register(playerCollider,this);
            }
            if(playerCollider){playerCollider.center=blockCollider.center;playerCollider.size=blockCollider.size;playerCollider.enabled=playerSolid;}
            if(body){body.position=transform.position;body.rotation=transform.rotation;}
        }
        public void SetPlayerSolid(bool solid)
        {
            playerSolid=solid;
            if(blockCollider)blockCollider.excludeLayers=~((1<<Layers.Block)|(solid?1:0));
            if(playerCollider)playerCollider.enabled=solid;
        }
        [ContextMenu("Toggle grate / beacon")]
        void Toggle()=>SetPlayerSolid(!playerSolid);
        void OnDestroy(){if(playerCollider)Climbables.Unregister(playerCollider);}
    }
}
