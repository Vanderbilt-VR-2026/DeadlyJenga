using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt.Marina
{
    // Glowing chevrons on the floor from the arrival dock to the play screen.
    // Fades out near the screen, and stops showing once a player has started a few matches.
    public sealed class MarinaGuidePath : MonoBehaviour
    {
        const string RunsKey="DeadlyJenga.GuidePathRuns";
        const int ShowForRuns=3;
        // Lobby-local route, checked to stay on walkable boardwalk.
        // Through the gap between the arrival chairs, up the middle of the boardwalk, along the front steps.
        static readonly Vector3[] Route={new(-9.9f,0,-7.2f),new(-10,0,-5.6f),new(-10,0,-3.4f),new(-9.3f,0,-2.2f),new(-8.7f,0,-.4f),
            new(-7.6f,0,2.2f),new(-4,0,3.3f),new(0,0,3.6f),new(0,0,8.6f)};
        Transform lobbyRoot;Vector3 builtAt=new(float.NaN,0,0);
        Mesh mesh;Material material;MeshRenderer meshRenderer;float fade=1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen(){GameEvents.DepartureStarted-=Counted;GameEvents.DepartureStarted+=Counted;}
        static void Counted(float seconds){PlayerPrefs.SetInt(RunsKey,PlayerPrefs.GetInt(RunsKey,0)+1);PlayerPrefs.Save();}
        public static bool Wanted=>PlayerPrefs.GetInt(RunsKey,0)<ShowForRuns;

        public void Build(Transform root)
        {
            lobbyRoot=root;
            var shader=Shader.Find("DeadlyJenga/Guide Path");if(!shader){enabled=false;return;}
            material=new Material(shader){name="Guide path",renderQueue=3060};
            material.SetColor("_Color",new Color(0,.55f,.45f,1));
            var go=new GameObject("Guide path to play screen",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(0,.04f,0);
            mesh=new Mesh{name="Guide path chevrons"};go.GetComponent<MeshFilter>().sharedMesh=mesh;
            meshRenderer=go.GetComponent<MeshRenderer>();meshRenderer.sharedMaterial=material;
            meshRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;meshRenderer.receiveShadows=false;
        }
        // The lobby is moved into place after it builds, so the floor is sampled once it settles.
        void Rebuild()
        {
            builtAt=lobbyRoot.position;
            var vertices=new System.Collections.Generic.List<Vector3>();var uvs=new System.Collections.Generic.List<Vector2>();
            var colors=new System.Collections.Generic.List<Color>();var triangles=new System.Collections.Generic.List<int>();
            float travelled=0;const float step=.9f;float next=.6f;
            int mask=~((1<<Layers.Survivor)|(1<<2));
            for(int s=0;s<Route.Length-1;s++)
            {
                var a=Route[s];var b=Route[s+1];float length=Vector3.Distance(a,b);var forward=(b-a).normalized;
                while(next<=travelled+length)
                {
                    var p=a+forward*(next-travelled);
                    var world=lobbyRoot.TransformPoint(p+Vector3.up*3.4f);
                    float y=Physics.Raycast(world,Vector3.down,out var hit,6,mask,QueryTriggerInteraction.Ignore)?lobbyRoot.InverseTransformPoint(hit.point).y:1.35f;
                    Chevron(new Vector3(p.x,y+.025f,p.z),forward,next,next/Mathf.Max(1,TotalLength()),vertices,uvs,colors,triangles);
                    next+=step;
                }
                travelled+=length;
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        static float TotalLength(){float t=0;for(int i=0;i<Route.Length-1;i++)t+=Vector3.Distance(Route[i],Route[i+1]);return t;}
        // A flat "V" pointing along the path: two short slanted bars.
        static void Chevron(Vector3 c,Vector3 forward,float distance,float progress,System.Collections.Generic.List<Vector3> v,System.Collections.Generic.List<Vector2> uv,System.Collections.Generic.List<Color> col,System.Collections.Generic.List<int> tri)
        {
            var side=Vector3.Cross(Vector3.up,forward);
            const float half=.34f,thick=.1f,depth=.2f;
            for(int arm=-1;arm<=1;arm+=2)
            {
                var outer=c+side*(half*arm)-forward*depth;var inner=c+forward*depth*.35f;
                var along=(inner-outer).normalized;var normal=Vector3.Cross(Vector3.up,along)*thick;
                int start=v.Count;
                v.Add(outer-normal);v.Add(outer+normal);v.Add(inner+normal);v.Add(inner-normal);
                for(int k=0;k<4;k++){uv.Add(new Vector2(distance,k<2?0:1));{float glow=Mathf.Lerp(.6f,1,progress);col.Add(new Color(glow,glow,glow,1));}}
                tri.Add(start);tri.Add(start+1);tri.Add(start+2);tri.Add(start);tri.Add(start+2);tri.Add(start+3);
            }
        }
        void LateUpdate()
        {
            if(!meshRenderer)return;
            if(lobbyRoot.position!=builtAt)Rebuild();
            var session=Services.Session;var player=session?.Player;
            float target=0;
            if(Wanted&&NetworkActors.Alive(player))
            {
                var local=lobbyRoot.InverseTransformPoint(player.transform.position);
                float toScreen=new Vector2(local.x-MayhemMarina.PlayScreen.x,local.z-MayhemMarina.PlayScreen.z).magnitude;
                target=toScreen<4.5f?0:1;
            }
            fade=Mathf.MoveTowards(fade,target,Time.unscaledDeltaTime*1.5f);
            meshRenderer.enabled=fade>.01f;
            if(meshRenderer.enabled)material.SetFloat("_Fade",fade);
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
