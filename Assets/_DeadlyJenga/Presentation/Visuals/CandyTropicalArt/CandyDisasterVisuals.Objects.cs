using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyDisasterVisuals
    {
        struct MeteorLook { public DisasterPoint point; public float seen; public bool planet, hit; public Vector3 contact; }
        struct Fragment { public Vector3 position,velocity; public float born,life,size,seed; }
        readonly Dictionary<(IDisasterState,int),MeteorLook> meteorLooks=new Dictionary<(IDisasterState,int),MeteorLook>();
        readonly List<(IDisasterState,int)> oldMeteors=new List<(IDisasterState,int)>();
        readonly List<Fragment> fragments=new List<Fragment>(384);
        readonly List<Matrix4x4> stones=new List<Matrix4x4>(256),chips=new List<Matrix4x4>(384),smokePuffs=new List<Matrix4x4>(384),flames=new List<Matrix4x4>(384),dustPuffs=new List<Matrix4x4>(128);
        Mesh rock,quad;Material stone,chipMaterial,smoke,flame,dust,storm,acid,gravityBubble;
        readonly List<Matrix4x4> acidStreaks=new List<Matrix4x4>(420);
        IDisasterState drawingSource;
        Quaternion billboard;
        void InitializeObjects()
        {
            rock=Sphere();rock.name="Shared irregular meteor rock";
            var vertices=rock.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i].normalized;
                float shape=1+.10f*Mathf.Sin(p.x*13+p.z*7)*Mathf.Sin(p.y*11-p.x*5)+.055f*Mathf.Cos(p.z*25+p.y*17);
                vertices[i]*=shape;
            }
            rock.vertices=vertices;rock.RecalculateNormals();rock.RecalculateBounds();
            quad=new Mesh{name="Shared soft disaster billboard",vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},triangles=new[]{0,2,1,1,2,3}};
            stone=ObjectMaterial("CandyMeteorStone",new Color(.16f,.11f,.085f,1));
            chipMaterial=ObjectMaterial("CandyMeteorStone",new Color(.22f,.16f,.12f,1));chipMaterial.SetFloat("_Heat",.35f);
            smoke=ObjectMaterial("CandyDisasterSmoke",new Color(.24f,.27f,.3f,.38f));
            dust=ObjectMaterial("CandyDisasterSmoke",new Color(.49f,.40f,.29f,.28f));
            flame=ObjectMaterial("CandyDisasterSmoke",new Color(1,.4f,.05f,.86f));flame.SetFloat("_Flame",1);
            acid=ObjectMaterial("CandyDisasterSmoke",new Color(.56f,.86f,.12f,.8f));
            gravityBubble=ObjectMaterial("CandyGravityBubble",new Color(.35f,.6f,1,.5f));
            storm=ObjectMaterial("CandyStormFunnel",new Color(.36f,.38f,.39f,.98f));
        }
        static Material ObjectMaterial(string shader,Color tint)
        {
            var material=new Material(Resources.Load<Shader>("CandyScenery/"+shader)){name=shader+" (runtime)",enableInstancing=true};
            material.SetColor("_BaseColor",tint);return material;
        }
        void BeginObjects()
        {
            acidStreaks.Clear();stones.Clear();chips.Clear();smokePuffs.Clear();flames.Clear();dustPuffs.Clear();
            var camera=Services.Session?.ActiveCamera;
            billboard=camera?camera.transform.rotation:Quaternion.identity;
        }
        void Puff(List<Matrix4x4> list,Vector3 position,float width,float height,float roll=0)
        {list.Add(Matrix4x4.TRS(position,billboard*Quaternion.Euler(0,0,roll),new Vector3(width,height,1)));}
        void Meteors(DisasterFrame f)
        {
            if(f.points==null)return;
            foreach(var point in f.points)
            {
                var key=(drawingSource,point.id);
                meteorLooks[key]=new MeteorLook{point=point,seen=Time.time,planet=f.kind=="PlanetKillerMeteor"};
                Vector3 position=point.position+point.velocity*Mathf.Clamp(Time.time-f.sampledAt,0,.25f);
                float spin=Time.time*18+point.id*73;
                stones.Add(Matrix4x4.TRS(position,Quaternion.Euler(spin*.7f,spin,spin*.3f),Vector3.one*point.size));
                var back=-point.velocity.normalized;
                for(int j=0;j<6;j++)
                {
                    float fraction=j/5f;Vector3 p=position+back*point.size*(.3f+fraction*2.5f);
                    Puff(flames,p,point.size*(1-fraction*.78f),point.size*(1.3f-fraction*.8f),point.id*43+j*53);
                    if(j>2)Puff(smokePuffs,p+back*point.size*.6f,point.size*(.8f+fraction),point.size*(.8f+fraction),j*45);
                }
            }
        }
        void MeteorContact(Vector3 position,float strength)
        {
            // Remember a nearby contact, but require the meteor to disappear before bursting.
            (IDisasterState,int) best=default;float nearest=float.PositiveInfinity;bool found=false;
            foreach(var pair in meteorLooks)
            {
                var look=pair.Value;if(look.hit||Time.time-look.seen>.35f)continue;
                float distance=Vector3.Distance(position,look.point.position);
                if(distance<look.point.size*.65f+look.point.velocity.magnitude*.12f+1&&distance<nearest){nearest=distance;best=pair.Key;found=true;}
            }
            if(found){var look=meteorLooks[best];look.contact=position;look.hit=true;meteorLooks[best]=look;}
        }
        void Burst(Vector3 position,DisasterPoint point,bool planet)
        {
            int count=planet?72:12;
            for(int i=0;i<count;i++)
            {
                if(fragments.Count==384)fragments.RemoveAt(0);
                float a=Hash(point.id*97+i)*Mathf.PI*2,y=.22f+Hash(i+point.id*21)*.85f;
                Vector3 direction=new Vector3(Mathf.Cos(a),y,Mathf.Sin(a)).normalized;
                float size=Mathf.Clamp(point.size*(planet?.075f:.12f),.09f,6)*( .45f+Hash(i+87));
                fragments.Add(new Fragment{position=position+direction*Mathf.Min(point.size*.3f,12),velocity=direction*(planet?17:8)*( .5f+Hash(i+202))+point.velocity*.08f,born=Time.time,life=planet?3:1.5f,size=size,seed=i*51+point.id});
            }
        }
        void RenderObjects()
        {
            oldMeteors.Clear();
            foreach(var pair in meteorLooks)
            {
                if(Time.time-pair.Value.seen<.04f)continue;
                var look=pair.Value;
                // Replicas have no local collision event. Recover the visual contact from the last flight segment.
                if(look.hit)Burst(look.contact,look.point,look.planet);
                else if(Time.time-look.seen<.45f&&look.point.life>.3f)
                {
                    float distance=look.point.velocity.magnitude*.3f;
                    if(Physics.SphereCast(look.point.position,look.point.size*.5f,look.point.velocity.normalized,out var hit,distance,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor),QueryTriggerInteraction.Collide))Burst(hit.point,look.point,look.planet);
                }
                oldMeteors.Add(pair.Key);
            }
            foreach(var key in oldMeteors)meteorLooks.Remove(key);
            for(int i=fragments.Count-1;i>=0;i--)
            {
                var fragment=fragments[i];float age=Time.time-fragment.born,t=age/fragment.life;
                if(t>=1){fragments.RemoveAt(i);continue;}
                Vector3 p=fragment.position+fragment.velocity*age+Vector3.down*(4.9f*age*age);
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t));
                chips.Add(Matrix4x4.TRS(p,Quaternion.Euler(fragment.seed+age*140,age*100,fragment.seed),Vector3.one*fragment.size*fade));
                if(i%3==0)Puff(dustPuffs,fragment.position+fragment.velocity*(age*.25f)+Vector3.up*age,fragment.size*(3+age*2)*fade,fragment.size*(3+age*2)*fade,fragment.seed);
            }
            DrawObjects(stones,rock,stone);DrawObjects(chips,rock,chipMaterial);
            DrawObjects(acidStreaks,quad,acid);DrawObjects(smokePuffs,quad,smoke);DrawObjects(dustPuffs,quad,dust);DrawObjects(flames,quad,flame);
        }
        void DrawObjects(List<Matrix4x4> list,Mesh mesh,Material material)
        {
            for(int start=0;start<list.Count;start+=1023){int count=Mathf.Min(1023,list.Count-start);list.CopyTo(start,matrices,0,count);Graphics.DrawMeshInstanced(mesh,0,material,matrices,count,null,ShadowCastingMode.Off,false);}
        }
        void Tornado(DisasterFrame f)
        {
            if(f.values==null||f.values.Length<4)return;
            float height=f.values[0],size=f.values[1],speed=1+f.values[2]*.1f;
            const int rows=32,cols=40;
            if(!funnel)
            {
                funnel=new Mesh{name="Wind-sheared condensation funnel"};funnel.MarkDynamic();
                funnelVertices=new Vector3[(rows+1)*(cols+1)];funnelNormals=new Vector3[funnelVertices.Length];funnelTriangles=new int[rows*cols*6];
                var uv=new Vector2[funnelVertices.Length];int index=0;
                for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
                {int i=y*(cols+1)+x;uv[i]=new Vector2(x/(float)cols,y/(float)rows);if(y<rows&&x<cols){funnelTriangles[index++]=i;funnelTriangles[index++]=i+cols+1;funnelTriangles[index++]=i+1;funnelTriangles[index++]=i+1;funnelTriangles[index++]=i+cols+1;funnelTriangles[index++]=i+cols+2;}}
                funnel.vertices=funnelVertices;funnel.uv=uv;funnel.triangles=funnelTriangles;
            }
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {
                float t=y/(float)rows,a=x/(float)cols*Mathf.PI*2;
                float radius=Mathf.Lerp(Mathf.Max(.35f,size*.055f),size*.5f,Mathf.Pow(t,1.6f));
                radius*=1+.12f*Mathf.Sin(a*3+t*19-Time.time*speed)+.07f*Mathf.Sin(a*7-t*31+Time.time*speed*2);
                Vector3 lean=new Vector3(Mathf.Sin(t*3+Time.time*.21f)*size*.12f*t,0,Mathf.Sin(t*4-Time.time*.16f)*size*.09f*t);
                int i=y*(cols+1)+x;funnelVertices[i]=lean+new Vector3(Mathf.Cos(a)*radius,t*height,Mathf.Sin(a)*radius);funnelNormals[i]=new Vector3(Mathf.Cos(a),-.1f,Mathf.Sin(a)).normalized;
            }
            funnel.vertices=funnelVertices;funnel.normals=funnelNormals;funnel.RecalculateBounds();
            Graphics.DrawMesh(funnel,Matrix4x4.TRS(f.center,Quaternion.identity,Vector3.one),storm,0,null,0,null,ShadowCastingMode.Off,false);
            for(int i=0;i<56;i++)
            {
                float t=(i+.5f)/56,a=i*2.4f+Time.time*speed,radius=Mathf.Lerp(Mathf.Max(.35f,size*.055f),size*.5f,Mathf.Pow(t,1.6f));
                var p=f.center+new Vector3(Mathf.Cos(a)*radius+Mathf.Sin(t*3+Time.time*.21f)*size*.12f*t,t*height,Mathf.Sin(a)*radius+Mathf.Sin(t*4-Time.time*.16f)*size*.09f*t);
                Puff(smokePuffs,p,size*(.12f+t*.13f),height*.09f,i*37);
            }
            // Condensation crown and ground debris.
            for(int i=0;i<70;i++)
            {
                float t=Hash(i+91),a=i*2.4f+Time.time*speed*(1.5f-t),radius=Mathf.Lerp(size*.11f,size*.59f,t*t);
                Vector3 p=f.center+new Vector3(Mathf.Cos(a)*radius,t*height,Mathf.Sin(a)*radius);
                if(i<24){p=f.center+new Vector3(Mathf.Cos(a)*size*(.35f+Hash(i)*.7f),height+size*(Hash(i+31)-.5f)*.25f,Mathf.Sin(a)*size*(.35f+Hash(i)*.7f));Puff(smokePuffs,p,size*1.5f,size*.65f,i*41);}
                else if(i<48){p.y=f.center.y+Hash(i+82)*size*.12f;Puff(dustPuffs,p,size*.3f,size*.22f,i*34);}
                else chips.Add(Matrix4x4.TRS(p,Quaternion.Euler(i*39+Time.time*200,i*17,Time.time*120),Vector3.one*(.12f+Hash(i)*.35f)));
            }
        }
        void Fire(DisasterFrame f)
        {
            var world=Services.World;if(world==null||f.blocks==null)return;
            int stride=Mathf.Max(1,Mathf.CeilToInt(f.blocks.Length/96f));
            for(int i=0;i<f.blocks.Length;i+=stride)
            {
                int id=f.blocks[i];if(id<0||id>=world.Blocks.Count)continue;var rb=world.Blocks[id];if(!rb||!rb.gameObject.activeInHierarchy)continue;
                var collider=rb.GetComponent<Collider>();if(!collider)continue;var bounds=collider.bounds;
                for(int j=0;j<3;j++)
                {
                    float phase=Time.time*5+id*1.7f+j*2,scale=.8f+.15f*Mathf.Sin(phase);
                    var p=bounds.center+new Vector3(Mathf.Sin(j*2.4f)*bounds.extents.x*.6f,bounds.extents.y+.5f,Mathf.Cos(j*2.4f)*bounds.extents.z*.6f);
                    Puff(flames,p,scale,scale*2,j*13);if(j==0)Puff(smokePuffs,p+Vector3.up*(1+Mathf.Repeat(Time.time+id,2)),1.8f,2.5f,id*31);
                }
            }
        }
        void Dust(DisasterFrame f)
        {
            float water=Services.World?.WaterHeight(f.center)??0;
            for(int i=0;i<36;i++){float a=i*Mathf.PI*2/36,r=Mathf.Max(f.size.x,f.size.z)*.4f;Puff(dustPuffs,new Vector3(f.center.x+Mathf.Cos(a)*r,water+.6f+Mathf.Abs(Mathf.Sin(Time.time+i))*.5f,f.center.z+Mathf.Sin(a)*r),2.5f,1.8f,i*31);}
        }
        void Rain(DisasterFrame f)
        {
            if(f.values==null||f.values.Length<2)return;int count=Mathf.Clamp(Mathf.RoundToInt(f.values[1]*.7f),10,420);
            for(int i=0;i<count;i++){float h=Mathf.Repeat(Hash(i+300)-Time.time*.45f,1);var p=f.center+new Vector3((Hash(i)-.5f)*f.size.x,(h-.4f)*(f.size.y+30),(Hash(i+800)-.5f)*f.size.z);Puff(acidStreaks,p,.045f,.65f+Hash(i+33)*.65f,-7);}
        }
        void ClearObjects(){meteorLooks.Clear();fragments.Clear();}
        void DestroyObjects(){foreach(var m in new[]{stone,chipMaterial,smoke,flame,dust,storm,acid,gravityBubble})if(m)Destroy(m);if(rock)Destroy(rock);if(quad)Destroy(quad);}
    }
}





