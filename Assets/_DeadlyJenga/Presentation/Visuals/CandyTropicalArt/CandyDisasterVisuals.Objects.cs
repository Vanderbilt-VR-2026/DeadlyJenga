using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyDisasterVisuals
    {
        struct MeteorLook { public DisasterPoint point; public float seen; public bool planet, hit; public Vector3 contact; }
        struct Fragment { public Vector3 position,velocity; public float born,life,size,seed; public bool candy; }
        readonly Dictionary<(IDisasterState,int),MeteorLook> meteorLooks=new Dictionary<(IDisasterState,int),MeteorLook>();
        readonly List<(IDisasterState,int)> oldMeteors=new List<(IDisasterState,int)>();
        readonly List<Fragment> fragments=new List<Fragment>(384);
        int fragmentCursor;
        void AddFragment(Fragment fragment)
        {if(fragments.Count<384)fragments.Add(fragment);else fragments[fragmentCursor++%384]=fragment;}
        readonly List<Matrix4x4> stones=new List<Matrix4x4>(256),chips=new List<Matrix4x4>(384),smokePuffs=new List<Matrix4x4>(384),flames=new List<Matrix4x4>(384),dustPuffs=new List<Matrix4x4>(128);
        Texture3D stormNoise;
        Mesh rock,quad;Material stone,chipMaterial,smoke,flame,dust,storm,stormMist,stormRibbon,stormFlash,acid,gravityBubble;
        readonly List<Matrix4x4> windPuffs=new List<Matrix4x4>(112);
        readonly List<Matrix4x4> acidStreaks=new List<Matrix4x4>(420);
        IDisasterState drawingSource;
        readonly List<Matrix4x4> candyChips=new List<Matrix4x4>(384);
        Mesh shard; Material shardMaterial;
        readonly List<Matrix4x4> stormBolts=new List<Matrix4x4>(32);
        float previewStormFlash=-1;
        Mesh stormRibbons;
        Vector3[] streamVertices;
        Quaternion billboard;
        void InitializeObjects()
        {
            shard=new Mesh{name="Shared eight-face candy shard",vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back},triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2}};
            shard.RecalculateNormals();shardMaterial=Material(new Color(.95f,.76f,.38f));
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
            storm=ObjectMaterial("CandyStormFunnel",new Color(.22f,.27f,.32f,.88f));
            stormMist=ObjectMaterial("CandyStormMist",new Color(.30f,.38f,.50f,.62f));
            var stormCloud=Resources.Load<Texture2D>("CandyScenery/StormCloudPuff");
            if(stormCloud){stormMist.SetTexture("_CloudTexture",stormCloud);stormMist.SetFloat("_UseCloudTexture",1);}
            stormRibbon=new Material(stormMist){name="Streaming vortex wisps"};stormRibbon.SetFloat("_Streamed",1);
            stormRibbon.SetColor("_BaseColor",new Color(.52f,.64f,.78f,.3f));
            stormFlash=new Material(stormMist){name="Internal storm lightning",renderQueue=storm.renderQueue+2};stormFlash.SetFloat("_Lightning",1);
        }
        static Material ObjectMaterial(string shader,Color tint)
        {
            var material=new Material(Resources.Load<Shader>("CandyScenery/"+shader)){name=shader+" (runtime)",enableInstancing=true};
            material.SetColor("_BaseColor",tint);return material;
        }
        void BeginObjects()
        {
            acidStreaks.Clear();stones.Clear();chips.Clear();candyChips.Clear();smokePuffs.Clear();flames.Clear();dustPuffs.Clear();windPuffs.Clear();stormBolts.Clear();
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
            else if(strength>=18)
            {
                var camera=Services.Session?.ActiveCamera;
                if(camera && (camera.transform.position-position).sqrMagnitude>10000)return;
                // Cosmetic only: a bounded pool of eight-face chips, no rigidbodies or colliders.
                int seed=position.GetHashCode();
                for(int i=0;i<8;i++)
                {
                    float angle=Hash(seed+i)*Mathf.PI*2;
                    var direction=new Vector3(Mathf.Cos(angle),.4f+Hash(seed+i+10),Mathf.Sin(angle));
                    AddFragment(new Fragment{position=position,velocity=direction*(2+Hash(seed+i+20)*4),born=Time.time,life=.65f,size=.15f+Hash(seed+i+30)*.3f,seed=i*51,candy=true});
                }
            }
        }
        void Burst(Vector3 position,DisasterPoint point,bool planet)
        {
            int count=planet?72:12;
            for(int i=0;i<count;i++)
            {
                float a=Hash(point.id*97+i)*Mathf.PI*2,y=.22f+Hash(i+point.id*21)*.85f;
                Vector3 direction=new Vector3(Mathf.Cos(a),y,Mathf.Sin(a)).normalized;
                float size=Mathf.Clamp(point.size*(planet?.075f:.12f),.09f,6)*( .45f+Hash(i+87));
                AddFragment(new Fragment{position=position+direction*Mathf.Min(point.size*.3f,12),velocity=direction*(planet?17:8)*( .5f+Hash(i+202))+point.velocity*.08f,born=Time.time,life=planet?3:1.5f,size=size,seed=i*51+point.id});
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
                (fragment.candy?candyChips:chips).Add(Matrix4x4.TRS(p,Quaternion.Euler(fragment.seed+age*140,age*100,fragment.seed),Vector3.one*fragment.size*fade));
                if(!fragment.candy && i%3==0)Puff(dustPuffs,fragment.position+fragment.velocity*(age*.25f)+Vector3.up*age,fragment.size*(3+age*2)*fade,fragment.size*(3+age*2)*fade,fragment.seed);
            }
            DrawObjects(stones,rock,stone);DrawObjects(chips,rock,chipMaterial);
            DrawObjects(candyChips,shard,shardMaterial);
            DrawObjects(acidStreaks,quad,acid);DrawObjects(smokePuffs,quad,smoke);DrawObjects(dustPuffs,quad,dust);DrawObjects(flames,quad,flame);
            DrawObjects(windPuffs,quad,stormMist);DrawObjects(stormBolts,quad,stormFlash);
        }
        void DrawObjects(List<Matrix4x4> list,Mesh mesh,Material material)
        {
            for(int start=0;start<list.Count;start+=1023){int count=Mathf.Min(1023,list.Count-start);list.CopyTo(start,matrices,0,count);Graphics.DrawMeshInstanced(mesh,0,material,matrices,count,null,ShadowCastingMode.Off,false);}
        }
        void Tornado(DisasterFrame f)
        {
            if(f.values==null||f.values.Length<4)return;
            PrepareTornado(f);
            Graphics.DrawMesh(funnel,Matrix4x4.TRS(f.center,Quaternion.identity,Vector3.one),storm,0,null,0,null,ShadowCastingMode.Off,false);
            Graphics.DrawMesh(stormRibbons,Matrix4x4.TRS(f.center,Quaternion.identity,Vector3.one),stormRibbon,0,null,0,null,ShadowCastingMode.Off,false);
        }
        void PrepareTornado(DisasterFrame f)
        {
            if(f.values==null||f.values.Length<4)return;
            float height=f.values[0],size=f.values[1],speed=1+f.values[2]*.1f;
            if(!stormNoise)
            {
                const int n=64;var voxels=new byte[n*n*n];
                for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float ax=x*Mathf.PI*2/n,ay=y*Mathf.PI*2/n,az=z*Mathf.PI*2/n;
                    float a=Mathf.PerlinNoise(12+Mathf.Cos(ax)*2+Mathf.Cos(az)*2,23+Mathf.Sin(ax)*2+Mathf.Cos(ay)*2);
                    float b=Mathf.PerlinNoise(43+Mathf.Sin(ay)*2+Mathf.Cos(ax)*2,17+Mathf.Sin(az)*2+Mathf.Cos(ay)*2);
                    voxels[x+n*(y+n*z)]=(byte)Mathf.Clamp(Mathf.RoundToInt((a+b)*127.5f),0,255);
                }
                stormNoise=new Texture3D(n,n,n,TextureFormat.R8,true){name="Periodic storm density volume",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
                stormNoise.SetPixelData(voxels,0);stormNoise.Apply(true,true);storm.SetTexture("_DensityTex",stormNoise);
            }
            if(!funnel)
            {
                funnelVertices=new Vector3[8];
                funnel=new Mesh{name="Volumetric vortex bounds",vertices=funnelVertices,triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5}};
                funnel.MarkDynamic();
            }
            float volumeRadius=size*5.2f,top=height+size*2.5f;
            funnelVertices[0]=new Vector3(-volumeRadius,-1,-volumeRadius);funnelVertices[1]=new Vector3(volumeRadius,-1,-volumeRadius);
            funnelVertices[2]=new Vector3(volumeRadius,top,-volumeRadius);funnelVertices[3]=new Vector3(-volumeRadius,top,-volumeRadius);
            funnelVertices[4]=new Vector3(-volumeRadius,-1,volumeRadius);funnelVertices[5]=new Vector3(volumeRadius,-1,volumeRadius);
            funnelVertices[6]=new Vector3(volumeRadius,top,volumeRadius);funnelVertices[7]=new Vector3(-volumeRadius,top,volumeRadius);
            funnel.vertices=funnelVertices;
            funnel.RecalculateBounds();
            float cycle=Mathf.Repeat(Time.time,7);
            float flash=Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(cycle-1.8f)/.1f),2)+.55f*Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(cycle-2f)/.075f),2);
            if(previewStormFlash>=0)flash=previewStormFlash;
            storm.SetVector("_StormInfo",new Vector4(size,height,speed,flash));
            stormMist.SetColor("_BaseColor",Color.Lerp(new Color(.30f,.38f,.50f,.62f),new Color(.48f,.62f,1.1f,.72f),flash));
            stormRibbon.SetColor("_BaseColor",Color.Lerp(new Color(.52f,.64f,.78f,.3f),new Color(.7f,.9f,1.3f,.36f),flash));
            if(flash>.02f)
            {
                stormFlash.SetColor("_BaseColor",new Color(.8f,1.2f,2.8f,flash*.9f));
                Vector3 start=f.center+new Vector3(size*.5f,height+size*.8f,size*.3f),fork=f.center+new Vector3(size*1.2f,height-size*.3f,size*.8f);
                StormBolt(start,fork,17,2.2f,10);
                StormBolt(Vector3.Lerp(start,fork,.45f),f.center+new Vector3(size*2.5f,height+size*.1f,-size*.3f),42,1.6f,7);
                StormBolt(Vector3.Lerp(start,fork,.65f),f.center+new Vector3(size*.2f,height-size*.5f,size*1.5f),93,1.2f,5);
            }
            BuildStormStreams(size,height,speed);
            for(int i=0;i<32;i++)
            {
                float a=i*Mathf.PI*2/32-Time.time*speed*.23f,r=size*(1.3f+Hash(i+18)*2.1f);
                Vector3 p=f.center+new Vector3(Mathf.Cos(a)*r,height+size*(-.2f+Hash(i+161)*.7f),Mathf.Sin(a)*r);
                float w=size*(1.8f+Hash(i+234)*.8f);
                Puff(windPuffs,p,w,w*.8f,Mathf.Sin(a)*20);
            }
            // Ground debris stays separate from the cloud canopy.
            for(int i=0;i<70;i++)
            {
                float t=Hash(i+91),a=i*2.4f+Time.time*speed*(1.5f-t),radius=Mathf.Lerp(size*.11f,size*.59f,t*t);
                Vector3 p=f.center+new Vector3(Mathf.Cos(a)*radius,t*height,Mathf.Sin(a)*radius);
                if(i<24)continue;
                else if(i<48){p.y=f.center.y+Hash(i+82)*size*.12f;Puff(dustPuffs,p,size*.3f,size*.22f,i*34);}
                else chips.Add(Matrix4x4.TRS(p,Quaternion.Euler(i*39+Time.time*200,i*17,Time.time*120),Vector3.one*(.12f+Hash(i)*.35f)));
            }
        }
        void StormBolt(Vector3 from,Vector3 to,int seed,float width,int segments)
        {
            Vector3 previous=from;
            for(int i=1;i<=segments;i++)
            {
                float t=i/(float)segments;
                Vector3 point=Vector3.Lerp(from,to,t)+new Vector3(Hash(seed+i)-.5f,0,Hash(seed+i+83)-.5f)*(width*6*Mathf.Sin(t*Mathf.PI));
                Vector3 direction=Quaternion.Inverse(billboard)*(point-previous);
                float roll=-Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg;
                Puff(stormBolts,(point+previous)*.5f,width,Vector3.Distance(point,previous)*1.08f,roll);previous=point;
            }
        }
        void BuildStormStreams(float size,float height,float speed)
        {
            const int bands=6,rows=96,columns=4;
            if(!stormRibbons)
            {
                streamVertices=new Vector3[bands*(rows+1)*(columns+1)];
                var uv=new Vector2[streamVertices.Length];var triangles=new int[bands*rows*columns*6];int ti=0;
                for(int band=0;band<bands;band++)for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                {
                    int id=(band*(rows+1)+y)*(columns+1)+x;uv[id]=new Vector2(x/(float)columns,y/(float)rows);
                    if(y==rows||x==columns)continue;
                    triangles[ti++]=id;triangles[ti++]=id+columns+1;triangles[ti++]=id+1;
                    triangles[ti++]=id+1;triangles[ti++]=id+columns+1;triangles[ti++]=id+columns+2;
                }
                stormRibbons=new Mesh{name="Continuous helical vapor streams",vertices=streamVertices,uv=uv,triangles=triangles};stormRibbons.MarkDynamic();
            }
            for(int band=0;band<bands;band++)for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                float t=y/(float)rows,u=x/(float)columns,bodyT=Mathf.Min(1,t/.82f),capT=Mathf.Clamp01((t-.82f)/.18f);
                float a=bodyT*19+capT*6+band*Mathf.PI*2/bands+Time.time*speed*1.8f+(u-.5f)*.85f;
                float radius=Mathf.Lerp(size*.14f,size*.8f,Mathf.Pow(bodyT,1.2f))*(1+.06f*Mathf.Sin(bodyT*31+band));
                if(capT>0)radius=Mathf.Lerp(size*.8f,size*3.9f,capT);
                var lean=new Vector3(Mathf.Sin(bodyT*3+Time.time*.7f),0,Mathf.Cos(bodyT*4-Time.time*.6f))*size*.13f*bodyT;
                streamVertices[(band*(rows+1)+y)*(columns+1)+x]=lean+new Vector3(Mathf.Cos(a)*radius,bodyT*height+Mathf.Sin(capT*Mathf.PI)*size*.65f,Mathf.Sin(a)*radius);
            }
            stormRibbons.vertices=streamVertices;stormRibbons.RecalculateBounds();
        }
        void Fire(DisasterFrame f)
        {
            if(f.burns!=null){FireSurfaces(f);return;}
            var world=Services.World;if(world==null||f.blocks==null)return;
            for(int i=0;i<f.blocks.Length;i++)
            {
                int id=f.blocks[i];if(id<0||id>=world.Blocks.Count)continue;var rb=world.Blocks[id];if(!rb||!rb.gameObject.activeInHierarchy)continue;
                Engulf(rb,id);
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
        void DestroyObjects(){foreach(var m in new[]{stone,chipMaterial,smoke,flame,dust,storm,stormMist,stormRibbon,stormFlash,acid,gravityBubble})if(m)Destroy(m);if(rock)Destroy(rock);if(quad)Destroy(quad);if(stormNoise)Destroy(stormNoise);if(stormRibbons)Destroy(stormRibbons);}
    }
}
