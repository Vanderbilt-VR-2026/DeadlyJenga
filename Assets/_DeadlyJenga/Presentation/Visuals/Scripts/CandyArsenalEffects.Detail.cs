using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyArsenalEffects
    {
        sealed class Residue {public Transform root;public float born,life;public Vector3 from,to;public bool spike;}
        Material detailGlow;
        readonly List<Residue> residues=new List<Residue>();
        void LateUpdate()
        {
            for(int i=residues.Count-1;i>=0;i--)
            {
                var r=residues[i];if(!r.root){residues.RemoveAt(i);continue;}float age=Time.time-r.born;
                if(age>=r.life){Destroy(r.root.gameObject);residues.RemoveAt(i);continue;}
                if(r.spike){r.root.position=Vector3.Lerp(r.from,r.to,Mathf.Clamp01(age/.065f));float fade=Mathf.Clamp01((r.life-age)/.45f);r.root.localScale=new Vector3(.035f,.22f,.035f)*fade;}
                else r.root.localScale=Vector3.one*Mathf.Lerp(.2f,r.life>1?16:7,Mathf.Clamp01(age/r.life));
            }
        }
        void Keep(Residue r){if(residues.Count>=96){if(residues[0].root)Destroy(residues[0].root.gameObject);residues.RemoveAt(0);}residues.Add(r);}
        Transform Visual(string name,Mesh mesh,Vector3 position,Color color)
        {
            if(!detailGlow){detailGlow=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));detailGlow.SetFloat("_Surface",1);detailGlow.SetFloat("_Blend",0);detailGlow.SetFloat("_SrcBlend",5);detailGlow.SetFloat("_DstBlend",10);detailGlow.SetFloat("_ZWrite",0);detailGlow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");detailGlow.renderQueue=3000;}
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.transform.position=position;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=detailGlow;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);go.GetComponent<MeshRenderer>().SetPropertyBlock(block);return go.transform;
        }
        void Spike(EquipmentActionFrame action)
        {
            var end=action.endpoints[0];var spike=Visual("Visual - steaming neon spike",Resources.GetBuiltinResource<Mesh>("Capsule.fbx"),action.position,new Color(.3f,1,.02f));spike.rotation=Quaternion.FromToRotation(Vector3.up,(end-action.position).sqrMagnitude>.001f?(end-action.position).normalized:Vector3.forward);
            var steam=spike.gameObject.AddComponent<ParticleSystem>();steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=steam.main;main.startLifetime=.3f;main.startSpeed=.2f;main.startSize=.05f;main.maxParticles=6;main.startColor=new Color(.45f,1,.15f,.35f);var emission=steam.emission;emission.rateOverTime=8;steam.GetComponent<ParticleSystemRenderer>().sharedMaterial=glow;steam.Play();
            Keep(new Residue{root=spike,born=Time.time,life=1.6f,from=action.position,to=end,spike=true});
        }
        void BlastDetail(EquipmentActionFrame action)
        {
            bool electric=action.kind==EquipmentKind.GrenadeLauncher;
            var shell=Visual("Visual - blast shock front",Resources.GetBuiltinResource<Mesh>("Sphere.fbx"),action.position,electric?new Color(.25f,.1f,1,.10f):new Color(1,.45f,.12f,.08f));shell.localScale=Vector3.one*.2f;Keep(new Residue{root=shell,born=Time.time,life=electric?.6f:1.2f});
            if(!electric)return;
            for(int i=0;i<7;i++)
            {
                var go=new GameObject("Visual - electric fork");go.transform.SetParent(shell,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glow;line.useWorldSpace=false;line.positionCount=6;line.startWidth=.035f;line.endWidth=.003f;line.startColor=line.endColor=new Color(.5f,.55f,1);Vector3 direction=Quaternion.Euler(i*53,i*137,0)*Vector3.forward;
                for(int n=0;n<6;n++)line.SetPosition(n,direction*n*.09f+new Vector3(Mathf.Sin(n*5+i),Mathf.Cos(n*3+i),0)*.035f);
            }
        }
    }
}
