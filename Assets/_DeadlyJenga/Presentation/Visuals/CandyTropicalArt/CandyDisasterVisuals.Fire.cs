using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyDisasterVisuals
    {
        Mesh fireCube;Material fireShell,fireTongue,charredMaterial,smoulderMaterial;
        readonly List<Matrix4x4> fireShells=new List<Matrix4x4>(512),fireTips=new List<Matrix4x4>(384);
        readonly List<Matrix4x4> smoulders=new List<Matrix4x4>(192);
        sealed class CharredLook
        {
            public Renderer renderer;public Material[] originals,replacements;public MaterialPropertyBlock originalsProps;
            public Color color;public float burn,heat,appliedBurn=-1,appliedHeat=-1;
        }
        readonly Dictionary<Renderer,CharredLook> charred=new Dictionary<Renderer,CharredLook>();
        readonly Dictionary<Rigidbody,Renderer[]> burnRenderers=new Dictionary<Rigidbody,Renderer[]>();
        readonly HashSet<Renderer> seenCharred=new HashSet<Renderer>();
        readonly List<Renderer> oldCharred=new List<Renderer>();
        MaterialPropertyBlock burnProps;IWorldQuery fireWorld;Rigidbody fireFirst;
        void InitializeFire()
        {
            fireCube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            fireShell=ObjectMaterial("CandyBlockFire",Color.white);
            fireTongue=new Material(fireShell){name="Candy flame tongues",enableInstancing=true};fireTongue.SetFloat("_Tongue",1);fireTongue.SetFloat("_Cull",0);
            charredMaterial=ObjectMaterial("CandyCharredBlock",Color.white);
            smoulderMaterial=ObjectMaterial("CandyDisasterSmoke",new Color(.24f,.23f,.22f,.19f));burnProps=new MaterialPropertyBlock();
        }
        void BeginFire()
        {
            var world=Services.World;var first=world!=null&&world.Blocks.Count>0?world.Blocks[0]:null;
            if(!ReferenceEquals(fireWorld,world)||fireFirst!=first){ClearCharred();fireWorld=world;fireFirst=first;}
            fireShells.Clear();fireTips.Clear();smoulders.Clear();seenCharred.Clear();
        }
        void FireSurfaces(DisasterFrame frame)
        {
            var world=Services.World;if(world==null)return;
            float lag=Mathf.Clamp(Time.time-frame.sampledAt,0,.35f);
            float lifetime=frame.values!=null&&frame.values.Length>5?Mathf.Max(1,frame.values[5]):30;
            foreach(var burn in frame.burns)
            {
                if(burn.block<0||burn.block>=world.Blocks.Count)continue;
                var body=world.Blocks[burn.block];if(!body||!body.gameObject.activeInHierarchy)continue;
                float remaining=Mathf.Max(0,burn.flameRemaining-lag),smokeRemaining=Mathf.Max(0,burn.smokeRemaining-lag);
                if(remaining>0&&!frame.residualOnly)Engulf(body,burn.block);
                float amount=remaining>0?Mathf.Clamp01((1-remaining/lifetime)*1.5f):1;
                if(!burnRenderers.TryGetValue(body,out var renderers))
                {
                    var visual=body.GetComponent<IClimbable>()?.Visual;
                    renderers=visual?visual.GetComponentsInChildren<Renderer>(true):System.Array.Empty<Renderer>();burnRenderers[body]=renderers;
                }
                foreach(var renderer in renderers)
                {
                    if(!renderer||!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
                    seenCharred.Add(renderer);
                    if(!charred.TryGetValue(renderer,out var look))charred[renderer]=look=new CharredLook{renderer=renderer};
                    look.burn=amount;look.heat=remaining>0?1:smokeRemaining>0?.08f:0;
                }
                var view=Services.Session?.ActiveCamera;
                if(smokeRemaining>0&&smoulders.Count<192&&(!view||(view.transform.position-body.position).sqrMagnitude<3025))
                {
                    var collider=body.GetComponent<Collider>();if(!collider)continue;var bounds=collider.bounds;
                    float phase=Mathf.Repeat(Time.time*.28f+Hash(burn.block+271),1),width=Mathf.Clamp(bounds.size.magnitude*.15f,.3f,1.3f);
                    float fade=Mathf.Clamp01(smokeRemaining/3);
                    Puff(smoulders,bounds.center+Vector3.up*(bounds.extents.y+.2f+phase*2.5f),width*(.6f+phase)*fade,width*(1+phase)*fade,burn.block*31);
                }
            }
        }
        void Engulf(Rigidbody body,int id)
        {
            var collider=body.GetComponent<Collider>();if(!collider)return;
            var bounds=collider.bounds;
            if(collider is BoxCollider box)
            {
                var scale=box.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                fireShells.Add(Matrix4x4.TRS(box.transform.TransformPoint(box.center),box.transform.rotation,Vector3.Scale(box.size,scale)+Vector3.one*.14f));
            }
            else fireShells.Add(Matrix4x4.TRS(bounds.center,Quaternion.identity,bounds.size+Vector3.one*.14f));
            var view=Services.Session?.ActiveCamera;
            if(view&&(view.transform.position-bounds.center).sqrMagnitude>3025)return;
            float width=Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.24f,.35f,1.8f);
            float perimeter=2*(bounds.size.x+bounds.size.z);int tips=Mathf.Clamp(Mathf.CeilToInt(perimeter/.85f),12,32);
            for(int j=0;j<tips&&fireTips.Count<512;j++)
            {
                float distance=Mathf.Repeat((j+Hash(id)*.3f)/tips*perimeter,perimeter);
                Vector3 edge;
                if(distance<bounds.size.x)edge=new Vector3(-bounds.extents.x+distance,0,-bounds.extents.z);
                else if((distance-=bounds.size.x)<bounds.size.z)edge=new Vector3(bounds.extents.x,0,-bounds.extents.z+distance);
                else if((distance-=bounds.size.z)<bounds.size.x)edge=new Vector3(bounds.extents.x-distance,0,bounds.extents.z);
                else edge=new Vector3(-bounds.extents.x,0,bounds.extents.z-(distance-bounds.size.x));
                float phase=Mathf.Repeat(Time.time*(1.1f+Hash(j+id)*.7f)+Hash(id*13+j),1);
                float height=Mathf.Max(.8f,bounds.size.y*.9f+width*1.7f)*(.75f+Hash(id*31+j)*.5f);
                var p=bounds.center+edge*1.01f+Vector3.up*(-bounds.extents.y+height*.38f+phase*.35f);
                fireTips.Add(Matrix4x4.TRS(p,billboard,new Vector3(width*(.8f+.2f*Mathf.Sin(Time.time*7+j)),height,1)));
            }
            if(smokePuffs.Count<512)
            {
                float phase=Mathf.Repeat(Time.time*.28f+Hash(id+81),1);
                Puff(smokePuffs,bounds.center+Vector3.up*(bounds.extents.y+phase*3),width*(1+phase),width*(1.4f+phase),id*31);
            }
        }
        void RenderFire()
        {
            oldCharred.Clear();
            foreach(var pair in charred)
            {
                var renderer=pair.Key;var look=pair.Value;
                if(!renderer||!seenCharred.Contains(renderer)){RestoreCharred(look);oldCharred.Add(renderer);continue;}
                bool replaced=renderer.sharedMaterial!=charredMaterial;
                if(replaced)
                {
                    look.originals=renderer.sharedMaterials;look.originalsProps=new MaterialPropertyBlock();renderer.GetPropertyBlock(look.originalsProps);
                    var original=renderer.sharedMaterial;look.color=original&&original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):Color.white;
                    if(look.originalsProps.HasColor("_BaseColor"))look.color=look.originalsProps.GetColor("_BaseColor");
                    look.replacements=new Material[look.originals.Length];for(int i=0;i<look.replacements.Length;i++)look.replacements[i]=charredMaterial;
                    renderer.sharedMaterials=look.replacements;
                }
                if(replaced||Mathf.Abs(look.appliedBurn-look.burn)>.004f||Mathf.Abs(look.appliedHeat-look.heat)>.004f)
                {
                    renderer.GetPropertyBlock(burnProps);burnProps.SetColor("_BaseColor",look.color);burnProps.SetFloat("_BurnAmount",look.burn);burnProps.SetFloat("_Heat",look.heat);renderer.SetPropertyBlock(burnProps);
                    look.appliedBurn=look.burn;look.appliedHeat=look.heat;
                }
            }
            foreach(var renderer in oldCharred)charred.Remove(renderer);
            DrawObjects(fireShells,fireCube,fireShell);DrawObjects(fireTips,quad,fireTongue);DrawObjects(smoulders,quad,smoulderMaterial);
        }
        void RestoreCharred(CharredLook look)
        {
            if(look.renderer&&look.renderer.sharedMaterial==charredMaterial&&look.originals!=null)
            {look.renderer.sharedMaterials=look.originals;look.renderer.SetPropertyBlock(look.originalsProps);}
        }
        void ClearCharred(){foreach(var look in charred.Values)RestoreCharred(look);charred.Clear();burnRenderers.Clear();seenCharred.Clear();}
        public void ReleaseFireSurfaces()=>ClearCharred();
        void DestroyFire(){ClearCharred();foreach(var material in new[]{fireShell,fireTongue,charredMaterial,smoulderMaterial})if(material)Destroy(material);}
    }
}
