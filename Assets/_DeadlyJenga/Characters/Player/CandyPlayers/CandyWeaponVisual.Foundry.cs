using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        enum Finish
        {
            Steel, Charcoal, Ceramic, Brass, Walnut, Violet, Cyan, Amber, BladeSteel, BladeEdge,
            AxeLeather, AxeBronze, LauncherOlive, LauncherGunmetal, LauncherRubber, LauncherSteel, LauncherGraphite, LauncherStencil
        }
        static readonly Dictionary<EquipmentKind,FoundryModel> foundry=new Dictionary<EquipmentKind,FoundryModel>();
        static readonly Dictionary<Finish,Material> finishes=new Dictionary<Finish,Material>();
        sealed class FoundryModel
        {
            public FoundryModel feed;
            public readonly Dictionary<Finish,Mesh> meshes=new Dictionary<Finish,Mesh>();
        }
        sealed class FoundryBuilder
        {
            readonly Dictionary<Finish,List<CombineInstance>> groups=new Dictionary<Finish,List<CombineInstance>>();
            public void Add(Finish finish,Mesh mesh,Matrix4x4 matrix)
            {
                if(!groups.TryGetValue(finish,out var list))groups[finish]=list=new List<CombineInstance>();
                list.Add(new CombineInstance{mesh=mesh,transform=matrix});
            }
            public void Plate(Finish finish,Vector3 center,Vector3 size,float bevel=.012f)
            {
                float y=size.y*.5f,z=size.z*.5f,c=Mathf.Min(y,z)*.33f;
                var mesh=FoundrySolid("Beveled armor",size.x,Mathf.Min(bevel,Mathf.Min(size.x,c)*.42f),
                    new[]{new Vector2(-y,-z+c),new Vector2(-y,z-c),new Vector2(-y+c,z),new Vector2(y-c,z),
                    new Vector2(y,z-c),new Vector2(y,-z+c),new Vector2(y-c,-z),new Vector2(-y+c,-z)});
                Add(finish,mesh,Matrix4x4.Translate(center));
            }
            public void Profile(Finish finish,Vector2 center,params Vector2[] profile)
            {
                const int sides=32;
                var v=new Vector3[profile.Length*(sides+1)];var uv=new Vector2[v.Length];var indices=new List<int>();
                for(int j=0;j<profile.Length;j++)for(int i=0;i<=sides;i++)
                {
                    float a=i*Mathf.PI*2/sides;int n=j*(sides+1)+i;
                    v[n]=new Vector3(center.x+Mathf.Cos(a)*profile[j].y,center.y+Mathf.Sin(a)*profile[j].y,profile[j].x);
                    uv[n]=new Vector2(i/(float)sides,profile[j].x);
                    if(j==0||i==sides)continue;
                    int b=n-sides-1;indices.AddRange(new[]{b,b+1,n,b+1,n+1,n});
                }
                var mesh=new Mesh{name="Turned profile"};mesh.vertices=v;mesh.uv=uv;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
                var normals=mesh.normals;
                for(int j=0;j<profile.Length;j++){int a=j*(sides+1);var n=(normals[a]+normals[a+sides]).normalized;normals[a]=normals[a+sides]=n;}
                mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();Add(finish,mesh,Matrix4x4.identity);
            }
            public void Tube(Finish finish,float y,float a,float b,float r,float bore=0,float x=0)
            {
                float e=Mathf.Min(.016f,(b-a)*.19f);
                Profile(finish,new Vector2(x,y),new Vector2(a,bore),new Vector2(a,r-e*.4f),new Vector2(a+e,r),
                    new Vector2(b-e,r),new Vector2(b,r-e*.4f),new Vector2(b,bore),new Vector2(a,bore));
            }
            public void Shape(Finish finish,Action<CandyAccessoryMeshes.Builder> draw)
            {
                var b=new CandyAccessoryMeshes.Builder();draw(b);Add(finish,b.Mesh("Sculpted hardware"),Matrix4x4.identity);
            }
            public FoundryModel Bake(EquipmentKind kind)
            {
                var model=new FoundryModel();
                foreach(var p in groups)
                {
                    var mesh=new Mesh{name="Foundry "+kind+" "+p.Key,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                    mesh.CombineMeshes(p.Value.ToArray(),true,true);mesh.RecalculateBounds();model.meshes[p.Key]=mesh;
                    foreach(var part in p.Value)Object.Destroy(part.mesh);
                }
                return model;
            }
        }
        static Mesh FoundrySolid(string name,float width,float bevel,Vector2[] outline)
        {
            float area=0;Vector2 center=Vector2.zero;
            for(int i=0;i<outline.Length;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Length];
                area+=a.x*b.y-a.y*b.x;center+=a;
            }
            if(area<0)Array.Reverse(outline);
            center/=outline.Length;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            Vector3 At(int ring,int i)
            {
                var p=outline[i];bool cap=ring==0||ring==3;
                if(cap)p=Vector2.MoveTowards(p,center,Mathf.Min(bevel,(p-center).magnitude*.3f));
                float x=(ring<2?-1:1)*(width*.5f-(cap?0:bevel));
                return new Vector3(x,p.x,p.y);
            }
            void Face(Vector3 a,Vector3 b,Vector3 c)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                uv.Add(new Vector2(a.z,a.y)*4);uv.Add(new Vector2(b.z,b.y)*4);uv.Add(new Vector2(c.z,c.y)*4);
                tri.Add(n);tri.Add(n+1);tri.Add(n+2);
            }
            for(int i=0;i<outline.Length;i++)
            {
                int j=(i+1)%outline.Length;
                for(int r=0;r<3;r++)
                {
                    Face(At(r,i),At(r,j),At(r+1,i));Face(At(r,j),At(r+1,j),At(r+1,i));
                }
                Face(new Vector3(-width*.5f,center.x,center.y),At(0,j),At(0,i));
                Face(new Vector3(width*.5f,center.x,center.y),At(3,i),At(3,j));
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
        }
        bool BuildFoundry(Transform root,EquipmentKind kind)
        {
            if(kind!=EquipmentKind.RocketLauncher&&kind!=EquipmentKind.GrenadeLauncher&&kind!=EquipmentKind.BarbedBat&&
                kind!=EquipmentKind.WristLaser&&kind!=EquipmentKind.LaserAxe&&kind!=EquipmentKind.Dagger&&
                kind!=EquipmentKind.ChainGun&&kind!=EquipmentKind.Chainsaw&&kind!=EquipmentKind.SawStaff)return false;
            if(!foundry.TryGetValue(kind,out var model))
            {
                var b=new FoundryBuilder();
                var feedBuilder = new FoundryBuilder();
                switch(kind)
                {
                    case EquipmentKind.RocketLauncher: ForgeRocket(b,feedBuilder);break;
                    case EquipmentKind.GrenadeLauncher: ForgeGrenade(b,feedBuilder);break;
                    case EquipmentKind.BarbedBat: ForgeBat(b);break;
                    case EquipmentKind.WristLaser: ForgeWrist(b,feedBuilder);break;
                    case EquipmentKind.ChainGun: ForgeChainGun(b,feedBuilder);break;
                    case EquipmentKind.Chainsaw: ForgeChainsaw(b,feedBuilder);break;
                    case EquipmentKind.SawStaff: ForgeSawStaff(b);break;
                    default: ForgeBlade(b,feedBuilder,kind==EquipmentKind.LaserAxe);break;
                }
                foundry[kind]=model=b.Bake(kind);
                model.feed = feedBuilder.Bake(kind);
            }
            foreach(var p in model.meshes)MountFoundry(root,p.Value,p.Key);
            if (model.feed.meshes.Count > 0)
            {
                var feed = MakeFeedAssembly(root);
                foreach (var part in model.feed.meshes) MountFoundry(feed, part.Value, part.Key);
            }
            if(kind==EquipmentKind.ChainGun||kind==EquipmentKind.SawStaff||kind==EquipmentKind.Chainsaw)
            {
                var key=kind==EquipmentKind.ChainGun?"Foundry six-barrel rotor":"Serrated saw wheel - forged carbide";
                rotor=new GameObject(key).transform;rotor.SetParent(root,false);
                rotor.localPosition=kind==EquipmentKind.ChainGun?new Vector3(0,ChainGunBarrelHeight,ChainGunRotorStart):new Vector3(0,0,kind==EquipmentKind.Chainsaw?.79f:.88f);
                if(!arsenalMeshes.TryGetValue(key,out var mesh))
                {
                    var b=new CandyAccessoryMeshes.Builder();
                    if(kind==EquipmentKind.ChainGun)
                    {
                        b.Cone(Vector3.back*.09f,Vector3.forward*.018f,.040f,.043f,20);
                        b.Cone(Vector3.back*.008f,Vector3.forward*.028f,.109f,.109f,24);
                        b.Ring(Vector3.forward*.449f,.108f,.018f,Quaternion.identity);
                        for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Cos(a)*.083f,Mathf.Sin(a)*.083f,0);b.Cone(p,p+Vector3.forward*.58f,.022f,.022f,12);b.Cone(p+Vector3.forward*.52f,p+Vector3.forward*.61f,.028f,.026f,12);}
                    }
                    else
                    {
                        b.Cone(Vector3.left*.025f,Vector3.right*.025f,.195f,.195f,32);
                        b.Cone(Vector3.left*.040f,Vector3.right*.040f,.074f,.074f,20);
                        for(int i=0;i<32;i++){float a=i*Mathf.PI/16;var p=new Vector3(0,Mathf.Cos(a),Mathf.Sin(a));b.Cone(p*.185f,new Vector3(0,Mathf.Cos(a+.075f),Mathf.Sin(a+.075f))*.239f,.021f,.004f,4);}
                    }
                    arsenalMeshes[key]=mesh=b.Mesh(key);
                }
                MountFoundry(rotor,mesh,Finish.Steel);
            }
            return true;
        }
        void MountFoundry(Transform root,Mesh mesh,Finish finish)
        {
            var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=FoundrySurface(finish);
        }
        static Material FoundrySurface(Finish finish)
        {
            if(finishes.TryGetValue(finish,out var mat)&&mat)return mat;
            if(finish>=Finish.LauncherOlive)return finishes[finish]=LauncherSurface(finish);
            bool blade=finish==Finish.BladeSteel||finish==Finish.BladeEdge;
            bool leather=finish==Finish.AxeLeather, bronze=finish==Finish.AxeBronze;
            Color color=leather?new Color(.27f,.127f,.069f):bronze?new Color(.55f,.33f,.13f):finish==Finish.BladeEdge?new Color(.78f,.81f,.84f):finish==Finish.BladeSteel?new Color(.49f,.53f,.57f):finish==Finish.Steel?new Color(.44f,.52f,.59f):finish==Finish.Charcoal?new Color(.06f,.075f,.09f):
                finish==Finish.Ceramic?new Color(.85f,.24f,.06f):finish==Finish.Brass?new Color(.82f,.55f,.22f):
                finish==Finish.Walnut?new Color(.53f,.25f,.105f):finish==Finish.Violet?new Color(.42f,.16f,.84f):
                finish==Finish.Amber?new Color(1,.26f,.025f):new Color(.10f,.82f,1);
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Foundry "+finish,enableInstancing=true,color=color};
            bool wood=finish==Finish.Walnut;bool luminous=finish==Finish.Cyan||finish==Finish.Violet||finish==Finish.Amber;
            mat.SetFloat("_Metallic",blade?.87f:bronze||finish==Finish.Steel||finish==Finish.Brass?.78f:finish==Finish.Charcoal?.28f:.04f);
            mat.SetFloat("_Smoothness",blade?(finish==Finish.BladeEdge?.92f:.69f):wood?.46f:leather?.29f:bronze?.52f:finish==Finish.Charcoal?.34f:.72f);
            int n=blade?512:256;var tex=new Texture2D(n,n,TextureFormat.RGBA32,true)
                {name=mat.name+" surface",filterMode=FilterMode.Trilinear,anisoLevel=4,wrapMode=TextureWrapMode.Repeat};var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=x/(float)n,v=y/(float)n;
                float noise=Mathf.PerlinNoise(u*16,v*23);
                float grain=wood? .68f+.24f*Mathf.Sin((u*28+Mathf.PerlinNoise(u*3,v*2)*1.5f)*Mathf.PI*2)+noise*.13f:
                    .78f+noise*.19f+.055f*Mathf.Sin(y*2.6f+Mathf.Sin(x*.12f));
                if(leather)grain=.63f+noise*.21f+Mathf.PerlinNoise(u*122,v*187)*.18f;
                if(bronze)grain=.72f+noise*.20f+Mathf.PerlinNoise(u*91,v*91)*.09f;
                if(blade)grain=.81f+noise*.15f+.032f*Mathf.Sin(u*940f+Mathf.Sin(v*11f)*.8f);
                if(finish==Finish.Charcoal)grain-=Mathf.Pow(Mathf.Max(0,Mathf.Sin(x*.8f)*Mathf.Sin(y*.8f)),6)*.18f;
                pixels[y*n+x]=new Color(grain,grain,grain,1);
            }
            tex.SetPixels(pixels);tex.Apply(true,true);mat.SetTexture("_BaseMap",tex);
            if(luminous){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*1.5f);}
            return finishes[finish]=mat;
        }
        static void Grip(FoundryBuilder b,float z=0,float x=0,float y=0)
        {
            b.Plate(Finish.Charcoal,new Vector3(x,-.066f+y,z),new Vector3(.094f,.185f,.11f));
            for(int i=0;i<4;i++)b.Plate(Finish.Steel,new Vector3(x,-.13f+y+i*.029f,z-.047f),new Vector3(.082f,.009f,.018f),.002f);
        }
        sealed class LauncherMaps
        {
            public Texture2D albedo, normal, metalRoughness;
        }
        enum LauncherMaterial { Coated, Steel, Polymer }
        static readonly Dictionary<LauncherMaterial,LauncherMaps> launcherMaps=new Dictionary<LauncherMaterial,LauncherMaps>();

        static Material LauncherSurface(Finish finish)
        {
            var substrate=finish switch
            {
                Finish.LauncherSteel or Finish.LauncherGunmetal => LauncherMaterial.Steel,
                Finish.LauncherRubber => LauncherMaterial.Polymer,
                _ => LauncherMaterial.Coated
            };
            var color=finish switch
            {
                Finish.LauncherOlive => new Color(.46f,.49f,.32f),
                Finish.LauncherGraphite => new Color(.34f,.36f,.38f),
                Finish.LauncherGunmetal => new Color(.31f,.34f,.37f),
                Finish.LauncherSteel => new Color(.63f,.65f,.65f),
                Finish.LauncherStencil => new Color(.80f,.70f,.45f),
                _ => new Color(.115f,.119f,.116f)
            };
            if(!launcherMaps.TryGetValue(substrate,out var maps))
                launcherMaps[substrate]=maps=BuildLauncherMaps(substrate);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name="Launcher "+finish,color=color,enableInstancing=true
            };
            material.SetTexture("_BaseMap",maps.albedo);
            material.SetTexture("_BumpMap",maps.normal);
            material.SetFloat("_BumpScale",substrate==LauncherMaterial.Polymer?.46f:.62f);
            material.SetTexture("_MetallicGlossMap",maps.metalRoughness);
            material.SetFloat("_Metallic",substrate==LauncherMaterial.Steel?.86f:.12f);
            material.SetFloat("_Smoothness",1);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            return material;
        }

        static LauncherMaps BuildLauncherMaps(LauncherMaterial substrate)
        {
            const int size=256;
            Texture2D Map(string name,bool linear)=>new Texture2D(size,size,TextureFormat.RGBA32,true,linear)
            {
                name="Launcher "+substrate+" "+name,filterMode=FilterMode.Trilinear,
                anisoLevel=2,wrapMode=TextureWrapMode.Repeat
            };
            var maps=new LauncherMaps
            {
                albedo=Map("color",false),normal=Map("normal",true),metalRoughness=Map("metal and smoothness",true)
            };
            var color=new Color32[size*size];
            var normal=new Color32[color.Length];
            var metal=new Color32[color.Length];
            var heights=new float[color.Length];
            bool steel=substrate==LauncherMaterial.Steel, rubber=substrate==LauncherMaterial.Polymer;
            byte Byte(float value)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(value)*255);
            // Wrapped noise avoids a stripe at the UV seam of each turned part.
            float Grain(float u,float v,float xScale,float yScale)
            {
                float a=Mathf.Lerp(Mathf.PerlinNoise(11+u*xScale,23+v*yScale),
                    Mathf.PerlinNoise(11+(u-1)*xScale,23+v*yScale),u);
                float b=Mathf.Lerp(Mathf.PerlinNoise(11+u*xScale,23+(v-1)*yScale),
                    Mathf.PerlinNoise(11+(u-1)*xScale,23+(v-1)*yScale),u);
                return Mathf.Lerp(a,b,v);
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                int i=y*size+x;
                float broad=Grain(u,v,11,13);
                float micro=Grain(u,v,steel?47:61,steel?6:57);
                float scoring=steel?Mathf.Max(0,Grain(u,v,86,3)-.70f):0;
                float worn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.56f,.79f,broad))*(.4f+.6f*micro);
                float shade=.79f+(broad-.5f)*.19f+(micro-.5f)*.16f-scoring*.38f+worn*.12f;
                color[i]=new Color32(Byte(shade),Byte(shade*(rubber?.99f:.98f)),Byte(shade*.96f),255);
                heights[i]=micro*(rubber?.50f:.30f)+broad*.11f-scoring*.38f;
                float smooth=steel?.44f+micro*.17f:rubber?.24f+micro*.15f:.35f+micro*.17f+worn*.07f;
                metal[i]=new Color32(Byte(steel?.81f+worn*.15f:rubber?.015f:.09f+worn*.25f),0,0,Byte(smooth));
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=heights[y*size+(x+1)%size]-heights[y*size+(x+size-1)%size];
                float dy=heights[((y+1)%size)*size+x]-heights[((y+size-1)%size)*size+x];
                var n=new Vector3(-dx*.72f,-dy*.72f,1).normalized;
                normal[y*size+x]=new Color32(Byte(.5f+n.x*.5f),Byte(.5f+n.y*.5f),Byte(.5f+n.z*.5f),255);
            }
            maps.albedo.SetPixels32(color);maps.albedo.Apply(true,true);
            maps.normal.SetPixels32(normal);maps.normal.Apply(true,true);
            maps.metalRoughness.SetPixels32(metal);maps.metalRoughness.Apply(true,true);
            return maps;
        }

        static void LauncherGrip(FoundryBuilder b,float z,float height=0)
        {
            b.Plate(Finish.LauncherRubber,new Vector3(0,height-.064f,z),new Vector3(.074f,.154f,.086f),.013f);
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,height+.020f,z),new Vector3(.072f,.034f,.096f),.005f);
            for(int i=0;i<5;i++)
                b.Plate(Finish.LauncherRubber,new Vector3(0,height-.119f+i*.022f,z+.045f),
                    new Vector3(.064f,.008f,.012f),.003f);
        }

        static void LauncherTrigger(FoundryBuilder b,float front=.156f)
        {
            b.Shape(Finish.LauncherGunmetal,mesh=>
            {
                var heel=new Vector3(0,-.065f,.038f);
                var toe=new Vector3(0,-.065f,front-.014f);
                mesh.Beam(heel,toe,.010f);
                mesh.Beam(toe,new Vector3(0,.047f,front),.010f);
            });
            b.Shape(Finish.LauncherSteel,mesh=>mesh.Beam(new Vector3(0,.04f,.092f),
                new Vector3(0,-.045f,.076f),.008f));
        }

        static void LauncherBolts(FoundryBuilder b,float x,float y,float z,float spacing)
        {
            b.Shape(Finish.LauncherSteel,mesh=>
            {
                for(int i=0;i<3;i++)
                {
                    var point=new Vector3(x,y,z+i*spacing);
                    mesh.Cone(point,point+Vector3.right*Mathf.Sign(x)*.005f,.008f,.007f,6);
                }
            });
        }

        static void ForgeRocket(FoundryBuilder b,FoundryBuilder feed)
        {
            float y=RocketMuzzle.y;
            // The rear bell, pressure tube and visible bore share the projectile axis.
            b.Profile(Finish.LauncherGunmetal,new Vector2(0,y),
                new Vector2(-.365f,.100f),new Vector2(-.365f,.125f),new Vector2(-.343f,.127f),
                new Vector2(-.225f,.079f),new Vector2(-.10f,.068f),new Vector2(.875f,.068f),
                new Vector2(.940f,.083f),new Vector2(RocketMuzzle.z,.083f),new Vector2(RocketMuzzle.z,.058f),
                new Vector2(.93f,.055f),new Vector2(-.21f,.052f),new Vector2(-.365f,.100f));
            b.Tube(Finish.LauncherOlive,y,-.18f,.48f,.086f,.065f);
            b.Tube(Finish.LauncherSteel,y,.96f,.997f,.088f,.068f);
            b.Tube(Finish.LauncherGunmetal,y,1.012f,RocketMuzzle.z,.093f,.058f);
            b.Tube(Finish.LauncherSteel,y,-.358f,-.333f,.128f,.113f);
            b.Tube(Finish.LauncherOlive,y,.515f,.815f,.081f,.067f);
            for(int i=0;i<7;i++)b.Tube(Finish.LauncherGunmetal,y,.53f+i*.040f,.545f+i*.040f,.087f,.077f);
            b.Plate(Finish.LauncherOlive,new Vector3(0,.091f,.18f),new Vector3(.196f,.096f,.40f));
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.110f,.67f),new Vector3(.110f,.075f,.20f),.008f);
            b.Plate(Finish.LauncherRubber,new Vector3(0,.069f,-.246f),new Vector3(.127f,.070f,.205f));
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.121f,-.244f),new Vector3(.098f,.047f,.154f),.005f);
            feed.Plate(Finish.LauncherGunmetal,new Vector3(0,.056f,.06f),new Vector3(.163f,.093f,.415f));
            feed.Plate(Finish.LauncherOlive,new Vector3(0,.016f,.058f),new Vector3(.174f,.022f,.425f));
            foreach(float side in new[]{-1f,1f})
            {
                b.Plate(Finish.LauncherGraphite,new Vector3(side*.101f,.137f,.178f),new Vector3(.018f,.085f,.34f),.005f);
                LauncherBolts(b,side*.108f,.113f,.054f,.112f);
                b.Shape(Finish.LauncherStencil,mesh=>
                {
                    for(int i=0;i<4;i++)mesh.Beam(new Vector3(side*.112f,.148f,.16f+i*.023f),
                        new Vector3(side*.112f,.172f,.175f+i*.023f),.0028f);
                    mesh.Box(new Vector3(side*.112f,.170f,.087f),new Vector3(.004f,.007f,.050f));
                });
            }
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.306f,.290f),new Vector3(.072f,.033f,.38f),.006f);
            for(int i=0;i<7;i++)b.Plate(Finish.LauncherSteel,new Vector3(0,.329f,.148f+i*.044f),
                new Vector3(.072f,.013f,.023f),.003f);
            b.Tube(Finish.LauncherGunmetal,.357f,.145f,.385f,.042f,.028f);
            b.Tube(Finish.LauncherSteel,.357f,.366f,.397f,.049f,.030f);
            b.Tube(Finish.LauncherGraphite,.357f,.159f,.168f,.029f,0);
            b.Tube(Finish.LauncherGraphite,.357f,.378f,.383f,.030f,0);
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.040f,0),new Vector3(.074f,.024f,.080f),.004f);
            LauncherGrip(b,0);
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.055f,RocketSupportDepth),
                new Vector3(.077f,.066f,.087f),.006f);
            LauncherGrip(b,RocketSupportDepth);
            LauncherTrigger(b);
        }

        static void ForgeGrenade(FoundryBuilder b,FoundryBuilder feed)
        {
            float axis=GrenadeMuzzle.y;
            float drum=GrenadeDrumOffset;
            b.Tube(Finish.LauncherGunmetal,axis,.275f+drum,GrenadeMuzzle.z,.071f,.043f);
            b.Tube(Finish.LauncherSteel,axis,.288f+drum,.335f+drum,.079f,.065f);
            b.Tube(Finish.LauncherGraphite,axis,.51f,.701f,.080f,.068f);
            for(int i=0;i<5;i++)b.Tube(Finish.LauncherGunmetal,axis,.52f+i*.041f,.533f+i*.041f,.089f,.076f);
            b.Tube(Finish.LauncherSteel,axis,.732f,.765f,.080f,.061f);
            b.Tube(Finish.LauncherGunmetal,axis,.767f,GrenadeMuzzle.z,.083f,.045f);
            // Leave real air around each chamber and through the front of the muzzle.
            feed.Tube(Finish.LauncherGunmetal,GrenadeDrumHeight,-.078f+drum,.278f+drum,.064f,.019f);
            for(int i=0;i<6;i++)
            {
                float a=Mathf.PI*.5f+i*Mathf.PI/3;
                float x=Mathf.Cos(a)*GrenadeChamberRadius,y=GrenadeDrumHeight+Mathf.Sin(a)*GrenadeChamberRadius;
                feed.Tube(Finish.LauncherGunmetal,y,-.037f+drum,.265f+drum,.046f,.035f,x);
                feed.Tube(Finish.LauncherSteel,y,-.050f+drum,-.019f+drum,.048f,.035f,x);
                feed.Tube(Finish.LauncherGraphite,y,.232f+drum,.280f+drum,.049f,.037f,x);
                var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                feed.Shape(Finish.LauncherGunmetal,spider=>
                {
                    foreach(float depth in new[]{.018f,.216f})
                    {
                        var hub=new Vector3(0,GrenadeDrumHeight,depth+drum);
                        spider.Beam(hub+radial*.035f,hub+radial*.077f,.013f);
                    }
                });
            }
            b.Plate(Finish.LauncherGraphite,new Vector3(0,.315f,.098f+drum),new Vector3(.224f,.061f,.40f));
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.095f,-.106f),new Vector3(.155f,.182f,.125f));
            b.Plate(Finish.LauncherGraphite,new Vector3(0,.137f,.040f),new Vector3(.119f,.087f,.20f));
            b.Plate(Finish.LauncherGraphite,new Vector3(0,.117f,.390f),new Vector3(.143f,.082f,.235f));
            b.Plate(Finish.LauncherRubber,new Vector3(0,.093f,-.320f),new Vector3(.116f,.164f,.065f),.009f);
            b.Shape(Finish.LauncherGunmetal,mesh=>
            {
                foreach(float side in new[]{-1f,1f})
                {
                    mesh.Beam(new Vector3(side*.042f,.153f,-.30f),new Vector3(side*.052f,.185f,-.102f),.015f);
                    mesh.Beam(new Vector3(side*.042f,.053f,-.31f),new Vector3(side*.052f,.083f,-.11f),.015f);
                }
            });
            foreach(float side in new[]{-1f,1f})
            {
                LauncherBolts(b,side*.110f,.321f,.126f,.106f);
                b.Plate(Finish.LauncherSteel,new Vector3(side*.087f,.128f,-.077f),
                    new Vector3(.018f,.042f,.065f),.004f);
                b.Shape(Finish.LauncherStencil,mesh=>
                {
                    for(int i=0;i<3;i++)mesh.Box(new Vector3(side*.115f,.309f,.168f+i*.026f),
                        new Vector3(.005f,.012f,.014f));
                    mesh.Box(new Vector3(side*.115f,.299f,.193f),new Vector3(.005f,.004f,.070f));
                });
            }
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.341f,.240f),new Vector3(.073f,.025f,.333f));
            for(int i=0;i<8;i++)b.Plate(Finish.LauncherGunmetal,new Vector3(0,.355f,.096f+i*.041f),
                new Vector3(.080f,.022f,.027f),.004f);
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.333f,.627f),
                new Vector3(.068f,.048f,.056f),.006f);
            b.Plate(Finish.LauncherSteel,new Vector3(0,.378f,.627f),new Vector3(.038f,.065f,.032f));
            b.Plate(Finish.LauncherStencil,new Vector3(0,.413f,.627f),new Vector3(.009f,.009f,.012f),.002f);
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.065f,-.008f),new Vector3(.073f,.079f,.075f),.006f);
            LauncherGrip(b,0);
            b.Plate(Finish.LauncherGunmetal,new Vector3(0,.100f,GrenadeSupportDepth),
                new Vector3(.088f,.146f,.094f),.007f);
            LauncherGrip(b,GrenadeSupportDepth);
            LauncherTrigger(b,.132f);
        }
        static void ForgeBat(FoundryBuilder b)
        {
            b.Profile(Finish.Walnut,Vector2.zero,new Vector2(-.17f,0),new Vector2(-.17f,.047f),new Vector2(-.147f,.050f),
                new Vector2(-.12f,.027f),new Vector2(.16f,.028f),new Vector2(.31f,.045f),new Vector2(.53f,.069f),
                new Vector2(.75f,.077f),new Vector2(.879f,.073f),new Vector2(.91f,.057f),new Vector2(.916f,0));
            for(int i=0;i<9;i++)b.Tube(Finish.Charcoal,0,-.117f+i*.024f,-.10f+i*.024f,.031f);
            b.Tube(Finish.Brass,0,.818f,.851f,.077f,.068f);
            b.Shape(Finish.Steel,wire=>{
                Vector3 Point(int i)
                {
                    float angle=i*.39f,z=.39f+i*.0048f;
                    float woodRadius;
                    if(z<.53f)woodRadius=Mathf.Lerp(.045f,.069f,(z-.31f)/.22f);
                    else if(z<.75f)woodRadius=Mathf.Lerp(.069f,.077f,(z-.53f)/.22f);
                    else woodRadius=Mathf.Lerp(.077f,.073f,(z-.75f)/.129f);
                    float radius=woodRadius+.006f;
                    return new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,z);
                }
                for(int i=0;i<98;i++)
                {
                    var p=Point(i);wire.Beam(p,Point(i+1),.008f);
                    if(i%8!=0)continue;var outwards=new Vector3(p.x,p.y,0).normalized;
                    wire.Cone(p,p+outwards*.048f+Vector3.forward*.018f,.008f,0,5);
                    wire.Cone(p,p+outwards*.039f-Vector3.forward*.020f,.007f,0,5);
                    if(i%16==0){var nail=p-outwards*.007f;wire.Cone(nail,nail+outwards*.077f,.010f,.001f,7);}
                }
            });
        }
        static void ForgeWrist(FoundryBuilder b,FoundryBuilder feed)
        {
            b.Profile(Finish.Steel,Vector2.zero,
                new Vector2(-.19f,.055f),new Vector2(-.19f,.069f),
                new Vector2(-.17f,.071f),new Vector2(-.035f,.057f),
                new Vector2(.025f,.053f),new Vector2(.035f,.050f),
                new Vector2(.035f,.039f),new Vector2(-.035f,.040f),
                new Vector2(-.19f,.055f));
            b.Tube(Finish.Charcoal,0,-.18f,-.16f,.072f,.055f);
            b.Tube(Finish.Charcoal,0,-.035f,-.015f,.058f,.040f);
            feed.Plate(Finish.Charcoal,new Vector3(0,.067f,-.065f),new Vector3(.110f,.055f,.19f));
            feed.Plate(Finish.Ceramic,new Vector3(0,.092f,-.075f),new Vector3(.073f,.009f,.115f),.002f);
            b.Plate(Finish.Charcoal,new Vector3(0,.069f,.048f),new Vector3(.074f,.069f,.155f),.007f);
            b.Tube(Finish.Steel,.12f,.075f,.163f,.035f,.026f);
            b.Tube(Finish.Amber,.12f,.163f,.17f,.026f);
            feed.Plate(Finish.Amber,new Vector3(0,.101f,-.075f),new Vector3(.047f,.010f,.075f),.0015f);
        }
        static void ForgeChainGun(FoundryBuilder b,FoundryBuilder feed)
        {
            const float axis = ChainGunBarrelHeight;
            b.Plate(Finish.Charcoal,new Vector3(0,axis,.14f),new Vector3(.33f,.25f,.49f));
            b.Tube(Finish.Steel,axis,-.19f,.24f,.14f);
            b.Tube(Finish.Charcoal,axis,-.215f,-.16f,.12f);
            b.Tube(Finish.Steel,axis,.32f,.49f,.151f,.118f);

            feed.Plate(Finish.Charcoal,new Vector3(.225f,axis-.025f,.12f),new Vector3(.19f,.24f,.35f));

            ForgeChainGunHandle(b,ChainGunRearGripDepth,.09f,axis+.10f);
            b.Shape(Finish.LauncherSteel,mesh=>
            {
                var left=new Vector3(-.042f,-.068f,ChainGunRearGripDepth+.015f);
                var right=new Vector3(.042f,-.068f,ChainGunRearGripDepth+.015f);
                mesh.Beam(left,right,.006f);
                mesh.Beam(left,left+Vector3.up*.021f,.005f);
                mesh.Beam(right,right+Vector3.up*.021f,.005f);
            });
            float frontHandle=ChainGunFrontGripDepth;
            b.Plate(Finish.Steel,new Vector3(0,axis+.103f,frontHandle),new Vector3(.106f,.094f,.061f),.007f);
            b.Plate(Finish.Steel,new Vector3(0,axis+.143f,frontHandle),new Vector3(.35f,.026f,.075f));
            ForgeChainGunHandle(b,frontHandle,.154f,axis+.11f);
        }
        static void ForgeChainGunHandle(FoundryBuilder b,float z,float halfWidth,float baseHeight)
        {
            const float handleHeight = -.031f;
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*halfWidth;
                b.Plate(Finish.Steel,new Vector3(x,(baseHeight+handleHeight)*.5f,z),
                    new Vector3(.04f,handleHeight-baseHeight+.035f,.048f),.007f);
            }
            b.Shape(Finish.Charcoal,handle=>handle.Cone(new Vector3(-halfWidth,handleHeight,z),
                new Vector3(halfWidth,handleHeight,z),.027f,.027f,16));
        }
        static void ForgeChainsaw(FoundryBuilder b,FoundryBuilder feed)
        {
            b.Plate(Finish.Ceramic,new Vector3(0,.045f,.20f),new Vector3(.25f,.22f,.36f));
            b.Plate(Finish.Steel,new Vector3(0,.158f,.22f),new Vector3(.108f,.034f,.31f),.004f);
            feed.Plate(Finish.Charcoal,new Vector3(0,.19f,.22f),new Vector3(.115f,.047f,.31f));
            b.Plate(Finish.Steel,new Vector3(0,0,.63f),new Vector3(.056f,.18f,.59f));
            for(int i=0;i<12;i++)foreach(float y in new[]{-.092f,.092f})
                b.Plate(Finish.Charcoal,new Vector3(0,y,.42f+i*.041f),new Vector3(.066f,.027f,.024f),.003f);
            Grip(b,-.014f);
            b.Shape(Finish.Charcoal,mesh=>
            {
                var left=new Vector3(-.107f,SawBailHeight,SawBailDepth);
                var right=new Vector3(.107f,SawBailHeight,SawBailDepth);
                mesh.Beam(left,right,.027f);
                mesh.Beam(left,new Vector3(-.139f,.253f,SawBailDepth),.020f);
                mesh.Beam(right,new Vector3(.139f,.253f,SawBailDepth),.020f);
                foreach(float side in new[]{-1f,1f})
                    mesh.Beam(new Vector3(side*.139f,.253f,SawBailDepth),
                        new Vector3(side*.113f,.101f,SawBailDepth),.018f);
            });
            b.Shape(Finish.Steel,mesh=>mesh.Beam(new Vector3(0,-.046f,.050f),
                new Vector3(0,-.096f,.056f),.006f));
        }
        static void ForgeSawStaff(FoundryBuilder b)
        {
            b.Tube(Finish.Charcoal,0,-.18f,.83f,.034f);b.Tube(Finish.Brass,0,.56f,.72f,.047f);
            b.Plate(Finish.Steel,new Vector3(0,0,.79f),new Vector3(.134f,.095f,.175f));
            for(int i=0;i<7;i++)b.Tube(Finish.Steel,0,-.12f+i*.038f,-.105f+i*.038f,.037f);
        }
        static Mesh DaggerSurface(bool cuttingEdge)
        {
            var sections=new Vector3[]
            {
                new(.095f,.038f,.010f),new(.122f,.048f,.011f),new(.23f,.044f,.009f),
                new(.31f,.031f,.006f),new(.37f,.015f,.003f),new(.414f,.0003f,.0003f)
            };
            var vertices=new List<Vector3>();
            var uv=new List<Vector2>();
            var triangles=new List<int>();
            Vector3 Point(Vector3 ring,int side) => (side%8) switch
            {
                0 => new(ring.z,0,ring.x), 1 => new(ring.z*.65f,ring.y*.67f,ring.x),
                2 => new(0,ring.y,ring.x), 3 => new(-ring.z*.65f,ring.y*.67f,ring.x),
                4 => new(-ring.z,0,ring.x), 5 => new(-ring.z*.65f,-ring.y*.67f,ring.x),
                6 => new(0,-ring.y,ring.x), _ => new(ring.z*.65f,-ring.y*.67f,ring.x)
            };
            for(int j=0;j<sections.Length-1;j++)
            for(int side=0;side<8;side++)
            {
                if((side%4==1||side%4==2)!=cuttingEdge)continue;
                int start=vertices.Count;
                var a=Point(sections[j],side);var b=Point(sections[j],side+1);
                var c=Point(sections[j+1],side);var d=Point(sections[j+1],side+1);
                vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                foreach(var point in new[]{a,b,c,d})uv.Add(new Vector2(point.y*9f+.5f,point.z*2.5f));
                triangles.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
            }
            var mesh=new Mesh{name=cuttingEdge?"Honed dagger edges":"Taper-ground dagger faces"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();
            return mesh;
        }
        const float AxeWrapRadius = .032f;

        static void ForgeStormbreaker(FoundryBuilder b, FoundryBuilder feed)
        {
            b.Profile(Finish.Walnut,Vector2.zero,new(-.179f,0),new(-.175f,.032f),
                new(-.140f,.037f),new(-.108f,.029f),new(.110f,.029f),new(.24f,.030f),
                new(.53f,.029f),new(.66f,.038f),new(.798f,.046f),new(.834f,0));
            b.Tube(Finish.AxeBronze,0,-.184f,-.145f,.038f);
            foreach(float center in new[]{0f,AxeSupportDepth})
            {
                b.Tube(Finish.AxeLeather,0,center-.098f,center+.098f,AxeWrapRadius);
                b.Tube(Finish.AxeBronze,0,center-.108f,center-.092f,.035f);
                b.Tube(Finish.AxeBronze,0,center+.092f,center+.108f,.035f);
                b.Shape(Finish.AxeLeather,mesh=>
                {
                    Vector3 At(int i)
                    {
                        float angle=i*Mathf.PI/8;
                        return new Vector3(Mathf.Cos(angle)*(AxeWrapRadius-.0008f),
                            Mathf.Sin(angle)*(AxeWrapRadius-.0008f),center-.091f+i*.00142f);
                    }
                    for(int i=0;i<128;i++)mesh.Cone(At(i),At(i+1),.0018f,.0018f,4);
                });
            }
            b.Tube(Finish.AxeBronze,0,.590f,.648f,.046f);
            b.Tube(Finish.Steel,0,.646f,.767f,.051f);
            b.Add(Finish.Steel,FoundrySolid("Forged axe eye and hammer poll",.116f,.012f,new[]{
                new Vector2(.050f,.726f),new Vector2(.060f,.902f),new Vector2(.008f,.941f),
                new Vector2(-.077f,.912f),new Vector2(-.148f,.881f),new Vector2(-.224f,.898f),
                new Vector2(-.246f,.869f),new Vector2(-.246f,.784f),new Vector2(-.220f,.755f),
                new Vector2(-.140f,.778f),new Vector2(-.058f,.746f)
            }),Matrix4x4.identity);
            b.Add(Finish.BladeSteel,FoundrySolid("Forged axe cheek",.090f,.012f,new[]{
                new Vector2(-.036f,.707f),new Vector2(.104f,.698f),new Vector2(.290f,.619f),
                new Vector2(.344f,.695f),new Vector2(.374f,.827f),new Vector2(.356f,.962f),
                new Vector2(.305f,1.031f),new Vector2(.092f,.967f),new Vector2(-.042f,.923f)
            }),Matrix4x4.identity);
            // The edge is ground in two bevels from the thick cheek to a narrow cutting line.
            b.Add(Finish.BladeEdge,StormbreakerBevel(),Matrix4x4.identity);
            b.Plate(Finish.BladeSteel,new Vector3(0,-.237f,.827f),new Vector3(.111f,.035f,.132f),.007f);
            b.Plate(Finish.AxeBronze,new Vector3(0,-.215f,.827f),new Vector3(.115f,.012f,.134f),.003f);
            b.Plate(Finish.AxeBronze,new Vector3(0,.167f,.822f),new Vector3(.100f,.159f,.166f),.009f);
            b.Plate(Finish.Charcoal,new Vector3(0,.167f,.822f),new Vector3(.105f,.143f,.149f),.008f);
            foreach(float side in new[]{-1f,1f})
            {
                b.Shape(Finish.AxeBronze,mesh=>
                {
                    Vector3 Knot(int i)
                    {
                        float angle=i*(2*Mathf.PI/72);
                        float y=(Mathf.Sin(angle)+2*Mathf.Sin(2*angle))*.022f;
                        float z=(Mathf.Cos(angle)-2*Mathf.Cos(2*angle))*.022f;
                        return new Vector3(side*(.054f+.002f*Mathf.Cos(3*angle)),.167f+y,.822f+z);
                    }
                    for(int i=0;i<72;i++)mesh.Cone(Knot(i),Knot(i+1),.0028f,.0028f,5);
                    for(int i=0;i<12;i++)
                    {
                        float angle=i*Mathf.PI/6;
                        var stud=new Vector3(side*.051f,.167f+Mathf.Cos(angle)*.067f,
                            .822f+Mathf.Sin(angle)*.070f);
                        mesh.Cone(stud,stud+Vector3.right*side*.004f,.0034f,.0024f,6);
                    }
                    foreach(float z in new[]{.742f,.904f})
                    {
                        var pin=new Vector3(side*.043f,.075f,z);
                        mesh.Cone(pin,pin+Vector3.right*side*.008f,.009f,.007f,10);
                    }
                });
                feed.Plate(Finish.Cyan,new Vector3(side*.046f,.323f,.889f),
                    new Vector3(.003f,.012f,.088f),.001f);
            }
        }

        static Mesh StormbreakerBevel()
        {
            Vector2[] inner={
                new(.284f,.624f),new(.340f,.695f),new(.371f,.827f),new(.352f,.962f),new(.301f,1.027f)
            };
            Vector2[] edge={
                new(.314f,.601f),new(.389f,.670f),new(.429f,.827f),new(.407f,.974f),new(.329f,1.056f)
            };
            var vertices=new List<Vector3>();
            var uv=new List<Vector2>();
            var triangles=new List<int>();
            foreach(float side in new[]{-1f,1f})
            {
                int start=vertices.Count;
                for(int i=0;i<inner.Length;i++)
                {
                    vertices.Add(new Vector3(side*.036f,inner[i].x,inner[i].y));
                    vertices.Add(new Vector3(0,edge[i].x,edge[i].y));
                    uv.Add(inner[i]*4);
                    uv.Add(edge[i]*4);
                    if(i==0)continue;
                    int a=start+(i-1)*2;
                    if(side<0)triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});
                    else triangles.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});
                }
            }
            var mesh=new Mesh{name="Ground Stormbreaker cutting bevel"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();
            return mesh;
        }

        static void ForgeBlade(FoundryBuilder b,FoundryBuilder feed,bool axe)
        {
            if(axe)ForgeStormbreaker(b,feed);
            else
            {
                b.Profile(Finish.Charcoal,Vector2.zero,new(-.16f,.031f),new(-.135f,.034f),
                    new(-.10f,.028f),new(-.03f,.030f),new(.03f,.027f),new(.074f,.032f));
                b.Tube(Finish.Steel,0,-.172f,-.141f,.036f);
                for(int i=0;i<8;i++)b.Tube(Finish.Charcoal,0,-.124f+i*.024f,-.116f+i*.024f,.033f);
                b.Plate(Finish.Steel,new Vector3(0,0,.083f),new Vector3(.049f,.128f,.031f),.006f);
                b.Tube(Finish.Brass,0,.065f,.085f,.036f);
                b.Add(Finish.BladeSteel,DaggerSurface(false),Matrix4x4.identity);
                b.Add(Finish.BladeEdge,DaggerSurface(true),Matrix4x4.identity);
            }
        }
    }
}
