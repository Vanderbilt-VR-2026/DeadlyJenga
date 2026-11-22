using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        Material rainbowCurrent;
        static readonly int FlowTime=Shader.PropertyToID("_FlowTime");
        static readonly Color[] Spectrum={
            new(.98f,.13f,.25f),new(1,.41f,.10f),new(1,.79f,.14f),new(.20f,.83f,.38f),
            new(.12f,.61f,.94f),new(.28f,.29f,.93f),new(.72f,.27f,.91f)
        };

        GameObject CurrentModel(string name,ArtMesh mesh,Transform parent)
        {
            if(!rainbowCurrent)
            {
                var shader=Resources.Load<Shader>("FantasyMaps/RainbowCurrent");
                rainbowCurrent=new Material(shader){name="Rainbow water"};
            }
            var model=Model(name,mesh,parent,new[]{rainbowCurrent});
            mesh.ApplyWaterCoordinates(model.GetComponent<MeshFilter>().sharedMesh);
            return model;
        }

        void Splash(Transform parent,Vector3 foot,float width,string label)
        {
            var mesh=new ArtMesh();
            mesh.SyrupCrown(width);
            for(int i=0;i<46;i++)
            {
                float phase=Mathf.Repeat(i*.618034f,1), angle=i*2.399963f;
                float reach=width*Mathf.Lerp(.16f,.54f,Hash(new Vector3(i,2,9)));
                float size=width*Mathf.Lerp(.009f,.031f,phase*phase);
                var tint=ArtMesh.SyrupColor(angle);
                // Author around zero so the shader can align each bead with its flight.
                mesh.Sphere(Vector3.zero,new Vector3(size,size*(1.2f+phase),size),0,tint,7,10);
                mesh.WaterData(new Vector4(phase,angle,1,reach));
            }
            var splash=CurrentModel("Visual · rainbow splash "+label,mesh,parent);
            splash.transform.localPosition=foot;
            // Include the spray's flight in the mesh bounds for both eyes.
            splash.GetComponent<MeshFilter>().sharedMesh.bounds=new Bounds(Vector3.up*width*.35f,new Vector3(width*3,width*2,width*3));
        }

        sealed partial class ArtMesh
        {
            public static Color SyrupColor(float angle)
            {
                float band=(Mathf.Cos(angle)*.5f+.5f)*(Spectrum.Length-1);
                int index=Mathf.FloorToInt(band);
                return Color.Lerp(Spectrum[index],Spectrum[Mathf.Min(index+1,Spectrum.Length-1)],band-index);
            }

            // A continuous sheet sweeps out of the impact basin, turns upright and
            // curls back on itself. Vary the rim height around its circumference.
            public void SyrupCrown(float width)
            {
                const int sides=112, rows=14;
                Vector3 Surface(float a,float t)
                {
                    float lobe=.5f+.5f*Mathf.Cos(9*a+.88f*Mathf.Sin(3*a)+.62f*Mathf.Sin(2*a));
                    float tongue=lobe*lobe*lobe*(.74f+.26f*Mathf.Sin(5*a+.7f));
                    float radius=.045f+.43f*Mathf.Sin(t*1.87f)+tongue*.21f*t*t;
                    float height=.018f+.41f*Mathf.Pow(t,2.8f)*(.34f+1.42f*tongue);
                    return new Vector3(Mathf.Cos(a)*radius*1.18f,height,Mathf.Sin(a)*radius*.83f)*width;
                }
                for(int row=0;row<=rows;row++)
                {
                    float t=row/(float)rows;
                    for(int side=0;side<=sides;side++)
                    {
                        float a=side*2*Mathf.PI/sides;
                        var da=Surface(a+.003f,t)-Surface(a-.003f,t);
                        var dt=Surface(a,Mathf.Min(1,t+.003f))-Surface(a,Mathf.Max(0,t-.003f));
                        vertices.Add(Surface(a,t));
                        normals.Add(Vector3.Cross(da,dt).normalized);
                        colors.Add(SyrupColor(a));
                        WaterData(new Vector4(t,a,3,width));
                        if(row==0 || side==0)continue;
                        int d=vertices.Count-1,b=d-sides-1;
                        triangles[0].AddRange(new[]{b-1,b,d,b-1,d,d-1});
                    }
                }
            }

            List<Vector4> waterData;
            public void ApplyWaterCoordinates(Mesh mesh)=>mesh.SetUVs(0,waterData);
            public void WaterData(Vector4 data)
            {
                waterData??=new List<Vector4>();
                while(waterData.Count<vertices.Count)waterData.Add(data);
            }
            public void WaterStrip(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float along0,float along1,float u,float v,Color tint)
            {
                Quad(a,b,c,d,0,tint);
                waterData??=new List<Vector4>();
                waterData.Add(new Vector4(along0,u,0,0));waterData.Add(new Vector4(along0,v,0,0));
                waterData.Add(new Vector4(along1,v,0,0));waterData.Add(new Vector4(along1,u,0,0));
            }
        }
    }
}
