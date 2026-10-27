using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(300)]
    public sealed class CandyPlasmaView : MonoBehaviour
    {
        readonly Dictionary<int,Transform> shots=new Dictionary<int,Transform>(),impacts=new Dictionary<int,Transform>();
        readonly Stack<Transform> shotPool=new Stack<Transform>(),impactPool=new Stack<Transform>();
        readonly HashSet<int> visible=new HashSet<int>();readonly List<int> stale=new List<int>();
        BlasterFrame last;float received;Material material;Mesh ball;MaterialPropertyBlock cloudProperties;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=host=>{if(!host.GetComponent<CandyPlasmaView>())host.AddComponent<CandyPlasmaView>();};
        void Awake(){cloudProperties=new MaterialPropertyBlock();var shader=Resources.Load<Shader>("CandyAlienPlasma");material=shader?new Material(shader):new Material(Resources.Load<Material>("CandyUtilityGlow"));ball=Ball();}
        void OnGUI()
        {
            var session=Services.Session;if(session==null||session.IsVR||session.MenuOpen||session.PlayingHand||session.Player?.gameObject.GetComponent<IEquipmentControls>()?.Equipment.drawn!=true)return;
            var previous=GUI.color;GUI.color=new Color(.3f,.95f,1);GUI.DrawTexture(new Rect(Screen.width*.5f-4,Screen.height*.5f-1,8,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(Screen.width*.5f-1,Screen.height*.5f-4,2,8),Texture2D.whiteTexture);GUI.color=previous;
        }
        void Update()
        {
            var state=MatchRuntime.Current?.Frame.blaster;
            if(!ReferenceEquals(state,last)){last=state;received=Time.time;}
            float elapsed=Mathf.Min(.25f,Time.time-received);visible.Clear();
            if(state?.enabled==true)foreach(var shot in state.shots)
            {
                visible.Add(shot.id);if(!shots.TryGetValue(shot.id,out var model)){model=shotPool.Count>0?shotPool.Pop():MakeBall();shots[shot.id]=model;model.gameObject.SetActive(true);}
                model.position=shot.position+shot.velocity*elapsed;model.rotation=Quaternion.Euler(shot.id*37,Time.time*95,Time.time*53);
            }
            Release(shots,shotPool);visible.Clear();
            if(state?.enabled==true)foreach(var impact in state.impacts)
            {
                if(impact.remaining<=elapsed)continue;visible.Add(impact.id);
                if(!impacts.TryGetValue(impact.id,out var model)){model=impactPool.Count>0?impactPool.Pop():MakeImpact();impacts[impact.id]=model;model.gameObject.SetActive(true);}
                model.position=impact.position;float life=Mathf.Clamp01((impact.remaining-elapsed)/.4f);model.localScale=Vector3.one*Mathf.Lerp(.15f,2.5f,1-life);model.rotation=Quaternion.Euler(impact.id*17,impact.id*41,Time.time*18);
                cloudProperties.SetColor("_BaseColor",new Color(.12f,.9f,1,life*.65f));for(int i=0;i<model.childCount;i++)model.GetChild(i).GetComponent<Renderer>().SetPropertyBlock(cloudProperties);
            }
            Release(impacts,impactPool);
        }
        void Release(Dictionary<int,Transform> active,Stack<Transform> pool)
        {stale.Clear();foreach(var pair in active)if(!visible.Contains(pair.Key)){pair.Value.gameObject.SetActive(false);pool.Push(pair.Value);stale.Add(pair.Key);}foreach(int id in stale)active.Remove(id);}
        Transform MakeBall()
        {
            var go=new GameObject("Visual - traveling plasma");go.transform.SetParent(transform,false);go.transform.localScale=Vector3.one*.12f;go.AddComponent<MeshFilter>().sharedMesh=ball;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        Transform MakeImpact()
        {
            var root=new GameObject("Visual - plasma impact radius").transform;root.SetParent(transform,false);
            for(int axis=0;axis<4;axis++)
            {
                var go=new GameObject("Translucent plasma cloud");go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(Mathf.Sin(axis*4)*.18f,Mathf.Cos(axis*7)*.12f,Mathf.Cos(axis*4)*.18f);go.transform.localScale=new Vector3(.65f+axis*.08f,.55f+axis*.1f,.72f);go.transform.localRotation=Quaternion.Euler(axis*35,axis*65,axis*15);go.AddComponent<MeshFilter>().sharedMesh=ball;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            return root;
        }
        static Mesh Ball()
        {
            var v=new List<Vector3>();var t=new List<int>();const int sides=16,rings=8;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++){float a=x*Mathf.PI*2/sides,b=y*Mathf.PI/rings;v.Add(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a)));}
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int a=y*(sides+1)+x;t.AddRange(new[]{a,a+1,a+sides+2,a,a+sides+2,a+sides+1});}
            var mesh=new Mesh{name="Shared plasma sphere"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy(){if(material)Destroy(material);if(ball)Destroy(ball);}
    }
}
