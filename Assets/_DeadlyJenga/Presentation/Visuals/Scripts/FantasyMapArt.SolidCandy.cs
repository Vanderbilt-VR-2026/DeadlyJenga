using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Candy Sky Ring City look: solid glossy candy planks instead of glass, one color per district,
    // rope railings on bridges, roof railings and flags, ladders, shop awnings, the keep's battlements,
    // Downtown's chevrons and a chocolate fountain on the plaza vault. Art only, no colliders.
    // Bodies are named "index · site · purpose" by the map; the site name picks the district color.
    public sealed partial class FantasyMapArt
    {
        public bool solidCandy;

        static readonly Color Gold=new(1,.78f,.3f),Rope=new(.86f,.62f,.36f),RopeRed=new(.8f,.12f,.16f);
        readonly Dictionary<string,Material> solidMaterials=new();
        bool fountainBuilt;

        static (string site,string purpose) NameParts(Rigidbody body)
        {
            var parts=body.name.Split(new[]{" · "},System.StringSplitOptions.None);
            return parts.Length>=3?(parts[1],parts[2]):("","");
        }

        static Color DistrictColor(string site)
        {
            if(site.StartsWith("Sugar"))return new Color(1,.36f,.62f);
            if(site.StartsWith("Grape"))return new Color(.5f,.84f,.16f);
            if(site.StartsWith("Cloud Harbor")||site.StartsWith("Harbor"))return new Color(.62f,.36f,.96f);
            if(site.StartsWith("Downtown"))return new Color(1,.54f,.08f);
            if(site.StartsWith("Canyon"))return new Color(.08f,.72f,.66f);
            if(site.StartsWith("Plaza"))return new Color(1,.82f,.3f);
            return new Color(.94f,.88f,.97f);
        }

        Material SolidMaterial(Rigidbody body)
        {
            var (site,purpose)=NameParts(body);
            Color color;string key;
            if(purpose.Contains("crossing")){bool high=body.worldCenterOfMass.y-groundY>17;key=high?"bridge high":"bridge";color=high?new Color(.92f,.22f,.26f):new Color(.86f,.68f,.48f);}
            else if(purpose.Contains("trestle")){key="trestle";color=new Color(.74f,.56f,.4f);}
            else if(site.StartsWith("Harbor bascule")){key="bascule";color=new Color(.86f,.68f,.48f);}
            else{color=DistrictColor(site);key=color.ToString();}
            if(solidMaterials.TryGetValue(key,out var material)&&material)return material;
            material=new Material(cityCandyMaterial){name="Candy planks "+key};
            material.SetFloat("_Pattern",5);material.SetColor("_BaseColor",color);
            material.SetColor("_Accent",Color.Lerp(color,new Color(.25f,.1f,.2f),.62f));
            material.SetFloat("_Smoothness",.55f);material.SetFloat("_DetailStrength",0);material.SetFloat("_Glow",0);
            solidMaterials[key]=material;return material;
        }

        void ReleaseSolidMaterials(){foreach(var m in solidMaterials.Values)if(m)Release(m);solidMaterials.Clear();fountainBuilt=false;}

        void SolidDress(Rigidbody body,Transform visual)
        {
            var (site,purpose)=NameParts(body);
            var mesh=new ArtMesh(2);bool any=false;
            var boxes=body.GetComponentsInChildren<BoxCollider>();
            foreach(var shape in boxes)
            {
                var size=shape.size;var world=shape.transform.TransformPoint(shape.center);
                Vector3 P(Vector3 p)=>visual.InverseTransformPoint(shape.transform.TransformPoint(shape.center+p));
                var q=Quaternion.Inverse(visual.rotation)*shape.transform.rotation;
                var top=shape.transform.TransformPoint(shape.center+Vector3.up*size.y*.5f);
                bool flat=size.y<1.2f,uncovered=!Covered(top,body);
                if(purpose.Contains("crossing")&&flat&&Mathf.Max(size.x,size.z)>=2){RopeRails(mesh,size,P,q,body.worldCenterOfMass.y-groundY>17);any=true;continue;}
                if(site.StartsWith("Downtown")&&size.y>5&&Mathf.Min(size.x,size.z)>=4){Chevrons(mesh,size,P,q,world);any=true;}
                if(site.StartsWith("Plaza")&&flat&&uncovered&&Mathf.Min(size.x,size.z)>=8&&!fountainBuilt&&top.y>groundY+8)
                {fountainBuilt=true;Fountain(mesh,P(Vector3.up*size.y*.5f),1.7f);any=true;}
            }
            if(purpose.Contains(" floor ")){FloorDress(body,visual,site,purpose,mesh);any=true;}
            if(Landmark(body,visual,site,purpose,mesh))any=true;
            if(any)Model("Fantasy decorations",mesh,visual,new[]{cityCandyMaterial,cityGlowMaterial},true);
        }

        // Jenga tower floor: roof railing and flags on the top floor, a ladder on every floor, shop awnings on the ground floor.
        void FloorDress(Rigidbody body,Transform visual,string site,string purpose,ArtMesh mesh)
        {
            // The floor's own frame: the body transform (the map builds each floor around its first corner post).
            var frame=body.transform;var b=new Bounds();bool started=false;float deckTop=float.MinValue;
            foreach(var shape in body.GetComponentsInChildren<BoxCollider>())
            {
                var c=frame.InverseTransformPoint(shape.transform.TransformPoint(shape.center));
                var local=Quaternion.Inverse(frame.rotation)*shape.transform.rotation;
                var s=local*shape.size;s=new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
                var sb=new Bounds(c,s);if(!started){b=sb;started=true;}else b.Encapsulate(sb);
                if(shape.size.y>.6f&&shape.size.y<.8f)deckTop=Mathf.Max(deckTop,c.y+s.y*.5f);
            }
            if(!started)return;
            Vector3 V(Vector3 local)=>visual.InverseTransformPoint(frame.TransformPoint(local));
            var vq=Quaternion.Inverse(visual.rotation)*frame.rotation;
            float hx=b.extents.x,hz=b.extents.z;var mid=b.center;
            // Ladder on the +x face, a third of the way along, so ladders line up floor after floor.
            float ladderZ=mid.z+hz*.45f,face=mid.x+hx+.12f,floorY=b.min.y;
            for(int s=-1;s<=1;s+=2)mesh.Box(V(new Vector3(face,floorY+2,ladderZ+s*.42f)),new Vector3(.12f,4,.12f),vq,0,Gold);
            for(float y=.3f;y<4;y+=.42f)mesh.Box(V(new Vector3(face,floorY+y,ladderZ)),new Vector3(.1f,.08f,.84f),vq,0,Gold);
            if(deckTop==float.MinValue)return;
            var roofCenter=frame.TransformPoint(new Vector3(mid.x,deckTop,mid.z));
            if(Covered(roofCenter,body))return;
            // Roof railing with gaps where a bridge or ramp arrives.
            float inset=.35f;
            for(int edge=0;edge<4;edge++)
            {
                bool alongX=edge<2;float sign=edge%2==0?-1:1;float length=(alongX?hx:hz)*2-inset*2;int posts=Mathf.Max(2,Mathf.RoundToInt(length/2));
                for(int i=0;i<posts;i++)
                {
                    float t=-length*.5f+i*length/(posts-1);
                    var p=alongX?new Vector3(mid.x+t,deckTop,mid.z+sign*(hz-inset)):new Vector3(mid.x+sign*(hx-inset),deckTop,mid.z+t);
                    if(Blocked(frame.TransformPoint(p),body))continue;
                    mesh.Box(V(p+Vector3.up*.55f),new Vector3(.16f,1.1f,.16f),vq,0,Gold);
                    mesh.Sphere(V(p+Vector3.up*1.15f),Vector3.one*.14f,0,Gold,3,6);
                    if(i==posts-1)continue;
                    float t2=-length*.5f+(i+1)*length/(posts-1);
                    var p2=alongX?new Vector3(mid.x+t2,deckTop,mid.z+sign*(hz-inset)):new Vector3(mid.x+sign*(hx-inset),deckTop,mid.z+t2);
                    if(Blocked(frame.TransformPoint(p2),body))continue;
                    var a=V(p+Vector3.up*1.02f);var e=V(p2+Vector3.up*1.02f);
                    mesh.Box((a+e)*.5f,new Vector3(.1f,.1f,(e-a).magnitude),Quaternion.LookRotation(e-a),0,Gold);
                }
            }
            if(Hash(roofCenter)<.6f)Flag(mesh,V(new Vector3(mid.x-hx+1,deckTop,mid.z-hz+1)),DistrictColor(site));
        }

        // True where another body (a bridge end, a ramp) sits on the roof edge.
        static readonly Collider[] blockHits=new Collider[8];
        static bool Blocked(Vector3 p,Rigidbody self)
        {
            int n=Physics.OverlapBoxNonAlloc(p+Vector3.up*.8f,new Vector3(.9f,.6f,.9f),blockHits,Quaternion.identity,1<<Layers.Block,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(blockHits[i].attachedRigidbody!=self)return true;
            return false;
        }

        void RopeRails(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q,bool high)
        {
            bool alongZ=size.z>=size.x;float length=alongZ?size.z:size.x,width=alongZ?size.x:size.z;
            var along=alongZ?Vector3.forward:Vector3.right;var side=alongZ?Vector3.right:Vector3.forward;float top=size.y*.5f;
            int posts=Mathf.Max(2,Mathf.RoundToInt(length/2.4f)+1);var rope=high?RopeRed:Rope;
            for(int s=-1;s<=1;s+=2)
            {
                Vector3 last=Vector3.zero;
                for(int i=0;i<posts;i++)
                {
                    float x=-length*.5f+.2f+i*(length-.4f)/(posts-1);
                    var foot=along*x+side*s*(width*.5f-.08f)+Vector3.up*top;
                    mesh.Box(P(foot+Vector3.up*.6f),new Vector3(.14f,1.2f,.14f),q,0,Gold);
                    mesh.Sphere(P(foot+Vector3.up*1.24f),Vector3.one*.13f,0,Gold,3,6);
                    if(i>0)
                    {
                        foreach(float h in new[]{1.05f,.55f})
                        {
                            var a=P(last+Vector3.up*h);var e=P(foot+Vector3.up*h);var m=(a+e)*.5f+Vector3.down*.12f;
                            mesh.Box((a+m)*.5f,new Vector3(.08f,.08f,(m-a).magnitude),Quaternion.LookRotation(m-a),0,rope);
                            mesh.Box((m+e)*.5f,new Vector3(.08f,.08f,(e-m).magnitude),Quaternion.LookRotation(e-m),0,rope);
                        }
                    }
                    last=foot;
                }
            }
        }

        void Battlements(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q)
        {
            var h=size*.5f;var tint=new Color(.5f,.8f,.24f);
            for(int edge=0;edge<4;edge++)
            {
                bool alongX=edge<2;float sign=edge%2==0?-1:1;float length=alongX?size.x:size.z;int merlons=Mathf.Max(2,Mathf.FloorToInt(length/2.2f));
                for(int i=0;i<merlons;i++)
                {
                    if(i%2==1)continue;
                    float t=-length*.5f+(i+.5f)*length/merlons;
                    var c=alongX?new Vector3(t,h.y+.6f,sign*(h.z-.4f)):new Vector3(sign*(h.x-.4f),h.y+.6f,t);
                    mesh.Box(P(c),alongX?new Vector3(length/merlons,1.2f,.8f):new Vector3(.8f,1.2f,length/merlons),q,0,tint);
                }
            }
        }

        void Chevrons(ArtMesh mesh,Vector3 size,System.Func<Vector3,Vector3> P,Quaternion q,Vector3 world)
        {
            // On the face that looks toward the city center.
            var toCenter=Services.World.Center-world;toCenter.y=0;
            var faces=new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back};Vector3 best=faces[0];float score=-2;
            foreach(var f in faces){var w=q*f;float d=Vector3.Dot(w.normalized,toCenter.normalized);if(d>score){score=d;best=f;}}
            var h=size*.5f;float depth=Vector3.Scale(best,h).magnitude+.06f;var across=Vector3.Cross(Vector3.up,best);
            float span=Mathf.Min(Mathf.Abs(Vector3.Dot(across,size))*.35f,2.4f);
            for(int k=0;k<2;k++)
            {
                float y=h.y*.25f+k*span*.7f;
                foreach(int s in new[]{-1,1})
                {
                    var c=best*depth+across*s*span*.35f+Vector3.up*(y-span*.18f);
                    var rot=q*Quaternion.LookRotation(best)*Quaternion.Euler(0,0,s*40);
                    mesh.Box(P(c),new Vector3(span*.75f,.32f,.06f),rot,0,new Color(1,.92f,.25f));
                }
            }
        }

        void Fountain(ArtMesh mesh,Vector3 at,float k)
        {
            var up=Quaternion.identity;var choc=new Color(.42f,.22f,.12f);var milk=new Color(.62f,.38f,.22f);var cream=new Color(1,.95f,.86f);
            mesh.Cylinder(at,up,4.2f*k,.6f*k,0,cream,28);
            mesh.Cylinder(at+Vector3.up*.08f*k,up,3.8f*k,.6f*k,0,choc,28);
            float y=.6f;
            foreach(var (r,h) in new[]{(2.6f,1.6f),(1.8f,1.3f),(1.1f,1.1f)})
            {
                mesh.Cylinder(at+Vector3.up*y*k,up,.35f*k,h*k,0,cream,10);y+=h;
                mesh.Cylinder(at+Vector3.up*y*k,up,r*k,.35f*k,0,cream,22);
                mesh.Cylinder(at+Vector3.up*(y+.05f)*k,up,r*.9f*k,.35f*k,0,milk,22);
                for(int i=0;i<10;i++){float a=i*Mathf.PI/5;mesh.Box(at+new Vector3(Mathf.Cos(a)*r,y-.4f,Mathf.Sin(a)*r)*k,new Vector3(.22f,.9f,.22f)*k,up,0,choc);}
            }
            mesh.Sphere(at+Vector3.up*(y+.6f)*k,Vector3.one*.5f*k,0,new Color(.9f,.08f,.15f),6,10);
            // A glowing water jet reaching up toward the sky ring, and spray falling back into the top bowl.
            mesh.Cylinder(at+Vector3.up*(y+1)*k,up,.16f*k,4.5f*k,1,new Color(.7f,.92f,1),10);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;mesh.Sphere(at+new Vector3(Mathf.Cos(a)*.7f,y+4.8f-i%2*.5f,Mathf.Sin(a)*.7f)*k,Vector3.one*.22f*k,1,new Color(.8f,.95f,1),3,6);}
        }

        void Awning(ArtMesh mesh,Vector3 at,Quaternion q)
        {
            for(int i=0;i<8;i++)
            {
                float x=-2.8f+(i+.5f)*.7f;
                mesh.Box(at+q*new Vector3(x,0,.5f),new Vector3(.71f,.07f,1.2f),q*Quaternion.Euler(20,0,0),0,i%2==0?new Color(1,.35f,.6f):Sugar);
            }
        }

        void Flag(ArtMesh mesh,Vector3 at,Color tint)
        {
            mesh.Box(at+Vector3.up*1.8f,new Vector3(.1f,3.6f,.1f),Quaternion.identity,0,Sugar);
            mesh.Box(at+new Vector3(.6f,3.2f,0),new Vector3(1.2f,.75f,.05f),Quaternion.identity,0,tint);
            mesh.Sphere(at+Vector3.up*3.7f,Vector3.one*.16f,0,Gold,3,6);
        }

        // Structural artwork batching snapshots repeated block meshes and materials as soon as the city is built
        // and draws them itself, before this pass recolors them, so they would keep the old glass look. It is paused
        // from the moment this map is enabled and restored when it is disabled.
        bool batchingPaused;
        void OnEnable(){if(solidCandy&&candyCity&&!batchingPaused){batchingPaused=true;DeadlyJenga.CandyArt.StructuralMeshBatches.BatchingEnabled=false;}}
        void ResumeBatching(){if(!batchingPaused)return;batchingPaused=false;DeadlyJenga.CandyArt.StructuralMeshBatches.BatchingEnabled=true;}

        void WarmLighting()
        {
            RenderSettings.fogColor=new Color(1,.84f,.86f);
            RenderSettings.ambientSkyColor=new Color(.78f,.76f,1);RenderSettings.ambientEquatorColor=new Color(1,.78f,.78f);RenderSettings.ambientGroundColor=new Color(.5f,.36f,.42f);
        }
    }
}
