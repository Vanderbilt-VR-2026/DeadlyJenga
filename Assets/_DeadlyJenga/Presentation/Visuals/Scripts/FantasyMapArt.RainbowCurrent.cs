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

        void BuildCloudRiver(Vector3 center,Vector3 inlet,float inletWidth)
        {
            // Thread the low channel between the west screens and the rainworks. Its outer
            // bend returns behind the apartments and widens over the cumulus skirt.
            Vector3 Street(float x,float z)=>new(center.x+x,streetY+.40f,center.z+z);
            var outward=new Vector3(-.95106f,0,-.30902f).normalized;
            var lip=Street(outward.x*halfWidth*1.21f,outward.z*halfDepth*1.21f);
            lip.y-=1.2f;
            var points=new[]{inlet,inlet+Vector3.back*11,Street(-41,56),Street(-38,46),
                Street(-33,40),Street(-37,35),Street(-44,28),Street(-45,17),
                Street(-41.15f,9),Street(-41.1f,2),Street(-41.1f,-6),Street(-41.5f,-12),
                Street(-53,-14),Street(-69,-12),Street(-86,-17),Street(-100,-24),
                lip-outward*10,lip};
            var water=new ArtMesh();var banks=new ArtMesh();
            Vector3 previous=Vector3.zero,previousSide=Vector3.zero;
            float previousWidth=0,distance=0;
            for(int i=0;i<points.Length-1;i++)
            {
                var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];
                for(int step=i==0?0:1;step<=8;step++)
                {
                    float t=step/8f;
                    var linear=c-a;var quadratic=2*a-5*b+4*c-d;var cubic=-a+3*b-3*c+d;
                    var pos=b+.5f*t*(linear+t*(quadratic+t*cubic));
                    var tangent=linear+t*(2*quadratic+3*t*cubic);
                    var right=Vector3.Cross(Vector3.up,tangent).normalized;
                    float progress=i+t;
                    float width=Mathf.Lerp(inletWidth,4.2f,Mathf.SmoothStep(0,1,progress/3));
                    float narrows=Mathf.SmoothStep(0,1,progress-7)*(1-Mathf.SmoothStep(0,1,progress-11));
                    width=Mathf.Lerp(width,2.6f,narrows);
                    width=Mathf.Lerp(width,17,Mathf.SmoothStep(0,1,(progress-(points.Length-5))/4));
                    var local=L(pos);var side=transform.InverseTransformVector(right);
                    if(i!=0 || step!=0)
                    {
                        float next=distance+Vector3.Distance(previous,local);
                        for(int k=0;k<Spectrum.Length;k++)
                        {
                            float u=k/7f,v=(k+1)/7f;
                            water.WaterStrip(previous+previousSide*((u-.5f)*previousWidth),previous+previousSide*((v-.5f)*previousWidth),
                                local+side*((v-.5f)*width),local+side*((u-.5f)*width),distance*.16f,next*.16f,u,v,Spectrum[k],-1);
                        }
                        banks.ChannelSection(previous,previousSide,previousWidth,local,side,width);
                        distance=next;
                    }
                    previous=local;previousSide=side;previousWidth=width;
                }
            }
            Model("Visual · river channel",banks,transform,new[]{cloudMaterial});
            CurrentModel("Visual · city rainbow river",water,transform);
            var outlet=new GameObject("Visual · rim spillway");outlet.transform.SetParent(transform,false);
            outlet.transform.localPosition=L(lip);outlet.transform.localRotation=LR(Quaternion.LookRotation(outward));
            ownedObjects.Add(outlet);
            var impact=RainbowFall(outlet.transform,Vector3.zero,17,42,58,"rim");
            var bowl=new ArtMesh();var pool=new ArtMesh();
            bowl.CloudBowl();pool.CloudPool();
            Model("Visual · lower cloud bowl",bowl,transform,new[]{cloudMaterial}).transform.localPosition=L(impact);
            CurrentModel("Visual · lower cloud pool",pool,transform).transform.localPosition=L(impact);
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
            public void ChannelSection(Vector3 a,Vector3 rightA,float widthA,Vector3 b,Vector3 rightB,float widthB)
            {
                var down=Vector3.down*.72f;
                var l=a-rightA*(widthA*.5f+.32f);var r=a+rightA*(widthA*.5f+.32f);
                var nl=b-rightB*(widthB*.5f+.32f);var nr=b+rightB*(widthB*.5f+.32f);
                Quad(l+down,r+down,nr+down,nl+down,0,Sugar);
                for(int sign=-1;sign<=1;sign+=2)
                {
                    var p=a+rightA*(sign*(widthA*.5f-.08f));var q=b+rightB*(sign*(widthB*.5f-.08f));
                    var edgeA=p+rightA*(sign*.40f)+Vector3.up*.09f;
                    var edgeB=q+rightB*(sign*.40f)+Vector3.up*.09f;
                    if(sign<0){Quad(p,edgeA,edgeB,q,0,Sugar);Quad(edgeA,edgeA+down,edgeB+down,edgeB,0,Sugar);}
                    else {Quad(q,edgeB,edgeA,p,0,Sugar);Quad(edgeB,edgeB+down,edgeA+down,edgeA,0,Sugar);}
                }
            }

            public void CloudBowl()
            {
                // One closed shell from the belly, over the scalloped rim and down into the pool.
                var profile=new[]{new Vector2(0,-16),new Vector2(.48f,-14),new Vector2(.85f,-8),
                    new Vector2(1,-1),new Vector2(.99f,2.6f),new Vector2(.93f,4.1f),
                    new Vector2(.86f,3.3f),new Vector2(.79f,-.8f),new Vector2(.62f,-4),new Vector2(0,-6)};
                const int sectors=96;
                for(int j=0;j<profile.Length;j++)
                for(int i=0;i<sectors;i++)
                {
                    float a=i*2*Mathf.PI/sectors,r=profile[j].x;
                    float scallop=1+.027f*Mathf.Cos(11*a)+.017f*Mathf.Sin(7*a);
                    vertices.Add(new Vector3(43*r*scallop*Mathf.Cos(a),profile[j].y+r*r*(.8f*Mathf.Cos(11*a)+.45f*Mathf.Sin(7*a)),35*r*scallop*Mathf.Sin(a)));
                    colors.Add(Color.white);
                    if(j==0)continue;
                    int prev=(j-1)*sectors+i,next=(j-1)*sectors+(i+1)%sectors;
                    if(j>1)triangles[0].AddRange(new[]{prev,prev+sectors,next});
                    if(j<profile.Length-1)triangles[0].AddRange(new[]{next,prev+sectors,next+sectors});
                }
                // Average face normals across the scallops, including the angular seam.
                for(int n=0;n<vertices.Count;n++)normals.Add(Vector3.zero);
                var indices=triangles[0];
                for(int i=0;i<indices.Count;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];
                    var n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                    normals[a]+=n;normals[b]+=n;normals[c]+=n;
                }
                for(int n=0;n<normals.Count;n++)normals[n]=normals[n].normalized;
                // Low overlapping lobes give the outside a cloud silhouette below the waterline.
                for(int i=0;i<22;i++)
                {
                    float a=i*2*Mathf.PI/22,r=.91f+.025f*Mathf.Sin(5*a);
                    var bulb=new Vector3(43*r*Mathf.Cos(a),-4+1.4f*Mathf.Sin(3*a),35*r*Mathf.Sin(a));
                    Sphere(bulb,new Vector3(7.9f,5.2f+.9f*Mathf.Cos(4*a),7.8f),0,Color.white,8,12);
                }
            }

            public void CloudPool()
            {
                // The edge penetrates the inner wall below the rim, closing the pool all around.
                const int sectors=96;
                for(int i=0;i<sectors;i++)
                {
                    Vector3 Edge(int k){float a=k*2*Mathf.PI/sectors;return new Vector3(38*Mathf.Cos(a),0,31*Mathf.Sin(a));}
                    Tri(Vector3.zero,Edge(i+1),Edge(i),Vector3.up,0,new Color(.75f,.88f,.98f));
                    WaterData(new Vector4(0,0,-2,0));
                }
            }

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
            public void WaterStrip(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float along0,float along1,float u,float v,Color tint,float mode=0)
            {
                Quad(a,b,c,d,0,tint);
                waterData??=new List<Vector4>();
                waterData.Add(new Vector4(along0,u,mode,0));waterData.Add(new Vector4(along0,v,mode,0));
                waterData.Add(new Vector4(along1,v,mode,0));waterData.Add(new Vector4(along1,u,mode,0));
            }
        }
    }
}
