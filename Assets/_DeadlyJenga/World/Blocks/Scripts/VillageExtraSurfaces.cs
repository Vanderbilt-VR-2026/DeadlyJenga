using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Match the normal block's survivor-only collision copies for compound furniture and window bays.
    [RequireComponent(typeof(CastleBlock))]
    public sealed class VillageExtraSurfaces : MonoBehaviour
    {
        readonly List<BoxCollider> sources=new List<BoxCollider>(),copies=new List<BoxCollider>();
        readonly List<CastleSurfaceGroup> groups=new List<CastleSurfaceGroup>();
        Vector3 position,scale;Quaternion rotation;bool synced;
        void Awake()
        {
            if(!Application.isPlaying)return;var block=GetComponent<CastleBlock>();var shapes=GetComponents<BoxCollider>();
            for(int i=1;i<shapes.Length;i++)
            {
                var group=CastleSurfaceGroup.Acquire(gameObject.scene,transform.position);
                var go=new GameObject(name+" [compound survivor surface]");go.layer=Layers.Surface;go.transform.SetParent(group.transform,false);
                var copy=go.AddComponent<BoxCollider>();copy.excludeLayers=~(1<<Layers.Survivor);copy.layerOverridePriority=10;
                Climbables.Register(copy,block);sources.Add(shapes[i]);copies.Add(copy);groups.Add(group);
            }
            Sync();
        }
        void OnEnable(){foreach(var c in copies)if(c)c.gameObject.SetActive(true);}
        void OnDisable(){foreach(var c in copies)if(c)c.gameObject.SetActive(false);}
        void LateUpdate()=>Sync();
        public void Sync()
        {
            if(synced&&position==transform.position&&rotation==transform.rotation&&scale==transform.lossyScale)return;
            for(int i=0;i<copies.Count;i++)if(copies[i])
            {copies[i].transform.SetPositionAndRotation(transform.position,transform.rotation);copies[i].transform.localScale=transform.lossyScale;copies[i].center=sources[i].center;copies[i].size=sources[i].size;}
            position=transform.position;rotation=transform.rotation;scale=transform.lossyScale;synced=true;CastleSurfaceGroup.Changed();
        }
        void OnDestroy()
        {
            for(int i=0;i<copies.Count;i++)
            {if(copies[i]){Climbables.Unregister(copies[i]);Destroy(copies[i].gameObject);}if(groups[i])groups[i].Release();}
        }
    }
}
