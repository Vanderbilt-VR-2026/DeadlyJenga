using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DeadlyJenga.CandyArt
{
    // Optional lobby dressing. No colliders, network state or gameplay dependencies.
    public sealed partial class CandyLobbyResort : MonoBehaviour
    {
        public Material candyMaterial;
        public Transform milkshakeGeometry;
        [Range(.3f,1)] public float gloss=.82f;
        public Color cream=new Color(1,.94f,.82f), coral=new Color(1,.23f,.39f), mint=new Color(.12f,.93f,.68f);
        public Color lavender=new Color(.66f,.38f,1), blue=new Color(.05f,.62f,1), gold=new Color(1,.72f,.18f);
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        readonly Dictionary<int,Batch> batches=new Dictionary<int,Batch>();
        Transform visual;
        Color[] palette;
        sealed class Batch
        {
            public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            public readonly List<int> triangles=new List<int>();
        }
        void Start()=>Build();
        public void Build()
        {
            if(visual)return;
            palette=new[]{cream,coral,mint,lavender,blue,gold,new Color(.17f,.09f,.28f),new Color(.12f,.65f,.35f),new Color(.68f,.36f,.17f),new Color(.4f,.16f,.065f),new Color(.65f,.9f,1,.055f)};
            visual=new GameObject("Visual · Candy island resort").transform;visual.SetParent(transform,false);
            Plaza();Gateway();Palms();BeachClub();Waterpark();StationStands();Milkshake();
            foreach(var entry in batches)
            {
                var b=entry.Value;var mesh=new Mesh{name="Resort static batch "+entry.Key,indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(b.vertices);mesh.SetNormals(b.normals);mesh.SetTriangles(b.triangles,0);mesh.RecalculateBounds();meshes.Add(mesh);
                var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(visual,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var material=candyMaterial?new Material(candyMaterial):new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name="Resort candy "+entry.Key;material.SetColor("_BaseColor",palette[entry.Key]);material.SetFloat("_Smoothness",gloss);material.SetFloat("_Metallic",.06f);material.enableInstancing=true;
                if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",null);
                if(entry.Key==9){material.SetFloat("_Smoothness",.38f);material.SetFloat("_Metallic",0);}
                if(entry.Key==10){material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;}
                materials.Add(material);go.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            batches.Clear();
        }
        void Plaza()
        {
            // Thin inlays follow the existing walkable sand rather than creating a raised floor.
            Ring(new Vector3(0,.463f,-2.6f),1.12f,.07f,2);
            Ring(new Vector3(0,.464f,-2.6f),.95f,.025f,0);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;var p=new Vector3(Mathf.Cos(a)*.76f,.465f,-2.6f+Mathf.Sin(a)*.76f);
                Ellipsoid(p,new Vector3(.09f,.012f,.09f),i%2==0?3:5);
            }
            for(int lane=0;lane<3;lane++)
            {
                float target=(lane-1)*3.05f;
                for(int i=0;i<7;i++)
                {
                    float t=(i+1)/8f;var p=new Vector3(Mathf.SmoothStep(0,target,t),.463f,Mathf.Lerp(-1.8f,lane==1?.8f:-.6f,t));
                    Rounded(p,new Vector3(.44f,.018f,.25f),lane==0?3:lane==1?2:1,Quaternion.Euler(0,Mathf.Lerp(0,(lane-1)*-25,t),0));
                }
            }
            for(int i=0;i<13;i++)
            {
                float a=(i/12f)*Mathf.PI;
                Rounded(new Vector3(Mathf.Cos(a)*4.7f,.46f,-2.4f-Mathf.Sin(a)*2),new Vector3(.3f,.025f,.17f),i%3==0?5:0,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
            }
        }
        void Gateway()
        {
            var path=new List<Vector3>{new Vector3(-4.9f,.4f,3.4f),new Vector3(-4.9f,3.3f,3.4f)};
            for(int i=0;i<=28;i++)
            {float a=Mathf.PI-i*Mathf.PI/28;path.Add(new Vector3(Mathf.Cos(a)*4.9f,3.3f+Mathf.Sin(a)*1.25f,3.4f));}
            path.Add(new Vector3(4.9f,.4f,3.4f));Tube(path,.18f,0,12);
            for(int side=-1;side<=1;side+=2)
            {
                Rounded(new Vector3(side*4.9f,.56f,3.4f),new Vector3(.85f,.33f,.85f),3);
                for(int n=0;n<5;n++)Ellipsoid(new Vector3(side*4.9f,1+n*.42f,3.4f),new Vector3(.21f,.065f,.21f),n%2==0?2:1);
            }
            Rounded(new Vector3(0,3.8f,3.35f),new Vector3(5.3f,.95f,.22f),6);
            Rounded(new Vector3(0,3.8f,3.48f),new Vector3(5.48f,1.12f,.22f),3);
            Sign("DEADLY JENGA",new Vector3(0,3.91f,3.21f),new Vector2(4.8f,.55f),68,0);
            Sign("PARADISE.  UNTIL IT ISN'T.",new Vector3(0,3.51f,3.21f),new Vector2(4.6f,.22f),25,2);
            // Tiny stacked-block skyline on the arch makes the destination recognizable.
            for(int tower=0;tower<3;tower++)for(int row=0;row<3+(tower==1?2:0);row++)
                Rounded(new Vector3((tower-1)*.75f,4.48f+row*.2f,3.4f),new Vector3(.57f,.19f,.28f),tower==1?3:tower==0?1:2,Quaternion.Euler(0,row%2*90,0));
            for(int i=0;i<19;i++)
            {
                float x=Mathf.Lerp(-5.5f,5.5f,i/18f),y=2.8f+.55f*Mathf.Pow(x/5.5f,2);
                Ellipsoid(new Vector3(x,y,4.25f),Vector3.one*.065f,i%3+1);
                if(i>0)Tube(new[]{new Vector3(x-11f/18,y-.03f,4.25f),new Vector3(x,y,4.25f)},.012f,0,5);
            }
        }
        void StationStands()
        {
            for(int i=0;i<3;i++)
            {
                var p=new Vector3((i-1)*3.05f,.52f,i==1?1.8f:1);
                Rounded(p,new Vector3(i==0?1.6f:1.45f,.1f,.8f),i==0?3:i==1?2:1);
                Rounded(p+Vector3.up*.17f,new Vector3(.18f,.4f,.22f),0);
                Ellipsoid(p+Vector3.up*.17f,new Vector3(.19f,.08f,.19f),i==0?3:i==1?2:1);
            }
            // Legs give the giant keyboard a deliberately absurd console silhouette.
            foreach(float x in new[]{-4.45f,-1.55f})Rounded(new Vector3(x,.5f,-.05f),new Vector3(.2f,.45f,.32f),3);
        }
        void Palms()
        {
            Palm(new Vector3(-5.85f,.2f,1.8f),3.7f,-.35f);Palm(new Vector3(5.85f,.2f,2.1f),4.1f,.3f);
            Palm(new Vector3(-4.8f,.25f,5.2f),3.3f,.3f);Palm(new Vector3(4.9f,.25f,5.1f),3.6f,-.25f);
            Palm(new Vector3(6.1f,.17f,.5f),2.8f,.3f);
        }
        void Palm(Vector3 root,float height,float lean)
        {
            var trunk=new List<Vector3>();
            for(int i=0;i<=12;i++){float t=i/12f;trunk.Add(root+new Vector3(lean*t*t,height*t,.2f*t*t));}
            Tube(trunk,.13f,8,9);
            for(int i=0;i<7;i++){float t=i/8f;Ellipsoid(root+new Vector3(lean*t*t,height*t,.2f*t*t),new Vector3(.145f,.04f,.145f),5);}
            Vector3 crown=trunk[trunk.Count-1];
            for(int leaf=0;leaf<7;leaf++)
            {
                float a=leaf*Mathf.PI*2/7;Vector3 dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),side=Vector3.Cross(Vector3.up,dir);
                var b=BatchFor(leaf%2==0?2:7);int start=b.vertices.Count;
                for(int i=0;i<=12;i++)
                {
                    float t=i/12f;var p=crown+dir*(1.65f*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.56f-.45f*t*t);
                    float width=Mathf.Max(.005f,Mathf.Sin(t*Mathf.PI)*.3f),depth=Mathf.Max(.004f,width*.3f);
                    var tangent=(dir*1.65f+Vector3.up*(Mathf.Cos(t*Mathf.PI)*Mathf.PI*.56f-.9f*t)).normalized;
                    var up=Vector3.Cross(tangent,side).normalized;
                    for(int s=0;s<=8;s++)
                    {float angle=s*Mathf.PI/4;var normal=(side*(Mathf.Cos(angle)/width)+up*(Mathf.Sin(angle)/depth)).normalized;b.vertices.Add(p+side*(Mathf.Cos(angle)*width)+up*(Mathf.Sin(angle)*depth));b.normals.Add(normal);}
                }
                for(int i=0;i<12;i++)for(int j=0;j<8;j++){int n=start+i*9+j;Quad(b,n,n+1,n+9,n+10);}
            }
            for(int n=0;n<3;n++){float a=n*Mathf.PI*2/3;Ellipsoid(crown+new Vector3(Mathf.Cos(a)*.18f,-.15f,Mathf.Sin(a)*.18f),Vector3.one*.18f,5);}
            for(int n=0;n<4;n++){float a=n*Mathf.PI/2;Ellipsoid(root+new Vector3(Mathf.Cos(a)*.26f,0,Mathf.Sin(a)*.26f),new Vector3(.28f,.12f,.24f),0);}
        }
        void BeachClub()
        {
            Umbrella(new Vector3(-5.15f,.35f,-3.2f),.85f,1);
            foreach(float side in new[]{-1f})
            {
                Vector3 p=new Vector3(side*5.1f,.65f,-1.7f);
                for(int i=0;i<2;i++)
                {
                    Vector3 seat=p+Vector3.forward*(i*.63f);Rounded(seat,new Vector3(1.1f,.23f,.45f),i==0?3:2,Quaternion.Euler(0,side*15,0));
                    Rounded(seat+new Vector3(side*.4f,.25f,0),new Vector3(.22f,.45f,.45f),0,Quaternion.Euler(0,0,-side*15));
                }
            }
            // Bright life rings sit by the shore like oversized pool toys.
            Ring(new Vector3(-5.8f,.18f,-.5f),.46f,.13f,1);Ring(new Vector3(6,.08f,0),.5f,.14f,2);
            Ellipsoid(new Vector3(4.9f,.63f,-3.6f),Vector3.one*.3f,5);
            Ring(new Vector3(4.9f,.63f,-3.6f),.3f,.025f,0,Quaternion.Euler(90,0,0));
            Sign("MEET.  BUILD.  SURVIVE.",new Vector3(0,.54f,-4.5f),new Vector2(2.8f,.35f),30,3,Quaternion.Euler(70,0,0));
        }
        void Umbrella(Vector3 p,float size,int color,float canopyDrop=.4f)
        {
            Tube(new[]{p,p+Vector3.up*1.85f*size},.035f,0,8);
            for(int wedge=0;wedge<12;wedge++)
            {
                var b=BatchFor(wedge%2==0?color:0);int start=b.vertices.Count;
                for(int r=0;r<=5;r++)for(int a=0;a<=3;a++)
                {
                    float t=r/5f,angle=(wedge+a/3f)*Mathf.PI/6;var v=p+new Vector3(Mathf.Cos(angle)*t*size,(1.85f-canopyDrop*t*t)*size,Mathf.Sin(angle)*t*size);
                    b.vertices.Add(v);b.normals.Add(new Vector3(Mathf.Cos(angle)*t*.4f,1,Mathf.Sin(angle)*t*.4f).normalized);
                }
                for(int r=0;r<5;r++)for(int a=0;a<3;a++){int n=start+r*4+a;Quad(b,n,n+1,n+4,n+5,true);}
            }
            Ellipsoid(p+Vector3.up*1.88f*size,Vector3.one*.09f,5);
        }
        void Waterpark()
        {
            // Decorative looping flumes frame the hub; they stay outside the terminal sight lines.
            for(int side=-1;side<=1;side+=2)
            {
                var path=new List<Vector3>();
                for(int i=0;i<=44;i++)
                {float t=i/44f,a=t*Mathf.PI*2;path.Add(new Vector3(side*(4.4f+Mathf.Cos(a)*1.05f),.55f+Mathf.Sin(t*Mathf.PI)*2.1f,5.4f+Mathf.Sin(a)*.8f));}
                Tube(path,.22f,side<0?1:4,12);
                for(int n=0;n<3;n++)Tube(new[]{new Vector3(side*4.4f,.1f,4.8f+n*.5f),new Vector3(side*4.4f,1.8f,4.8f+n*.5f)},.055f,0,7);
            }
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5;var p=new Vector3(Mathf.Cos(a)*.72f,.53f,5.2f+Mathf.Sin(a)*.72f);
                Rounded(p,new Vector3(.5f,.22f,.3f),i%3+1,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
            }
            for(int row=0;row<7;row++)for(int side=-1;side<=1;side+=2)
                Rounded(new Vector3(side*(row%2==0?.23f:.33f),.65f+row*.22f,5.2f),new Vector3(.62f,.21f,.29f),row%3+1,Quaternion.Euler(0,row%2*90,0));
        }
        void Sign(string text,Vector3 position,Vector2 size,int fontSize,int color,Quaternion? rotation=null)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Canvas));go.transform.SetParent(visual,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=Vector3.one*.005f;
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var label=new GameObject("Lettering",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);var t=label.GetComponent<Text>();
            t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=fontSize;t.fontStyle=FontStyle.Bold;t.color=palette[color];t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=12;t.resizeTextMaxSize=fontSize;t.rectTransform.sizeDelta=size/.005f;
        }
        Batch BatchFor(int color)
        {if(!batches.TryGetValue(color,out var b)){b=new Batch();batches.Add(color,b);}return b;}
        static void Quad(Batch b,int a,int c,int d,int e,bool twoSided=false)
        {b.triangles.AddRange(new[]{a,c,d,d,c,e});if(twoSided)b.triangles.AddRange(new[]{d,c,a,e,c,d});}
        void Rounded(Vector3 center,Vector3 size,int color,Quaternion? rotation=null)
        {
            var b=BatchFor(color);var q=rotation??Quaternion.identity;float[] grid={-.5f,-.4f,.4f,.5f};
            void Face(Vector3 normal,Vector3 u,Vector3 v)
            {
                int start=b.vertices.Count;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {var p=normal*.5f+u*grid[x]+v*grid[y];var inner=new Vector3(Mathf.Clamp(p.x,-.4f,.4f),Mathf.Clamp(p.y,-.4f,.4f),Mathf.Clamp(p.z,-.4f,.4f));var n=(p-inner).normalized;b.vertices.Add(center+q*Vector3.Scale(inner+n*.1f,size));b.normals.Add(q*new Vector3(n.x/size.x,n.y/size.y,n.z/size.z).normalized);}
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int n=start+y*4+x;Quad(b,n,n+1,n+4,n+5);}
            }
            Face(Vector3.forward,Vector3.right,Vector3.up);Face(Vector3.back,Vector3.left,Vector3.up);Face(Vector3.up,Vector3.right,Vector3.back);Face(Vector3.down,Vector3.right,Vector3.forward);Face(Vector3.right,Vector3.back,Vector3.up);Face(Vector3.left,Vector3.forward,Vector3.up);
        }
        void Ellipsoid(Vector3 p,Vector3 size,int color)
        {
            var b=BatchFor(color);int start=b.vertices.Count;
            for(int y=0;y<=8;y++)for(int x=0;x<=12;x++)
            {float a=x*Mathf.PI/6,t=y*Mathf.PI/8;var n=new Vector3(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a));b.vertices.Add(p+Vector3.Scale(n,size));b.normals.Add(new Vector3(n.x/size.x,n.y/size.y,n.z/size.z).normalized);}
            for(int y=0;y<8;y++)for(int x=0;x<12;x++){int n=start+y*13+x;Quad(b,n,n+1,n+13,n+14);}
        }
        void Ring(Vector3 center,float radius,float thickness,int color,Quaternion? rotation=null)
        {
            var points=new List<Vector3>();var q=rotation??Quaternion.identity;
            for(int i=0;i<=48;i++){float a=i*Mathf.PI/24;points.Add(center+q*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            Tube(points,thickness,color,8);
        }
        void Tube(IList<Vector3> points,float radius,int color,int sides)
        {
            var b=BatchFor(color);int start=b.vertices.Count;
            for(int i=0;i<points.Count;i++)
            {
                var dir=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;var u=Vector3.Cross(dir,Mathf.Abs(dir.y)>.95f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(dir,u);
                for(int j=0;j<=sides;j++){float a=j*Mathf.PI*2/sides;var n=u*Mathf.Cos(a)+v*Mathf.Sin(a);b.vertices.Add(points[i]+n*radius);b.normals.Add(n);}
            }
            for(int i=0;i<points.Count-1;i++)for(int j=0;j<sides;j++){int n=start+i*(sides+1)+j;Quad(b,n,n+1,n+sides+1,n+sides+2);}
        }
        void OnDestroy()
        {foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var material in materials)if(material)Destroy(material);if(visual)Destroy(visual.gameObject);}
    }
}
