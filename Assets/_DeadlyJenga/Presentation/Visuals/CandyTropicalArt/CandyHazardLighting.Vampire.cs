using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyHazardLighting
    {
        Mesh shadeSphere;
        readonly Material[] shadeLayers=new Material[4];
        Light shadeLight;
        bool vampireLighting;
        Color vampireAmbientSky,vampireAmbientEquator,vampireAmbientGround;
        float vampireSunIntensity;
        Quaternion vampireSunRotation;
        void BeginVampireLighting()
        {
            if(vampireLighting)return;vampireLighting=true;
            vampireAmbientSky=RenderSettings.ambientSkyColor;vampireAmbientEquator=RenderSettings.ambientEquatorColor;vampireAmbientGround=RenderSettings.ambientGroundColor;
            if(sun){vampireSunIntensity=sun.intensity;vampireSunRotation=sun.transform.rotation;}
            if(!shadeSphere)
            {
                // Four depth-tested shells make a short, soft visibility falloff without depth textures or a render feature.
                const int rows=12,cols=24;var vertices=new Vector3[(rows+1)*(cols+1)];var triangles=new int[rows*cols*6];int k=0;
                for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
                {float a=x/(float)cols*Mathf.PI*2,b=y/(float)rows*Mathf.PI;int i=y*(cols+1)+x;vertices[i]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));if(y<rows&&x<cols){triangles[k++]=i;triangles[k++]=i+1;triangles[k++]=i+cols+1;triangles[k++]=i+1;triangles[k++]=i+cols+2;triangles[k++]=i+cols+1;}}
                shadeSphere=new Mesh{name="Vampire local darkness shells",vertices=vertices,triangles=triangles};
                var shader=Resources.Load<Shader>("CandyScenery/CandyVampireDarkness");
                for(int i=0;i<shadeLayers.Length;i++)shadeLayers[i]=new Material(shader){name="Vampire visibility layer",renderQueue=3230-i};
                var go=new GameObject("Vampire dim nearby visibility");go.transform.SetParent(transform,false);shadeLight=go.AddComponent<Light>();shadeLight.type=LightType.Point;shadeLight.range=2.1f;shadeLight.intensity=.12f;shadeLight.color=new Color(.46f,.52f,.65f);shadeLight.shadows=LightShadows.None;
            }
        }
        void DrawVampireShade(Camera view)
        {
            BeginVampireLighting();float darkness=1-exposure;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.002f,.003f,.006f),vampireAmbientSky*1.35f,exposure);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.001f,.002f,.004f),vampireAmbientEquator*1.35f,exposure);
            RenderSettings.ambientGroundColor=Color.Lerp(Color.black,vampireAmbientGround*1.35f,exposure);
            if(sun)sun.intensity=Mathf.Lerp(.015f,vampireSunIntensity*1.35f,exposure);
            if(shadeLight){shadeLight.enabled=darkness>.01f;shadeLight.transform.position=view.transform.position;shadeLight.intensity=.12f*darkness;}
            for(int i=0;i<shadeLayers.Length;i++)
            {
                float radius=i==0?.55f:i==1?.85f:i==2?1.3f:1.9f;
                float alpha=i==0?.12f:i==1?.28f:i==2?.6f:1;
                shadeLayers[i].SetFloat("_Darkness",alpha*darkness);
                Graphics.DrawMesh(shadeSphere,Matrix4x4.TRS(view.transform.position,Quaternion.identity,Vector3.one*radius),shadeLayers[i],0,view,0,null,ShadowCastingMode.Off,false);
            }
        }
        void EndVampireLighting()
        {
            if(!vampireLighting)return;vampireLighting=false;
            RenderSettings.ambientSkyColor=vampireAmbientSky;RenderSettings.ambientEquatorColor=vampireAmbientEquator;RenderSettings.ambientGroundColor=vampireAmbientGround;
            if(sun){sun.intensity=vampireSunIntensity;sun.transform.rotation=vampireSunRotation;}
            if(shadeLight)shadeLight.enabled=false;
        }
        void DestroyVampireShade(){EndVampireLighting();if(shadeSphere)Destroy(shadeSphere);foreach(var m in shadeLayers)if(m)Destroy(m);if(shadeLight)Destroy(shadeLight.gameObject);}
    }
}

