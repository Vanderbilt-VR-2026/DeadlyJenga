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
        readonly List<Matrix4x4> seaSpray = new(40), seaFoam = new(2), stormRemnants = new(12);
        Material tornadoSpray, tornadoFoam, tornadoRemnants;
        MaterialPropertyBlock tornadoVolume;
        DisasterFrame lastTornado;
        float lastTornadoDrawn, tornadoDissolve;
        Mesh stormRibbons;
        Vector3[] streamVertices;
        float nextStormStreamUpdate,lastStreamSize,lastStreamHeight,lastVolumeSize,lastVolumeHeight;
        bool compactStorm;
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
            compactStorm=Application.isMobilePlatform;
            storm.SetFloat("_MarchBudget",compactStorm?20:52);
            BuildStormDensity();
            stormMist=ObjectMaterial("CandyStormMist",new Color(.30f,.38f,.50f,.62f));
            var stormCloud=Resources.Load<Texture2D>("CandyScenery/StormCloudPuff");
            if(stormCloud){stormMist.SetTexture("_CloudTexture",stormCloud);stormMist.SetFloat("_UseCloudTexture",1);}
            stormRibbon=new Material(stormMist){name="Streaming vortex wisps"};stormRibbon.SetFloat("_Streamed",1);
            stormRibbon.SetColor("_BaseColor",new Color(.52f,.64f,.78f,.3f));
            tornadoSpray = new Material(stormMist) { name = "Tornado sea spray" };
            tornadoFoam = new Material(stormMist) { name = "Tornado surface foam" };
            tornadoFoam.SetFloat("_SeaFoam", 1f);
            tornadoRemnants = new Material(stormMist) { name = "Dissolving storm wisps" };
            tornadoVolume = new MaterialPropertyBlock();
            stormFlash=new Material(stormMist){name="Internal storm lightning",renderQueue=storm.renderQueue+2};stormFlash.SetFloat("_Lightning",1);
        }
        static Material ObjectMaterial(string shader,Color tint)
        {
            var material=new Material(Resources.Load<Shader>("CandyScenery/"+shader)){name=shader+" (runtime)",enableInstancing=true};
            material.SetColor("_BaseColor",tint);return material;
        }
        void BeginObjects()
        {
            acidStreaks.Clear();stones.Clear();chips.Clear();candyChips.Clear();smokePuffs.Clear();flames.Clear();dustPuffs.Clear();windPuffs.Clear();stormBolts.Clear();seaSpray.Clear();seaFoam.Clear();stormRemnants.Clear();
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
            DrawObjects(seaFoam,quad,tornadoFoam);
            DrawObjects(seaSpray,quad,tornadoSpray);
            DrawObjects(stormRemnants,quad,tornadoRemnants);
        }
        void DrawObjects(List<Matrix4x4> list,Mesh mesh,Material material)
        {
            for(int start=0;start<list.Count;start+=1023){int count=Mathf.Min(1023,list.Count-start);list.CopyTo(start,matrices,0,count);Graphics.DrawMeshInstanced(mesh,0,material,matrices,count,null,ShadowCastingMode.Off,false);}
        }
        void Tornado(DisasterFrame frame)
        {
            if (frame.values == null || frame.values.Length < 4) return;
            lastTornado = frame;
            lastTornadoDrawn = Time.time;
            DrawTornado(frame, 0f);
        }

        void RenderDepartedTornado()
        {
            if (lastTornado == null) return;
            float elapsed = Time.time - lastTornadoDrawn;
            if (elapsed <= 0f) return;
            if (elapsed >= 1.6f) { lastTornado = null; return; }
            DrawTornado(lastTornado, Mathf.SmoothStep(0f, 1f, elapsed / 1.6f));
        }

        void DrawTornado(DisasterFrame frame, float tail)
        {
            bool hasMotion = frame.points != null && frame.points.Length > 0 && frame.values.Length >= 6;
            float ageSinceSample = hasMotion ? Mathf.Clamp(Time.time - frame.sampledAt, 0f, .2f) : 0f;
            Vector3 center = frame.center;
            if (hasMotion) center += frame.points[0].velocity * ageSinceSample;

            float fadeSeconds = hasMotion ? frame.values[5] * .19f : 0f;
            float endFade = fadeSeconds > 0f
                ? Mathf.SmoothStep(0f, 1f, 1f - (frame.remaining - ageSinceSample) / fadeSeconds) : 0f;
            tornadoDissolve = Mathf.Max(endFade, tail);
            if (tornadoDissolve >= .995f) return;

            bool water = !Physics.Raycast(center + Vector3.up * 12f, Vector3.down, 12.25f,
                1 << Layers.Surface, QueryTriggerInteraction.Ignore);
            PrepareTornadoAt(frame, center, water);
            var transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
            tornadoVolume.SetFloat("_StormDissolve", tornadoDissolve);
            Graphics.DrawMesh(funnel, transform, storm, 0, null, 0, tornadoVolume, ShadowCastingMode.Off, false);
            if (tornadoDissolve < .96f)
                Graphics.DrawMesh(stormRibbons, transform, stormRibbon, 0, null, 0, null, ShadowCastingMode.Off, false);

            float size = frame.values[1];
            if (water) BuildTornadoSpray(center, size, frame.values[2]);
            if (tornadoDissolve > .01f)
            {
                float opacity = Mathf.Sin(tornadoDissolve * Mathf.PI);
                tornadoRemnants.SetColor("_BaseColor", new Color(.43f, .52f, .64f, opacity * .45f));
                for (int i = 0; i < 9; i++)
                {
                    float angle = i * 2.4f - Time.time * .55f;
                    float radius = size * (.7f + tornadoDissolve * (1f + Hash(i)));
                    float height = frame.values[0] * (.6f + Hash(i + 19) * .36f);
                    var position = center + new Vector3(Mathf.Cos(angle) * radius,
                        height + tornadoDissolve * size, Mathf.Sin(angle) * radius);
                    Puff(stormRemnants, position, size * (1.1f + tornadoDissolve), size * .65f, i * 19f);
                }
            }
        }

        void BuildTornadoSpray(Vector3 center, float size, float force)
        {
            float life = 1f - tornadoDissolve;
            float breakup = tornadoDissolve * size;
            tornadoFoam.SetColor("_BaseColor", new Color(.71f, .95f, 1f, life * .72f));
            tornadoSpray.SetColor("_BaseColor", new Color(.63f, .89f, 1f, life * .64f));
            float foamWidth = size * 2.4f + breakup * 1.7f;
            seaFoam.Add(Matrix4x4.TRS(center + Vector3.up * .11f, Quaternion.Euler(90f, 0f, 0f),
                new Vector3(foamWidth, foamWidth, 1f)));
            int count = compactStorm ? 12 : 26;
            for (int i = 0; i < count; i++)
            {
                float rise = Mathf.Repeat(Time.time * (.52f + Hash(i) * .34f) + Hash(i + 15), 1f);
                float angle = i * 2.4f + Time.time * (1.6f + force * .05f) + rise * 4.2f;
                float radius = size * (.30f + Hash(i + 26) * .39f - rise * .22f) + breakup * .6f;
                var position = center + new Vector3(Mathf.Cos(angle) * radius,
                    size * rise * (.24f + Hash(i + 41) * .52f) * life, Mathf.Sin(angle) * radius);
                float width = size * (.08f + Hash(i + 73) * .09f) * Mathf.Sin(rise * Mathf.PI);
                Puff(seaSpray, position, width, width * 2.1f, Mathf.Sin(angle) * 34f);
            }
        }
        void BuildStormDensity()
        {
            if(stormNoise)return;
            const int n=48;
            var voxels=new byte[n*n*n];
            var sin=new float[n];var cos=new float[n];
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;sin[i]=Mathf.Sin(a)*2;cos[i]=Mathf.Cos(a)*2;}
            for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float a=Mathf.PerlinNoise(12+cos[x]+cos[z],23+sin[x]+cos[y]);
                float b=Mathf.PerlinNoise(43+sin[y]+cos[x],17+sin[z]+cos[y]);
                voxels[x+n*(y+n*z)]=(byte)Mathf.Clamp(Mathf.RoundToInt((a+b)*127.5f),0,255);
            }
            stormNoise=new Texture3D(n,n,n,TextureFormat.R8,true){name="Periodic storm density volume",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            stormNoise.SetPixelData(voxels,0);stormNoise.Apply(true,true);storm.SetTexture("_DensityTex",stormNoise);
        }
        void PrepareTornado(DisasterFrame f)
        {
            tornadoDissolve = 0f;
            PrepareTornadoAt(f, f.center, false);
        }

        void PrepareTornadoAt(DisasterFrame f, Vector3 center, bool water)
        {
            if(f.values==null||f.values.Length<4)return;
            float height=f.values[0],size=f.values[1],speed=1+f.values[2]*.1f;
            if(!funnel)
            {
                funnelVertices=new Vector3[8];
                funnel=new Mesh{name="Volumetric vortex bounds",vertices=funnelVertices,triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5}};
                funnel.MarkDynamic();
            }
            if(lastVolumeSize!=size||lastVolumeHeight!=height)
            {
                lastVolumeSize=size;lastVolumeHeight=height;
                float volumeRadius=size*4.4f,top=height+size*1.8f;
                funnelVertices[0]=new Vector3(-volumeRadius,-1,-volumeRadius);funnelVertices[1]=new Vector3(volumeRadius,-1,-volumeRadius);
                funnelVertices[2]=new Vector3(volumeRadius,top,-volumeRadius);funnelVertices[3]=new Vector3(-volumeRadius,top,-volumeRadius);
                funnelVertices[4]=new Vector3(-volumeRadius,-1,volumeRadius);funnelVertices[5]=new Vector3(volumeRadius,-1,volumeRadius);
                funnelVertices[6]=new Vector3(volumeRadius,top,volumeRadius);funnelVertices[7]=new Vector3(-volumeRadius,top,volumeRadius);
                funnel.vertices=funnelVertices;
                funnel.RecalculateBounds();
            }
            float cycle=Mathf.Repeat(Time.time,7);
            float flash=Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(cycle-1.8f)/.1f),2)+.55f*Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(cycle-2f)/.075f),2);
            if(previewStormFlash>=0)flash=previewStormFlash;
            float visibility = 1f - tornadoDissolve;
            flash *= visibility;
            storm.SetVector("_StormInfo",new Vector4(size,height,speed,flash));
            stormMist.SetColor("_BaseColor",Color.Lerp(new Color(.18f,.22f,.31f,.64f*visibility),new Color(.40f,.49f,.86f,.70f*visibility),flash));
            stormRibbon.SetColor("_BaseColor",Color.Lerp(new Color(.27f,.34f,.46f,(compactStorm?.12f:.19f)*visibility),new Color(.59f,.71f,1.1f,.28f),flash));
            if(flash>.02f)
            {
                stormFlash.SetColor("_BaseColor",new Color(.8f,1.2f,2.8f,flash*.7f));
                Vector3 start=center+new Vector3(size*.5f,height+size*.8f,size*.3f),fork=center+new Vector3(size*1.2f,height-size*.3f,size*.8f);
                StormBolt(start,fork,17,2.2f,10);
                StormBolt(Vector3.Lerp(start,fork,.45f),center+new Vector3(size*2.5f,height+size*.1f,-size*.3f),42,1.6f,7);
                StormBolt(Vector3.Lerp(start,fork,.65f),center+new Vector3(size*.2f,height-size*.5f,size*1.5f),93,1.2f,5);
            }
            BuildStormStreams(size,height,speed);
            int clouds=compactStorm?5:11;
            for(int i=0;i<clouds;i++)
            {
                float a=i*Mathf.PI*2/clouds-Time.time*speed*.23f,r=size*(1.3f+Hash(i+18)*2.1f);
                Vector3 p=center+new Vector3(Mathf.Cos(a)*r,height+size*(-.2f+Hash(i+161)*.7f),Mathf.Sin(a)*r);
                float w=size*(1.05f+Hash(i+234)*.55f);
                Puff(windPuffs,p,w,w*.8f,Mathf.Sin(a)*20);
            }
            int dustCount=water?0:compactStorm?5:11, debrisCount=water?0:compactStorm?15:36;
            for(int i=0;i<dustCount+debrisCount;i++)
            {
                float t=Hash(i+91),a=i*2.4f+Time.time*speed*(1.5f-t);
                float radius=Mathf.Lerp(size*.10f,size*.68f,t*t);
                Vector3 p=center+new Vector3(Mathf.Cos(a)*radius,t*height,Mathf.Sin(a)*radius);
                if(i<dustCount)
                {
                    p.y=center.y+Hash(i+82)*size*.14f;
                    Puff(dustPuffs,p,size*.28f*visibility,size*.19f*visibility,i*34);
                }
                else
                {
                    float scale=(.12f+Hash(i)*.41f)*visibility;
                    chips.Add(Matrix4x4.TRS(p,Quaternion.Euler(i*39+Time.time*200,i*17,Time.time*120),new Vector3(scale*1.8f,scale*.63f,scale)));
                }
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
            int bands=compactStorm?2:3,rows=compactStorm?32:48;
            const int columns=2;
            if(stormRibbons&&Time.time<nextStormStreamUpdate&&lastStreamSize==size&&lastStreamHeight==height)return;
            nextStormStreamUpdate=Time.time+(compactStorm?1f/15:1f/24);
            lastStreamSize=size;lastStreamHeight=height;
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
        void ClearObjects(){meteorLooks.Clear();fragments.Clear();lastTornado=null;}
        void DestroyObjects(){foreach(var m in new[]{stone,chipMaterial,smoke,flame,dust,storm,stormMist,stormRibbon,stormFlash,acid,gravityBubble,tornadoSpray,tornadoFoam,tornadoRemnants})if(m)Destroy(m);if(rock)Destroy(rock);if(quad)Destroy(quad);if(stormNoise)Destroy(stormNoise);if(stormRibbons)Destroy(stormRibbons);}
    }
}
