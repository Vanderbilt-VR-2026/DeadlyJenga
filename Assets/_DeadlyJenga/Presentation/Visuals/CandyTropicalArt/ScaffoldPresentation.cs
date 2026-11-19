using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CandyArt
{
    public sealed class ScaffoldPresentation:MonoBehaviour
    {
        sealed class Look{public IScaffold scaffold;public GameObject root;public Renderer grate,beacon;public Renderer[] fallback;}
        readonly List<Look> looks=new List<Look>();
        readonly Dictionary<(Vector3Int,float),Mesh> meshes=new Dictionary<(Vector3Int,float),Mesh>();
        Component map;
        Material grate,beacon;
        float scanAt,windowMaximum=20;
        Canvas hud;Text status;Image fill;
        void Awake()
        {
            grate=new Material(Resources.Load<Shader>("CandyScenery/CandyScaffoldGrate"));beacon=new Material(Resources.Load<Shader>("CandyScenery/CandyScaffoldBeacon"));
            grate.enableInstancing=beacon.enableInstancing=true;
            var go=new GameObject("Scaffold climb countdown",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);hud=go.GetComponent<Canvas>();hud.renderMode=RenderMode.WorldSpace;hud.GetComponent<RectTransform>().sizeDelta=new Vector2(500,100);go.transform.localScale=Vector3.one*.0016f;
            var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(go.transform,false);var rect=panel.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(500,100);panel.GetComponent<Image>().color=new Color(.035f,.018f,.085f,.92f);
            var label=new GameObject("Climb time",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);status=label.GetComponent<Text>();status.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");status.fontSize=25;status.alignment=TextAnchor.MiddleCenter;status.color=Color.white;status.rectTransform.sizeDelta=new Vector2(480,70);status.rectTransform.anchoredPosition=Vector2.up*8;
            var bar=new GameObject("Time remaining",typeof(RectTransform),typeof(Image));bar.transform.SetParent(go.transform,false);fill=bar.GetComponent<Image>();fill.color=new Color(.25f,1,.8f);fill.rectTransform.pivot=new Vector2(0,.5f);fill.rectTransform.anchoredPosition=new Vector2(-230,-35);fill.rectTransform.sizeDelta=new Vector2(460,8);hud.enabled=false;
        }
        void LateUpdate()
        {
            var current=Services.World as Component;
            if(current!=map||Time.time>=scanAt&&(looks.Count==0||!looks[0].root))
            {
                ClearLooks();map=current;scanAt=Time.time+1;
                if(map)foreach(var scaffold in map.GetComponentsInChildren<IScaffold>())Add(scaffold);
            }
            foreach(var look in looks)if(look.root){look.grate.enabled=look.scaffold.PlayerSolid;look.beacon.enabled=!look.scaffold.PlayerSolid;}
            var controls=Services.Session?.Demo as IBuildUpControls;var view=Services.Session?.ActiveCamera;
            hud.enabled=view&&controls!=null&&controls.Available&&(controls.Delivering||controls.ClimbSecondsRemaining>0);
            if(hud.enabled)
            {
                hud.transform.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(0,.24f,1.1f)),view.transform.rotation);
                status.text=controls.Delivering?"NEXT TIER ARRIVING":"CLIMB  ·  "+Mathf.CeilToInt(controls.ClimbSecondsRemaining)+"s  ·  TIER "+(controls.Stage+1)+" / "+controls.StageCount;
                if(controls.Delivering)windowMaximum=0;
                windowMaximum=Mathf.Max(.01f,windowMaximum,controls.ClimbSecondsRemaining);
                fill.rectTransform.sizeDelta=new Vector2(460*Mathf.Clamp01(controls.ClimbSecondsRemaining/windowMaximum),8);
            }
        }
        void Add(IScaffold scaffold)
        {
            if(!scaffold.Visual)return;var fallback=scaffold.Visual.GetComponentsInChildren<Renderer>();foreach(var r in fallback)r.enabled=false;
            var root=new GameObject("Oil-glaze scaffold art");root.transform.SetParent(scaffold.Visual,false);
            var solid=new GameObject("Hollow grate",typeof(MeshFilter),typeof(MeshRenderer));solid.transform.SetParent(root.transform,false);
            var key=(scaffold.Cells,scaffold.CellSize);
            if(!meshes.TryGetValue(key,out var mesh)){mesh=Build(scaffold.Cells,scaffold.CellSize);meshes.Add(key,mesh);}
            solid.GetComponent<MeshFilter>().sharedMesh=mesh;solid.GetComponent<Renderer>().sharedMaterial=grate;
            var ghost=new GameObject("Flowing beacon",typeof(MeshFilter),typeof(MeshRenderer));ghost.transform.SetParent(root.transform,false);ghost.GetComponent<MeshFilter>().sharedMesh=mesh;ghost.GetComponent<Renderer>().sharedMaterial=beacon;
            looks.Add(new Look{scaffold=scaffold,root=root,grate=solid.GetComponent<Renderer>(),beacon=ghost.GetComponent<Renderer>(),fallback=fallback});
        }
        static Mesh Build(Vector3Int cells,float unit)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            void Beam(Vector3 center,Vector3 size)
            {
                int axis=size.x>size.y&&size.x>size.z?0:size.y>size.z?1:2;
                var direction=Vector3.zero;direction[axis]=1;var u=axis==0?Vector3.forward:Vector3.right;var v=Vector3.Cross(direction,u);
                float radius=.5f*Mathf.Min(size[(axis+1)%3],size[(axis+2)%3]);var half=direction*size[axis]*.5f;
                for(int side=0;side<8;side++)
                {
                    float a=side*Mathf.PI*.25f,b=(side+1)*Mathf.PI*.25f;
                    var na=u*Mathf.Cos(a)+v*Mathf.Sin(a);var nb=u*Mathf.Cos(b)+v*Mathf.Sin(b);int start=vertices.Count;
                    vertices.Add(center-half+na*radius);vertices.Add(center-half+nb*radius);vertices.Add(center+half+na*radius);vertices.Add(center+half+nb*radius);
                    normals.Add(na);normals.Add(nb);normals.Add(na);normals.Add(nb);triangles.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
                    for(int end=-1;end<=1;end+=2)
                    {
                        start=vertices.Count;vertices.Add(center+half*end);vertices.Add(center+half*end+na*radius);vertices.Add(center+half*end+nb*radius);
                        normals.Add(direction*end);normals.Add(direction*end);normals.Add(direction*end);
                        triangles.AddRange(end>0?new[]{start,start+1,start+2}:new[]{start,start+2,start+1});
                    }
                }
            }
            var dimensions=(Vector3)cells*unit;float width=.18f;
            for(int a=0;a<3;a++)
            {
                int b=(a+1)%3,c=(a+2)%3;
                // Outside rails plus crossmembers at the same snapped cell spacing as the physics.
                for(int side=0;side<2;side++)for(int index=0;index<=cells[b];index++)
                {
                    var p=dimensions*.5f;p[b]=Mathf.Clamp(index*unit,width*.5f,dimensions[b]-width*.5f);p[c]=side==0?width*.5f:dimensions[c]-width*.5f;
                    var size=Vector3.one*width;size[a]=dimensions[a];Beam(p,size);
                }
            }
            var mesh=new Mesh{name="Shared snapped hollow scaffold"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        void ClearLooks(){foreach(var look in looks){if(look.root)Destroy(look.root);foreach(var r in look.fallback)if(r)r.enabled=true;}looks.Clear();}
        void OnDestroy(){ClearLooks();foreach(var mesh in meshes.Values)if(mesh)Destroy(mesh);if(grate)Destroy(grate);if(beacon)Destroy(beacon);if(hud)Destroy(hud.gameObject);}
    }
}

