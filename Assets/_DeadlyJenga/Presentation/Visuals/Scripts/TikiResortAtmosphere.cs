using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Resort lighting is scoped to the lifetime of this map's visual prefab.
    [DefaultExecutionOrder(980)]
    public sealed class TikiResortAtmosphere : MonoBehaviour
    {
        public Light[] practicalLights;
        public Material resortSky;
        public Light sunlight;
        readonly Dictionary<Light,bool> sunStates=new Dictionary<Light,bool>();
        readonly Dictionary<CandyEnvironment,bool> environmentStates=new Dictionary<CandyEnvironment,bool>();
        readonly Dictionary<Renderer,bool> exteriorStates=new Dictionary<Renderer,bool>();
        Material previousSky;
        bool previousFog;
        AmbientMode ambientMode;
        Color sky,equator,ground;
        SphericalHarmonicsL2 previousProbe;
        bool captured;
        float nextUpdate;
        Renderer lagoon;
        MaterialPropertyBlock waterProperties;
        readonly List<Mesh> routeMeshes=new List<Mesh>(8);
        Transform routeArt;
        Vector2 shoreRadii=new Vector2(30,25);

        void OnEnable()
        {
            if(!Application.isPlaying)return;
            ambientMode=RenderSettings.ambientMode;sky=RenderSettings.ambientSkyColor;
            equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;previousProbe=RenderSettings.ambientProbe;
            previousSky=RenderSettings.skybox;previousFog=RenderSettings.fog;captured=true;
            lagoon=transform.Find("Turquoise waterfront")?.GetComponent<Renderer>();waterProperties=new MaterialPropertyBlock();
            BuildCompactArt();
            Apply();
        }
        void LateUpdate()
        {
            if(!captured||Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.3f;Apply();
        }
        void Apply()
        {
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional&&!light.transform.IsChildOf(transform))
                {if(!sunStates.ContainsKey(light))sunStates.Add(light,light.enabled);light.enabled=false;}
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.52f,.75f,.94f);
            RenderSettings.ambientEquatorColor=new Color(.46f,.59f,.59f);
            RenderSettings.ambientGroundColor=new Color(.30f,.31f,.24f);
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.52f,.59f,.61f));RenderSettings.ambientProbe=probe;
            if(CandyEnvironment.ForMap(this) is CandyEnvironment environment && environment)
            {
                if(!environmentStates.ContainsKey(environment))environmentStates.Add(environment,environment.applyLighting);environment.applyLighting=false;
                foreach(var renderer in environment.GetComponentsInChildren<Renderer>())
                {if(!exteriorStates.ContainsKey(renderer))exteriorStates.Add(renderer,renderer.enabled);renderer.enabled=false;}
            }
            RenderSettings.skybox=resortSky;RenderSettings.fog=false;
            if(sunlight)sunlight.enabled=true;
            if(lagoon)
            {
                var center=transform.position;var scale=transform.lossyScale;
                lagoon.GetPropertyBlock(waterProperties);waterProperties.SetVector("_Island",new Vector4(center.x,center.z,
                    shoreRadii.x*Mathf.Abs(scale.x),shoreRadii.y*Mathf.Abs(scale.z)));
                waterProperties.SetFloat("_WaterHeight",transform.TransformPoint(Vector3.down*1.4f).y);waterProperties.SetFloat("_SandTop",transform.TransformPoint(Vector3.down*.45f).y);
                lagoon.SetPropertyBlock(waterProperties);
            }
            // Four local accent lights on Quest, plus one shared sun.
            var camera=Services.Session?.ActiveCamera;
            Vector3 p=camera?camera.transform.position:transform.position;
            for(int i=0;i<practicalLights.Length;i++)
            {
                var light=practicalLights[i];if(!light)continue;
                int closer=0;float distance=(light.transform.position-p).sqrMagnitude;
                for(int j=0;j<practicalLights.Length;j++)if(practicalLights[j]&&j!=i)
                {float other=(practicalLights[j].transform.position-p).sqrMagnitude;if(other<distance||other==distance&&j<i)closer++;}
                light.enabled=Services.Session?.IsVR!=true||closer<4;
            }
        }
        void BuildCompactArt()
        {
            if(routeArt||!transform.parent)return;
            var collision=transform.parent.Find("Collision");
            if(!collision)return;
            var routes=collision.Find("Tiki tower routes");
            var beach=collision.Find("Contoured walkable beach")?.GetComponent<MeshCollider>();
            if(!routes||!beach||!beach.sharedMesh)return;

            transform.localScale=collision.localScale;
            var sand=transform.Find("Batched Warm fine sand")?.GetComponent<MeshFilter>();
            if(sand)sand.sharedMesh=beach.sharedMesh;
            var extents=beach.sharedMesh.bounds.extents;
            shoreRadii=new Vector2(extents.x*.96f,extents.z*.96f);

            routeArt=new GameObject("Visual · Reef lookout loop").transform;
            routeArt.SetParent(transform,false);
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var sphere=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            Material timber=Finish("Honey teak"),bamboo=Finish("Golden bamboo");
            Material cream=Finish("Weathered cream wood"),thatch=Finish("Woven palm thatch");
            Material coral=Finish("Coral painted wood"),teal=Finish("Lagoon painted wood");
            Material sandFinish=Finish("Warm fine sand"),gold=Finish("Sunshine painted wood");
            var batches=new Dictionary<Material,List<CombineInstance>>();

            void Add(Material finish,Mesh mesh,Matrix4x4 model)
            {
                if(!finish||!mesh)return;
                if(!batches.TryGetValue(finish,out var list))batches.Add(finish,list=new List<CombineInstance>());
                list.Add(new CombineInstance{mesh=mesh,transform=routeArt.worldToLocalMatrix*model});
            }
            void Detail(Material finish,Transform anchor,Vector3 at,Vector3 size,Quaternion rotation,Mesh mesh)
            {
                Add(finish,mesh,anchor.localToWorldMatrix*Matrix4x4.TRS(at,rotation,size));
            }

            foreach(var surface in routes.GetComponentsInChildren<MeshCollider>())
                Add(sandFinish,surface.sharedMesh,surface.transform.localToWorldMatrix);
            foreach(var surface in routes.GetComponentsInChildren<BoxCollider>())
            {
                var part=surface.transform;
                var size=surface.size;
                if(part.name.StartsWith("Guard"))
                {
                    foreach(float y in new[]{-.37f,.40f})
                        Detail(cream,part,new Vector3(0,y,0),new Vector3(.14f,.10f,size.z),Quaternion.identity,cube);
                    int spans=Mathf.Max(1,Mathf.CeilToInt(size.z/1.15f));
                    for(int i=0;i<=spans;i++)
                        Detail(timber,part,new Vector3(0,0,Mathf.Lerp(-size.z*.5f,size.z*.5f,i/(float)spans)),
                            new Vector3(.14f,.98f,.14f),Quaternion.identity,cube);
                    continue;
                }
                bool deck=part.name.StartsWith("Deck"),roof=part.name.StartsWith("Roof");
                Detail(deck?timber:roof?thatch:part.name.StartsWith("Stone")?sandFinish:bamboo,
                    part,surface.center,size,Quaternion.identity,cube);
                if(deck)
                {
                    var accent=part.name.Contains("Lower")?coral:teal;
                    // Lines stay inside the board edges and read from either approach.
                    foreach(int side in new[]{-1,1})
                        Detail(accent,part,new Vector3(side*(size.x*.5f-.13f),size.y*.5f+.007f,0),
                            new Vector3(.13f,.016f,size.z-.09f),Quaternion.identity,cube);
                }
            }

            foreach(Transform tower in routes)
            {
                bool signal=tower.name=="Signal lookout";
                if(!signal&&tower.name!="Surf watch")continue;
                var accent=signal?coral:teal;
                foreach(int side in new[]{-1,1})
                {
                    Detail(accent,tower,new Vector3(side*2.22f,2.89f,0),new Vector3(.28f,.32f,4.92f),
                        Quaternion.identity,cube);
                    Detail(cream,tower,new Vector3(0,2.91f,side*2.37f),new Vector3(4.95f,.23f,.18f),
                        Quaternion.identity,cube);
                    var lamp=new Vector3(side*1.82f,2.56f,-2.10f);
                    Detail(gold,tower,lamp,new Vector3(.28f,.35f,.28f),Quaternion.identity,sphere);
                    Detail(timber,tower,lamp+Vector3.up*.21f,new Vector3(.34f,.10f,.34f),Quaternion.identity,cube);
                }
                Detail(timber,tower,new Vector3(0,3.77f,.29f),new Vector3(.12f,1.30f,.12f),
                    Quaternion.identity,cube);
                Detail(accent,tower,new Vector3(.40f,4.22f,.29f),new Vector3(.80f,.37f,.06f),
                    Quaternion.Euler(0,-10,7),cube);
                Detail(cream,tower,new Vector3(.68f,4.24f,.29f),new Vector3(.20f,.35f,.07f),
                    Quaternion.Euler(0,-10,7),cube);
                if(signal)
                {
                    Detail(coral,tower,new Vector3(0,3.58f,.29f),new Vector3(.88f,.76f,.83f),Quaternion.identity,cube);
                    Detail(gold,tower,new Vector3(0,4.47f,.29f),new Vector3(.33f,.38f,.33f),Quaternion.identity,sphere);
                }
                else
                {
                    Detail(teal,tower,new Vector3(-1.82f,1.43f,2.36f),new Vector3(.36f,2.07f,.15f),
                        Quaternion.Euler(0,0,-13),sphere);
                    Detail(cream,tower,new Vector3(-1.41f,1.25f,2.36f),new Vector3(.31f,1.82f,.15f),
                        Quaternion.Euler(0,0,-13),sphere);
                }

                var sign=new GameObject(tower.name).transform;
                sign.SetParent(routeArt,false);
                sign.SetPositionAndRotation(tower.TransformPoint(new Vector3(0,2.59f,-2.51f)),tower.rotation);
                var title=sign.gameObject.AddComponent<TextMesh>();
                title.text=signal?"SIGNAL LOOKOUT":"SURF WATCH";
                title.anchor=TextAnchor.MiddleCenter;title.alignment=TextAlignment.Center;
                title.fontSize=52;title.characterSize=.035f;title.color=new Color(.98f,.9f,.69f);
            }

            foreach(var batch in batches)
            {
                var combined=new Mesh{name="Tiki route · "+batch.Key.name};
                combined.CombineMeshes(batch.Value.ToArray(),true,true);
                routeMeshes.Add(combined);
                var panel=new GameObject(combined.name,typeof(MeshFilter),typeof(MeshRenderer));
                panel.transform.SetParent(routeArt,false);
                panel.GetComponent<MeshFilter>().sharedMesh=combined;
                panel.GetComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
        }

        Material Finish(string label)=>transform.Find("Batched "+label)?.GetComponent<Renderer>()?.sharedMaterial;
        void OnDestroy(){foreach(var mesh in routeMeshes)if(mesh)Destroy(mesh);}

        void OnDisable()
        {
            if(!captured)return;
            foreach(var pair in sunStates)if(pair.Key)pair.Key.enabled=pair.Value;sunStates.Clear();
            foreach(var pair in environmentStates)if(pair.Key)pair.Key.applyLighting=pair.Value;environmentStates.Clear();
            foreach(var pair in exteriorStates)if(pair.Key)pair.Key.enabled=pair.Value;exteriorStates.Clear();
            RenderSettings.skybox=previousSky;RenderSettings.fog=previousFog;
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=sky;
            RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.ambientProbe=previousProbe;captured=false;
        }
    }
}
