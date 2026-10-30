using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed class CandySummoningColumn : MonoBehaviour
    {
        Transform target; Vector3 origin; MeshRenderer visual; MaterialPropertyBlock properties; float seed,columnHeight;bool follow;
        public Transform Target=>target;
        public Vector3 Origin=>origin;
        public bool Visible=>visual&&visual.enabled;
        public void Initialize(Transform player,Mesh mesh,Material material,int index,float height=150,bool followsPlayer=false)
        {
            target=player;origin=player.position;transform.position=origin;seed=index*.73f;columnHeight=height;follow=followsPlayer;
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            visual=gameObject.AddComponent<MeshRenderer>();visual.sharedMaterial=material;
            visual.shadowCastingMode=ShadowCastingMode.Off;visual.receiveShadows=false;
            visual.lightProbeUsage=LightProbeUsage.Off;visual.reflectionProbeUsage=ReflectionProbeUsage.Off;
            properties=new MaterialPropertyBlock();
        }
        public void SetVisible(bool visible){if(visual)visual.enabled=visible;}
        public void Render(float elapsed,float progress,float strength)
        {
            if(!target||!target.gameObject.activeInHierarchy){SetVisible(false);return;}
            transform.position=follow?target.position:new Vector3(target.position.x,origin.y,target.position.z);
            properties.SetFloat("_FlowTime",elapsed);properties.SetFloat("_Progress",progress);
            properties.SetFloat("_Strength",strength);properties.SetFloat("_Lift",follow?0:Mathf.Max(0,target.position.y-origin.y));properties.SetFloat("_ColumnHeight",columnHeight);
            properties.SetFloat("_Seed",seed);visual.SetPropertyBlock(properties);
        }
        public static Mesh CreateMesh(float height=150)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var info=new List<Vector2>();var triangles=new List<int>();
            const int strands=128,segments=8;const float radius=.46f;
            for(int strand=0;strand<strands;strand++)
            {
                float angle=strand*Mathf.PI*2/strands+.014f*Mathf.Sin(strand*2.3f);
                float width=.028f+.044f*(.5f+.5f*Mathf.Sin(strand*3.7f));
                for(int segment=0;segment<segments;segment++)
                {
                    float low=height*segment/segments,high=height*(segment+1)/segments;
                    Quad(vertices,uv,info,triangles,Point(angle-width,low,radius),Point(angle+width,low,radius),Point(angle+width,high,radius),Point(angle-width,high,radius),low,high,new Vector2(0,strand*.31f));
                }
            }
            for(int ring=1;ring<=2;ring++)
            for(int segment=0;segment<64;segment++)
            {
                if(segment%8==7)continue;
                float a=segment*Mathf.PI/32,b=(segment+1)*Mathf.PI/32,y=ring==1?.035f:Mathf.Min(1,height*.65f);
                float inner=ring==1?.32f:.435f,outer=ring==1?.6f:.49f;
                Quad(vertices,uv,info,triangles,Point(a,y,inner),Point(a,y,outer),Point(b,y,outer),Point(b,y,inner),a,b,new Vector2(ring,ring*.77f));
            }
            var mesh=new Mesh{name="Summoning filaments and player collar"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,info);mesh.SetTriangles(triangles,0);
            mesh.bounds=new Bounds(new Vector3(0,height*.5f,0),new Vector3(1.6f,height+4,1.6f));return mesh;
        }
        static Vector3 Point(float angle,float y,float radius)=>new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
        static void Quad(List<Vector3> vertices,List<Vector2> uv,List<Vector2> info,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float low,float high,Vector2 data)
        {
            int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            uv.Add(new Vector2(0,low));uv.Add(new Vector2(1,low));uv.Add(new Vector2(1,high));uv.Add(new Vector2(0,high));
            for(int i=0;i<4;i++)info.Add(data);
            triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
        }
    }
}
