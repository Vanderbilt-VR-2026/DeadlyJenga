using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        enum Finish { Steel, Charcoal, Ceramic, Brass, Walnut, Violet, Cyan }
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
                rotor.localPosition=kind==EquipmentKind.ChainGun?new Vector3(0,.19f,.39f):new Vector3(0,0,kind==EquipmentKind.Chainsaw?.79f:.88f);
                if(!arsenalMeshes.TryGetValue(key,out var mesh))
                {
                    var b=new CandyAccessoryMeshes.Builder();
                    if(kind==EquipmentKind.ChainGun)
                        for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Cos(a)*.083f,Mathf.Sin(a)*.083f,0);b.Cone(p,p+Vector3.forward*.58f,.022f,.022f,12);b.Cone(p+Vector3.forward*.52f,p+Vector3.forward*.61f,.028f,.026f,12);}
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
            Color color=finish==Finish.Steel?new Color(.44f,.52f,.59f):finish==Finish.Charcoal?new Color(.06f,.075f,.09f):
                finish==Finish.Ceramic?new Color(.85f,.24f,.06f):finish==Finish.Brass?new Color(.82f,.55f,.22f):
                finish==Finish.Walnut?new Color(.53f,.25f,.105f):finish==Finish.Violet?new Color(.42f,.16f,.84f):new Color(.10f,.82f,1);
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Foundry "+finish,enableInstancing=true,color=color};
            bool wood=finish==Finish.Walnut;bool luminous=finish==Finish.Cyan||finish==Finish.Violet;
            mat.SetFloat("_Metallic",finish==Finish.Steel||finish==Finish.Brass?.78f:finish==Finish.Charcoal?.28f:.04f);
            mat.SetFloat("_Smoothness",wood?.46f:finish==Finish.Charcoal?.34f:.72f);
            const int n=96;var tex=new Texture2D(n,n,TextureFormat.RGBA32,true){name=mat.name+" surface"};var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=x/(float)n,v=y/(float)n;
                float noise=Mathf.PerlinNoise(u*16,v*23);
                float grain=wood? .68f+.24f*Mathf.Sin((u*28+Mathf.PerlinNoise(u*3,v*2)*1.5f)*Mathf.PI*2)+noise*.13f:
                    .78f+noise*.19f+.055f*Mathf.Sin(y*2.6f+Mathf.Sin(x*.12f));
                if(finish==Finish.Charcoal)grain-=Mathf.Pow(Mathf.Max(0,Mathf.Sin(x*.8f)*Mathf.Sin(y*.8f)),6)*.18f;
                pixels[y*n+x]=new Color(grain,grain,grain,1);
            }
            tex.SetPixels(pixels);tex.Apply(true,true);mat.SetTexture("_BaseMap",tex);
            if(luminous){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*1.5f);}
            return finishes[finish]=mat;
        }
        static void Grip(FoundryBuilder b,float z=0)
        {
            b.Plate(Finish.Charcoal,new Vector3(0,-.066f,z),new Vector3(.094f,.185f,.11f));
            for(int i=0;i<4;i++)b.Plate(Finish.Steel,new Vector3(0,-.13f+i*.029f,z-.047f),new Vector3(.082f,.009f,.018f),.002f);
        }
        static void ForgeRocket(FoundryBuilder b,FoundryBuilder feed)
        {
            foreach(float x in new[]{-.084f,.084f})
            {
                b.Tube(Finish.Steel,.20f,-.49f,1.04f,.083f,.055f,x);
                b.Tube(Finish.Charcoal,.20f,.91f,1.085f,.101f,.060f,x);
                b.Tube(Finish.Brass,.20f,1.045f,1.10f,.097f,.063f,x);
                b.Tube(Finish.Charcoal,.20f,-.525f,-.39f,.102f,.063f,x);
            }
            b.Plate(Finish.Ceramic,new Vector3(0,.19f,.33f),new Vector3(.38f,.28f,.56f));
            feed.Plate(Finish.Charcoal,new Vector3(0,.05f,.09f),new Vector3(.20f,.12f,.52f));
            b.Plate(Finish.Charcoal,new Vector3(0,.01f,-.34f),new Vector3(.26f,.15f,.31f));
            foreach(float side in new[]{-1f,1f})
            {
                b.Plate(Finish.Steel,new Vector3(side*.197f,.21f,.31f),new Vector3(.025f,.18f,.43f),.005f);
                b.Plate(Finish.Cyan,new Vector3(side*.211f,.24f,.29f),new Vector3(.012f,.022f,.22f),.002f);
                for(int i=0;i<5;i++)b.Plate(Finish.Charcoal,new Vector3(side*.215f,.18f,.18f+i*.046f),new Vector3(.012f,.035f,.022f),.002f);
            }
            b.Plate(Finish.Charcoal,new Vector3(0,.46f,.29f),new Vector3(.084f,.045f,.38f));
            foreach(float z in new[]{.14f,.45f})b.Plate(Finish.Steel,new Vector3(0,.397f,z),new Vector3(.08f,.117f,.054f));
            b.Tube(Finish.Charcoal,.36f,.49f,.74f,.048f,.031f,-.125f);
            b.Tube(Finish.Cyan,.36f,.719f,.727f,.031f,0,-.125f);
            b.Plate(Finish.Charcoal,new Vector3(0,.073f,.625f),new Vector3(.155f,.13f,.32f));
            b.Plate(Finish.Steel,new Vector3(0,.127f,.67f),new Vector3(.25f,.07f,.22f));
            b.Shape(Finish.Brass,nails=>{foreach(float x in new[]{-.221f,.221f})for(int i=0;i<4;i++){var p=new Vector3(x,.255f,.132f+i*.106f);nails.Cone(p,p+Vector3.right*Mathf.Sign(x)*.009f,.012f,.009f,8);}});
            Grip(b);Grip(b,.69f);
        }
        static void ForgeGrenade(FoundryBuilder b,FoundryBuilder feed)
        {
            b.Tube(Finish.Steel,.12f,.28f,.77f,.077f,.055f);b.Tube(Finish.Charcoal,.12f,.69f,.82f,.108f,.057f);
            b.Tube(Finish.Violet,.12f,.766f,.79f,.110f,.057f);feed.Tube(Finish.Charcoal,.12f,-.075f,.27f,.15f);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3;float x=Mathf.Cos(a)*.115f,y=.12f+Mathf.Sin(a)*.115f;
                feed.Tube(Finish.Steel,y,-.035f,.27f,.046f,.035f,x);feed.Tube(Finish.Brass,y,.025f,.22f,.036f,0,x);
                feed.Tube(Finish.Violet,y,.225f,.25f,.036f,0,x);
            }
            b.Plate(Finish.Ceramic,new Vector3(0,.33f,.145f),new Vector3(.26f,.11f,.46f));
            b.Plate(Finish.Charcoal,new Vector3(0,.05f,-.32f),new Vector3(.123f,.145f,.40f));
            b.Plate(Finish.Steel,new Vector3(0,.035f,.52f),new Vector3(.128f,.10f,.28f));
            for(int i=0;i<7;i++)b.Plate(Finish.Charcoal,new Vector3(0,.401f,-.024f+i*.057f),new Vector3(.082f,.031f,.032f),.004f);
            Grip(b);
        }
        static void ForgeBat(FoundryBuilder b)
        {
            b.Profile(Finish.Walnut,Vector2.zero,new Vector2(-.17f,0),new Vector2(-.17f,.047f),new Vector2(-.147f,.050f),
                new Vector2(-.12f,.027f),new Vector2(.16f,.028f),new Vector2(.31f,.045f),new Vector2(.53f,.069f),
                new Vector2(.75f,.077f),new Vector2(.879f,.073f),new Vector2(.91f,.057f),new Vector2(.916f,0));
            for(int i=0;i<9;i++)b.Tube(Finish.Charcoal,0,-.117f+i*.024f,-.10f+i*.024f,.031f);
            b.Tube(Finish.Brass,0,.818f,.851f,.077f,.068f);
            b.Shape(Finish.Steel,wire=>{
                Vector3 Point(int i){float a=i*.39f,z=.39f+i*.0048f,r=Mathf.Lerp(.057f,.078f,i/98f);return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z);}
                for(int i=0;i<98;i++)
                {
                    var p=Point(i);wire.Beam(p,Point(i+1),.005f);
                    if(i%8!=0)continue;var outwards=new Vector3(p.x,p.y,0).normalized;
                    wire.Cone(p,p+outwards*.033f+Vector3.forward*.014f,.008f,0,5);
                    wire.Cone(p,p+outwards*.026f-Vector3.forward*.017f,.007f,0,5);
                    if(i%16==0){var nail=p-outwards*.007f;wire.Cone(nail,nail+outwards*.057f,.008f,.001f,7);}
                }
            });
        }
        static void ForgeWrist(FoundryBuilder b,FoundryBuilder feed)
        {
            b.Tube(Finish.Steel,0,-.24f,.04f,.114f,.087f);
            foreach(float z in new[]{-.22f,-.018f})b.Tube(Finish.Charcoal,0,z,z+.045f,.122f,.087f);
            feed.Plate(Finish.Ceramic,new Vector3(0,.105f,-.042f),new Vector3(.17f,.095f,.29f));
            b.Tube(Finish.Brass,.105f,.07f,.158f,.050f,.033f);b.Tube(Finish.Cyan,.105f,.144f,.15f,.032f);
            feed.Plate(Finish.Cyan,new Vector3(0,.156f,-.07f),new Vector3(.075f,.009f,.117f),.002f);
        }
        static void ForgeChainGun(FoundryBuilder b,FoundryBuilder feed)
        {
            b.Plate(Finish.Charcoal,new Vector3(0,.17f,.16f),new Vector3(.33f,.31f,.46f));
            b.Plate(Finish.Ceramic,new Vector3(0,.26f,.14f),new Vector3(.35f,.22f,.42f));
            b.Tube(Finish.Steel,.19f,.36f,.50f,.151f,.118f);b.Tube(Finish.Charcoal,.19f,.82f,.89f,.134f,.114f);
            feed.Plate(Finish.Charcoal,new Vector3(.23f,.14f,.13f),new Vector3(.20f,.27f,.35f));
            for(int i=0;i<6;i++)feed.Tube(Finish.Brass,.22f,.07f,.28f,.019f,0,.176f+i*.024f);
            b.Plate(Finish.Charcoal,new Vector3(0,.057f,.43f),new Vector3(.139f,.088f,.25f));
            Grip(b);Grip(b,.46f);
        }
        static void ForgeChainsaw(FoundryBuilder b,FoundryBuilder feed)
        {
            b.Plate(Finish.Ceramic,new Vector3(0,.045f,.20f),new Vector3(.25f,.22f,.36f));
            feed.Plate(Finish.Charcoal,new Vector3(0,.19f,.22f),new Vector3(.115f,.047f,.31f));
            b.Plate(Finish.Steel,new Vector3(0,0,.63f),new Vector3(.056f,.18f,.59f));
            for(int i=0;i<12;i++)foreach(float y in new[]{-.092f,.092f})
                b.Plate(Finish.Charcoal,new Vector3(0,y,.42f+i*.041f),new Vector3(.066f,.027f,.024f),.003f);
            Grip(b);
        }
        static void ForgeSawStaff(FoundryBuilder b)
        {
            b.Tube(Finish.Charcoal,0,-.18f,.83f,.034f);b.Tube(Finish.Brass,0,.56f,.72f,.047f);
            b.Plate(Finish.Steel,new Vector3(0,0,.79f),new Vector3(.134f,.095f,.175f));
            for(int i=0;i<7;i++)b.Tube(Finish.Steel,0,-.12f+i*.038f,-.105f+i*.038f,.037f);
        }
        static void ForgeBlade(FoundryBuilder b,FoundryBuilder feed,bool axe)
        {
            if(axe)
            {
                b.Tube(Finish.Walnut,0,-.16f,.77f,.036f);b.Tube(Finish.Brass,0,.52f,.72f,.052f);
                b.Add(Finish.Steel,FoundrySolid("Swept forged axe head",.071f,.011f,new[]{new Vector2(-.36f,.96f),new Vector2(-.42f,.86f),new Vector2(-.085f,.69f),new Vector2(.085f,.69f),new Vector2(.42f,.86f),new Vector2(.36f,.96f),new Vector2(0,1.055f)}),Matrix4x4.identity);
                feed.Plate(Finish.Cyan,new Vector3(0,0,.986f),new Vector3(.025f,.49f,.023f),.004f);
            }
            else
            {
                b.Tube(Finish.Charcoal,0,-.13f,.085f,.034f);b.Tube(Finish.Brass,0,-.15f,-.12f,.044f);
                b.Plate(Finish.Brass,new Vector3(0,0,.084f),new Vector3(.055f,.155f,.040f));
                b.Add(Finish.Steel,FoundrySolid("Forged dagger blade",.034f,.006f,new[]{new Vector2(-.053f,.10f),new Vector2(.053f,.10f),new Vector2(.044f,.27f),new Vector2(0,.405f),new Vector2(-.044f,.27f)}),Matrix4x4.identity);
            }
        }
    }
}
