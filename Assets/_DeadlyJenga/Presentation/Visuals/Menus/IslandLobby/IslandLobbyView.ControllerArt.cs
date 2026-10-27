using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        Material controllerPlastic,controllerRubber;
        Texture2D controllerGrain;
        readonly List<Mesh> controllerMeshes=new List<Mesh>();

        void BuildControllerArtwork(Transform parent,bool game,Color accent)
        {
            EnsureControllerMaterials();
            var ink=new Color(.055f,.045f,.085f);
            var metal=new Color(.48f,.53f,.61f);
            for(int side=-1;side<=1;side+=2)
            {
                var well=ControlLathe(parent,"Decorative control surround",new Vector3(side*.50f,0,0),
                    new[]{new Vector2(0,.012f),new Vector2(.30f,.012f),new Vector2(.32f,-.005f),new Vector2(.30f,-.022f),new Vector2(0,-.022f)},ink);
                ControlRing(well.transform,"Decorative accent rim",.294f,.008f,-.024f,accent);
            }
            if(game)
            {
                var stick=new GameObject("Xbox style thumbstick").transform;stick.SetParent(parent,false);stick.localPosition=new Vector3(-.50f,0,-.025f);
                ControlLathe(stick,"Decorative thumbstick rubber boot",Vector3.zero,new[]{new Vector2(0,0),new Vector2(.19f,0),new Vector2(.18f,-.025f),new Vector2(.085f,-.065f),new Vector2(0,-.065f)},ink,true);
                ControlLathe(stick,"Decorative short thumbstick shaft",Vector3.zero,new[]{new Vector2(0,-.04f),new Vector2(.065f,-.04f),new Vector2(.065f,-.115f),new Vector2(0,-.115f)},metal);
                ControlLathe(stick,"Decorative concave thumbstick cap",Vector3.zero,new[]{new Vector2(0,-.10f),new Vector2(.125f,-.10f),new Vector2(.174f,-.13f),new Vector2(.182f,-.17f),new Vector2(.165f,-.19f),new Vector2(.13f,-.18f),new Vector2(.08f,-.16f),new Vector2(0,-.155f)},ink,true,true);
                ControlRing(stick,"Decorative thumbstick grip seam",.153f,.003f,-.184f,new Color(.22f,.23f,.28f));
                string[] names={"A","B","X","Y"};
                Color[] colors={new Color(.10f,.84f,.30f),new Color(.96f,.10f,.20f),new Color(.10f,.43f,1),new Color(1,.76f,.055f)};
                Vector2[] offsets={new Vector2(0,-.19f),new Vector2(.19f,0),new Vector2(-.19f,0),new Vector2(0,.19f)};
                for(int i=0;i<4;i++)
                {
                    var button=new GameObject("Decorative "+names[i]+" button assembly").transform;button.SetParent(parent,false);
                    button.localPosition=new Vector3(.50f+offsets[i].x,offsets[i].y,-.027f);
                    ControlLathe(button,"Decorative "+names[i]+" socket",Vector3.zero,new[]{new Vector2(0,0),new Vector2(.100f,0),new Vector2(.105f,-.018f),new Vector2(.09f,-.028f),new Vector2(0,-.028f)},metal);
                    ControlLathe(button,"Decorative "+names[i]+" candy cap",Vector3.zero,new[]{new Vector2(0,-.012f),new Vector2(.083f,-.012f),new Vector2(.088f,-.042f),new Vector2(.078f,-.066f),new Vector2(.060f,-.076f),new Vector2(0,-.079f)},colors[i]);
                    ControlLetter(button,names[i][0],-.081f,.069f,Color.white);
                }
            }
            else
            {
                var pad=new GameObject("Directional pad").transform;pad.SetParent(parent,false);pad.localPosition=new Vector3(-.50f,0,-.025f);
                BuildDpad(pad,ink);
                var wheel=ControlLathe(parent,"Decorative knurled scroll wheel",new Vector3(.50f,0,-.026f),new[]{new Vector2(0,0),new Vector2(.235f,0),new Vector2(.249f,-.025f),new Vector2(.249f,-.075f),new Vector2(.22f,-.095f),new Vector2(.14f,-.10f),new Vector2(0,-.10f)},metal,false,true);
                ControlLathe(wheel.transform,"Decorative scroll wheel inset",Vector3.zero,new[]{new Vector2(0,-.101f),new Vector2(.157f,-.101f),new Vector2(.15f,-.108f),new Vector2(0,-.108f)},ink,true);
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;
                    var tick=Box(wheel.transform,"Decorative scroll wheel marker",new Vector3(Mathf.Sin(a)*.19f,Mathf.Cos(a)*.19f,-.104f),new Vector3(.013f,.027f,.005f),i==0?accent:new Color(.66f,.69f,.75f));
                    tick.transform.localRotation=Quaternion.Euler(0,0,-i*30);ApplyControllerFinish(tick.gameObject,false);
                }
            }
            foreach(float x in new[]{-.82f,.82f})foreach(float y in new[]{-.31f,.31f})
                ControlLathe(parent,"Decorative recessed screw",new Vector3(x,y,-.006f),new[]{new Vector2(0,0),new Vector2(.018f,0),new Vector2(.018f,-.008f),new Vector2(0,-.008f)},metal);
        }

        void EnsureControllerMaterials()
        {
            if(controllerPlastic)return;
            controllerGrain=new Texture2D(64,64,TextureFormat.RGBA32,true){name="Controller fine molded grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[4096];uint random=1729;
            for(int i=0;i<pixels.Length;i++){random=random*1664525+1013904223;float v=.91f+((random>>16)&255)/255f*.09f;pixels[i]=new Color(v,v,v,1);}
            controllerGrain.SetPixels(pixels);controllerGrain.Apply(true,true);
            controllerPlastic=new Material(keyboardMaterial){name="Polished candy controller plastic"};
            controllerPlastic.SetTexture("_BaseMap",controllerGrain);controllerPlastic.SetTextureScale("_BaseMap",new Vector2(5,5));
            controllerPlastic.SetFloat("_Smoothness",.74f);controllerPlastic.SetFloat("_Metallic",.16f);
            controllerRubber=new Material(controllerPlastic){name="Fine textured thumbstick rubber"};controllerRubber.SetFloat("_Smoothness",.24f);controllerRubber.SetFloat("_Metallic",0);
        }
        void ApplyControllerFinish(GameObject target,bool rubber)
        {EnsureControllerMaterials();target.GetComponent<MeshRenderer>().sharedMaterial=rubber?controllerRubber:controllerPlastic;}
        GameObject ControlMesh(Transform parent,string name,Vector3 position,Mesh mesh,Color color,bool rubber=false)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;controllerMeshes.Add(mesh);ApplyControllerFinish(go,rubber);
            var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",color);go.GetComponent<MeshRenderer>().SetPropertyBlock(properties);return go;
        }
        GameObject ControlLathe(Transform parent,string name,Vector3 position,Vector2[] profile,Color color,bool rubber=false,bool grip=false)
        {
            const int slices=64;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int row=0;row<profile.Length;row++)for(int i=0;i<=slices;i++)
            {
                float angle=i*Mathf.PI*2/slices;float radius=profile[row].x;
                if(grip&&radius>.16f)radius+=Mathf.Cos(angle*32)*.0025f;
                vertices.Add(new Vector3(Mathf.Sin(angle)*radius,Mathf.Cos(angle)*radius,profile[row].y));uv.Add(new Vector2(i/(float)slices,row/(float)(profile.Length-1)));
            }
            for(int row=0;row<profile.Length-1;row++)for(int i=0;i<slices;i++)
            {int a=row*(slices+1)+i,b=a+slices+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return ControlMesh(parent,name,position,mesh,color,rubber);
        }
        void ControlRing(Transform parent,string name,float radius,float thickness,float z,Color color)
        {ControlLathe(parent,name,Vector3.zero,new[]{new Vector2(radius-thickness,z),new Vector2(radius,z+.003f),new Vector2(radius+thickness,z),new Vector2(radius,z-.003f),new Vector2(radius-thickness,z)},color);}
        void BuildDpad(Transform parent,Color color)
        {
            Vector2[] shape={new Vector2(-.08f,.24f),new Vector2(.08f,.24f),new Vector2(.08f,.08f),new Vector2(.24f,.08f),new Vector2(.24f,-.08f),new Vector2(.08f,-.08f),new Vector2(.08f,-.24f),new Vector2(-.08f,-.24f),new Vector2(-.08f,-.08f),new Vector2(-.24f,-.08f),new Vector2(-.24f,.08f),new Vector2(-.08f,.08f)};
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int ring=0;ring<3;ring++)foreach(var p in shape){var v=p*(ring==2?.93f:1);vertices.Add(new Vector3(v.x,v.y,ring==0?0:ring==1?-.06f:-.075f));uv.Add(v*2+Vector2.one*.5f);}
            for(int ring=0;ring<2;ring++)for(int i=0;i<12;i++){int a=ring*12+i,b=ring*12+(i+1)%12;triangles.AddRange(new[]{a,b,a+12,b,b+12,a+12});}
            int center=vertices.Count;vertices.Add(new Vector3(0,0,-.075f));uv.Add(Vector2.one*.5f);
            for(int i=0;i<12;i++)triangles.AddRange(new[]{center,24+i,24+(i+1)%12});
            var mesh=new Mesh{name="Beveled directional cross"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            ControlMesh(parent,"Decorative beveled D-pad",Vector3.zero,mesh,color,true);
            foreach(var d in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
            {var mark=Box(parent,"Decorative directional notch",new Vector3(d.x*.18f,d.y*.18f,-.078f),new Vector3(d.x==0?.035f:.015f,d.y==0?.035f:.015f,.004f),new Color(.32f,.34f,.40f));ApplyControllerFinish(mark.gameObject,false);}
        }
        void ControlLetter(Transform parent,char letter,float z,float height,Color color)
        {
            var lines=new List<Vector2[]>();
            void Line(float ax,float ay,float bx,float by)=>lines.Add(new[]{new Vector2(ax,ay),new Vector2(bx,by)});
            if(letter=='A'){Line(-.32f,-.5f,0,.5f);Line(0,.5f,.32f,-.5f);Line(-.20f,-.12f,.20f,-.12f);}
            if(letter=='X'){Line(-.32f,-.5f,.32f,.5f);Line(-.32f,.5f,.32f,-.5f);}
            if(letter=='Y'){Line(-.32f,.5f,0,.04f);Line(.32f,.5f,0,.04f);Line(0,.04f,0,-.5f);}
            if(letter=='B')
            {Line(-.28f,-.5f,-.28f,.5f);Vector2[] points={new Vector2(-.28f,.5f),new Vector2(.16f,.5f),new Vector2(.31f,.32f),new Vector2(.16f,.04f),new Vector2(-.28f,.04f),new Vector2(.19f,.04f),new Vector2(.34f,-.22f),new Vector2(.18f,-.5f),new Vector2(-.28f,-.5f)};for(int i=0;i<points.Length-1;i++)lines.Add(new[]{points[i],points[i+1]});}
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            foreach(var line in lines)
            {
                var a=line[0]*height;var b=line[1]*height;var n=new Vector2(-(b-a).y,(b-a).x).normalized*height*.055f;int start=vertices.Count;
                foreach(var p in new[]{a-n,a+n,b+n,b-n}){vertices.Add(new Vector3(p.x,p.y,z));uv.Add(Vector2.one*.5f);}
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            var mesh=new Mesh{name=letter+" fitted cap legend"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            ControlMesh(parent,"Decorative "+letter+" fitted legend",Vector3.zero,mesh,color);
        }
        void DisposeControllerArtwork()
        {foreach(var mesh in controllerMeshes)if(mesh)Destroy(mesh);controllerMeshes.Clear();if(controllerPlastic)Destroy(controllerPlastic);if(controllerRubber)Destroy(controllerRubber);if(controllerGrain)Destroy(controllerGrain);}
    }
}
