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
            // The crest folds into a foamy tongue under the sheet. Droplets rise out of
            // this moving contact patch and fall back through its surface.
            for(int i=0;i<11;i++)
            {
                float a=i*2.399963f, r=Mathf.Sqrt(i/11f);
                var p=new Vector3(Mathf.Cos(a)*width*r*.47f,-width*.05f,Mathf.Sin(a)*width*r*.22f);
                mesh.Sphere(p,new Vector3(width*.19f,width*.115f,width*.14f),0,
                    Color.Lerp(Color.white,Spectrum[i%7],.21f),6,10);
                mesh.WaterData(new Vector4(i*.19f,a,3,width));
            }
            for(int i=0;i<32;i++)
            {
                float phase=Mathf.Repeat(i*.618034f,1),angle=i*2.399963f;
                float reach=width*(.27f+.51f*Hash(new Vector3(i,2,9)));
                var p=new Vector3(Mathf.Sin(i*7.13f)*width*.43f,-1,Mathf.Cos(i*3.71f)*width*.12f);
                float size=width*(.022f+.035f*phase);
                var tint=Color.Lerp(Color.white,Spectrum[i%7],.27f);
                mesh.Sphere(p,new Vector3(size,size*1.55f,size),0,tint,4,6);
                mesh.WaterData(new Vector4(phase,angle,1,reach));
            }
            for(int i=0;i<3;i++)
            {
                mesh.Ring(Vector3.zero,Quaternion.identity,width*.61f,width*.021f,32,5,0,new Color(.81f,.93f,.99f));
                mesh.WaterData(new Vector4(i/3f,0,2,0));
            }
            var splash=CurrentModel("Visual · rainbow splash "+label,mesh,parent);
            splash.transform.localPosition=foot;
            // Include the spray's flight in the mesh bounds for both eyes.
            splash.GetComponent<MeshFilter>().sharedMesh.bounds=new Bounds(Vector3.up*width*.35f,new Vector3(width*3,width*2,width*3));
        }

        sealed partial class ArtMesh
        {
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
