using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Author children as prefab blocks. Every connection is built once, in the module's local space.
    public sealed class ScaffoldModule : MonoBehaviour
    {
        public Vector2Int footprintCells=new Vector2Int(4,4);
        public Vector3Int socket;
        public MagneticStructure Structure {get;private set;}
        CastleBlock[] blocks;
        public CastleBlock[] Blocks=>blocks??(blocks=GetComponentsInChildren<CastleBlock>(true));
        public void Initialize()
        {
            if(Structure)return;
            Structure=gameObject.AddComponent<MagneticStructure>();
            foreach(var block in Blocks)
            {
                var magnet=block.GetComponent<MagneticBlock>();Structure.Add(magnet);
                block.Body.mass=40;block.RefreshAppearance();
            }
            for(int a=0;a<Blocks.Length;a++)for(int b=a+1;b<Blocks.Length;b++)
            {
                var ac=Blocks[a].GetComponent<BoxCollider>();var bc=Blocks[b].GetComponent<BoxCollider>();
                var ap=new MagneticDraftMap.Piece(transform.InverseTransformPoint(Blocks[a].transform.position),Vector3.Scale(ac.size,Blocks[a].transform.localScale),1);
                var bp=new MagneticDraftMap.Piece(transform.InverseTransformPoint(Blocks[b].transform.position),Vector3.Scale(bc.size,Blocks[b].transform.localScale),1);
                if(MagneticDraftMap.Touches(ap,bp))Structure.Connect(a,b);
            }
            Structure.Recompute();
        }
        public void SetFlight(bool flying,bool replica)
        {
            Structure.SetReplica(flying||replica);
            foreach(var block in Blocks)block.SetTraversalEnabled(!flying);
        }
        public void Place(Vector3 localPosition)
        {
            transform.localPosition=localPosition;
            foreach(var block in Blocks)
            {block.Body.position=block.transform.position;block.Body.rotation=block.transform.rotation;block.SyncSurface();}
        }
    }
}
