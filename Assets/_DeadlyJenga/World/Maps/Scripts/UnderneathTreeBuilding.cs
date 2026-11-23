using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Each storey keeps its network body slot. Its hulls and art can belong to that
    // slot or to the shared shell, including on a late-joining replica.
    public sealed class UnderneathTreeBuilding : MonoBehaviour
    {
        sealed class Section
        {
            public UnderneathTreeGiftPanel panel;
            public Transform contents;
            public Collider spine;
            public Renderer finish;
            public Vector3 shellPosition;
            public Quaternion shellRotation;
            public Vector3 shellScale;
        }

        readonly List<Section> sections = new List<Section>();
        UnderneathTreeGiftPanel shell;
        MeshCollider rug;
        UnderneathTreeMap map;
        Transform floor;
        Vector3 floorPoint, floorNormal;
        bool boundTogether, retired;
        float invalidSeconds;
        public bool Together => shell && shell.gameObject.activeSelf;
        public bool Retired => retired;
        public bool CanScore => shell && Authority && !retired && floor && floor.gameObject.activeInHierarchy;
        public Vector3 CapturePoint => floor ? floor.TransformPoint(floorPoint) : transform.position;
        bool Authority => !map.IsBuildingReplica && !(Services.Session is IWorldAuthority a && !a.HasWorldAuthority);

        internal void Configure(UnderneathTreeMap owner, UnderneathTreeGiftPanel compound, MeshCollider skirt,
            List<Rigidbody> bodies, string floorSection, string floorHull)
        {
            map=owner; shell=compound; rug=skirt;
            foreach(var body in bodies)
            {
                var part=body.transform;
                var contents=new GameObject("Building section contents").transform;
                contents.SetParent(part,false);
                for(int i=part.childCount-1;i>=0;i--)
                    if(part.GetChild(i)!=contents)part.GetChild(i).SetParent(contents,false);
                // The section spine remains on its original body for block queries.
                // This small copy supplies that same spine when the original body is inactive.
                var source=part.GetComponent<MeshCollider>();
                var spine=contents.gameObject.AddComponent<MeshCollider>();
                spine.sharedMesh=source.sharedMesh; spine.convex=true; spine.enabled=false;
                contents.gameObject.layer=part.gameObject.layer;
                contents.gameObject.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var finish=contents.gameObject.AddComponent<MeshRenderer>();
                finish.sharedMaterials=part.GetComponent<MeshRenderer>().sharedMaterials;finish.enabled=false;
                contents.SetParent(shell.transform,true);
                sections.Add(new Section{panel=body.GetComponent<UnderneathTreeGiftPanel>(),contents=contents,
                    spine=spine,finish=finish,shellPosition=contents.localPosition,
                    shellRotation=contents.localRotation,shellScale=contents.localScale});
                contents.SetParent(part,false);
                contents.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);contents.localScale=Vector3.one;
                Physics.SyncTransforms();
                if(!part.name.StartsWith(floorSection,StringComparison.Ordinal))continue;
                foreach(var hull in contents.GetComponentsInChildren<MeshCollider>())
                {
                    if(hull.name!=floorHull)continue;
                    floor=hull.transform;
                    var top=hull.bounds.center;top.y=hull.bounds.max.y;
                    floorPoint=floor.InverseTransformPoint(top);
                    floorNormal=floor.InverseTransformDirection(Vector3.up);
                    break;
                }
            }
            if(!floor)
            {
                retired=true;
                Debug.LogError(name+" is missing capture floor "+floorSection+" / "+floorHull,this);
            }
        }

        void Bind(bool together)
        {
            foreach(var section in sections)
            {
                var t=section.contents;
                t.SetParent(together?shell.transform:section.panel.transform,false);
                t.SetLocalPositionAndRotation(together?section.shellPosition:Vector3.zero,together?section.shellRotation:Quaternion.identity);
                t.localScale=together?section.shellScale:Vector3.one;
                section.spine.enabled=together;section.finish.enabled=together;
            }
            boundTogether=together;
        }

        // Reassembly is permitted only before any section has been displaced.
        public bool SetTogether(bool together)
        {
            if(!Authority)return false;
            if(together==Together)return true;
            if(together)
            {
                foreach(var s in sections)
                    if(!s.panel.gameObject.activeSelf || s.panel.IsLoose || !s.panel.IsAtWrappedPose())return false;
                if(shell.IsLoose)return false;
                shell.gameObject.SetActive(true);
                Bind(true);
                foreach(var s in sections)s.panel.gameObject.SetActive(false);
            }
            else
            {
                Vector3 center=shell.Body.worldCenterOfMass;
                Vector3 velocity=shell.Body.linearVelocity, spin=shell.Body.angularVelocity;
                foreach(var s in sections)
                {
                    s.panel.gameObject.SetActive(true);
                    s.panel.transform.SetPositionAndRotation(s.contents.position,s.contents.rotation);
                    s.panel.Body.position=s.contents.position;
                    s.panel.Body.rotation=s.contents.rotation;
                }
                Bind(false);
                Physics.SyncTransforms();
                var toy=GetComponent<UnderneathTreeToy>();
                toy.IgnoreOverlaps(toy.Pieces());
                if(shell.IsLoose)
                    foreach(var s in sections)
                    {
                        s.panel.Release(Vector3.zero);
                        s.panel.Body.linearVelocity=velocity+Vector3.Cross(spin,s.panel.Body.worldCenterOfMass-center);
                        s.panel.Body.angularVelocity=spin;
                    }
                shell.gameObject.SetActive(false);
            }
            return true;
        }

        [ContextMenu("Keep intact as one body")]
        void Group() { if(Application.isPlaying && shell)SetTogether(true); }
        [ContextMenu("Enable breakaway sections")]
        void Ungroup() { if(Application.isPlaying && shell)SetTogether(false); }

        // Called by a scripted elimination after its warning, independently of debris.
        public void Retire() { if(Authority)retired=true; }

        public bool Demolish(Vector3 impulse)
        {
            if(!Authority || !SetTogether(false))return false;
            Retire();
            foreach(var s in sections)
            {
                if(!s.panel.gameObject.activeSelf)continue;
                if(!s.panel.IsLoose)s.panel.Release(impulse);
                else s.panel.Body.AddForce(impulse,ForceMode.VelocityChange);
            }
            return true;
        }

        [ContextMenu("Retire zone and demolish building")]
        void DemolishInPlay(){ if(Application.isPlaying && shell)Demolish(Vector3.up*3); }

        void LateUpdate()
        {
            if(shell && Together!=boundTogether)Bind(Together);
        }

        void FixedUpdate()
        {
            if(!shell || !Authority || retired)return;
            if(Together!=boundTogether)Bind(Together);
            if(!floor || !floor.gameObject.activeInHierarchy){retired=true;return;}
            var p=CapturePoint;
            bool valid=Vector3.Dot(floor.TransformDirection(floorNormal),Vector3.up)>=.7071f;
            if(valid && rug)
            {
                var ray=new Ray(new Vector3(p.x,rug.bounds.max.y+2,p.z),Vector3.down);
                valid=rug.Raycast(ray,out var ground,rug.bounds.size.y+4) && p.y>=ground.point.y-.5f;
            }
            else if(!rug)valid=false;
            invalidSeconds=valid?0:invalidSeconds+Time.fixedDeltaTime;
            if(invalidSeconds>=2)retired=true;
        }
    }
}
