using System.Collections;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.CandyArt.Marina
{
    public sealed partial class MayhemMarina : MonoBehaviour
    {
        public static readonly Vector3 Arrival=new(-10,1.36f,-8);
        public static readonly Vector3 Wardrobe=new(-13,1.55f,8.2f);
        public static readonly Vector3 PlayScreen=new(0,3.85f,11.1f);
        public bool Built {get;private set;}
        public int RenderTriangles=>mesh?.RenderTriangles??0;
        public int CollisionTriangles=>mesh?.CollisionTriangles??0;
        public Mesh WalkableMesh=>mesh?.CollisionMesh;
        MarinaMesh mesh;
        Transform visual;
        ILobbyControls controls;
        readonly List<Material> extraMaterials=new();
        readonly List<Mesh> extraMeshes=new();
        readonly Dictionary<string,Transform> anchors=new();
        readonly List<Material> waterMaterials=new();
        readonly List<Vector4> waterMasks=new();
        readonly List<float> waterLevels=new();
        Vector3 waterOrigin=new Vector3(float.PositiveInfinity,0,0);
        ReflectionProbe reflection;
        void LateUpdate()
        {
            if(waterOrigin==transform.position)return;waterOrigin=transform.position;
            for(int i=0;i<waterMaterials.Count;i++)
            {
                var mask=waterMasks[i];var center=transform.TransformPoint(new Vector3(mask.x,0,mask.y));
                waterMaterials[i].SetVector("_Island",new Vector4(center.x,center.z,mask.z,mask.w));waterMaterials[i].SetFloat("_WaterHeight",transform.position.y+waterLevels[i]);
            }
        }
        IEnumerator CaptureReflection()
        {yield return null;yield return null;if(reflection)reflection.RenderProbe();}
        Material previousSky,marinaSky;
        CandyEnvironment coastalEnvironment;
        AmbientMode previousAmbientMode;
        Color previousSkyColor,previousEquatorColor,previousGroundColor,previousFogColor;
        float previousAmbientIntensity,previousFogDensity;
        FogMode previousFogMode;
        bool previousFog,ownsLighting;
        SphericalHarmonicsL2 previousProbe;
        void OnEnable()=>GameEvents.Emoted+=Residence;
        void OnDisable(){GameEvents.Emoted-=Residence;RestoreLighting();}
        void Residence(GameObject owner,string state)
        {
            if(owner!=gameObject)return;
            if(state=="LobbyParked"){RestoreLighting();if(volume)volume.enabled=false;}
            else if(state=="LobbyResumed"){ApplyLighting(coastalEnvironment);if(volume)volume.enabled=true;}
        }
        void RestoreLighting()
        {
            if(!ownsLighting)return;ownsLighting=false;
            if(RenderSettings.skybox!=marinaSky)return;
            RenderSettings.skybox=previousSky;RenderSettings.ambientMode=previousAmbientMode;
            RenderSettings.ambientSkyColor=previousSkyColor;RenderSettings.ambientEquatorColor=previousEquatorColor;RenderSettings.ambientGroundColor=previousGroundColor;RenderSettings.ambientIntensity=previousAmbientIntensity;RenderSettings.ambientProbe=previousProbe;
            RenderSettings.fog=previousFog;RenderSettings.fogMode=previousFogMode;RenderSettings.fogColor=previousFogColor;RenderSettings.fogDensity=previousFogDensity;
        }
        Volume volume;
        VolumeProfile volumeProfile;
        public Transform Anchor(string id)=>anchors.TryGetValue(id,out var point)?point:null;

        public void Build(ILobbyControls lobby,CandyEnvironment environment)
        {
            if(Built)return;controls=lobby;mesh=new MarinaMesh();
            visual=new GameObject("Visual · Mayhem Marina").transform;visual.SetParent(transform,false);
            ShoreAndCourts();Pavilion();StyleShop();Lounge();UpperTerraces();Landmark();Planting();DistantIslands();
            mesh.Flush(visual);
            if(controls!=null)controls.SetFloor(mesh.CollisionMesh,visual);
            Environment(environment);Water(environment);Signs();
            Mark("Arrival",Arrival);Mark("Wardrobe",Wardrobe);Mark("Play screen",PlayScreen);
            Mark("Play interaction",new Vector3(0,1.70f,3.4f));Mark("Lounge",new Vector3(13,1.28f,5));
            Mark("Pool",new Vector3(17,.3f,-8));Mark("Roof terrace",new Vector3(-12,4.05f,17));
            Mark("Overview camera",new Vector3(-29,18,-39));Mark("Arrival camera",new Vector3(-11,3.1f,-8.9f));
            Built=true;
        }
        void Mark(string id,Vector3 position)
        {var point=new GameObject("Marina anchor · "+id).transform;point.SetParent(visual,false);point.localPosition=position;anchors[id]=point;}
        void Environment(CandyEnvironment environment)
        {
            coastalEnvironment=environment;marinaSky=Resources.Load<Material>("MayhemMarina/MarinaSky");
            ApplyLighting(environment);
            var go=new GameObject("Marina warm coastal grading");go.transform.SetParent(visual,false);volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=3;
            volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();volume.profile=volumeProfile;
            var tone=volumeProfile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            var color=volumeProfile.Add<ColorAdjustments>();color.postExposure.Override(.1f);color.contrast.Override(5);color.saturation.Override(4);
            var bloom=volumeProfile.Add<Bloom>();bloom.intensity.Override(.14f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.55f);
            var probeObject=new GameObject("Marina reflection capture");probeObject.transform.SetParent(visual,false);probeObject.transform.localPosition=new Vector3(0,4,3);
            var probe=probeObject.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=256;probe.size=new Vector3(56,24,65);probe.boxProjection=true;probe.intensity=.8f;probe.nearClipPlane=.3f;probe.farClipPlane=250;probe.clearFlags=ReflectionProbeClearFlags.Skybox;reflection=probe;StartCoroutine(CaptureReflection());
            foreach(var p in new[]{new Vector3(0,4.0f,8),new Vector3(13.5f,3.6f,6),new Vector3(-13,3.6f,8)})
            {
                var lightObject=new GameObject("Marina sheltered warm bounce");lightObject.transform.SetParent(visual,false);lightObject.transform.localPosition=p;
                var fill=lightObject.AddComponent<Light>();fill.type=LightType.Point;fill.color=new Color(1,.74f,.45f);fill.intensity=2.5f;fill.range=8;fill.shadows=LightShadows.None;
            }
        }
        void ApplyLighting(CandyEnvironment environment)
        {
            if(!ownsLighting)
            {
                previousSky=RenderSettings.skybox;previousAmbientMode=RenderSettings.ambientMode;previousSkyColor=RenderSettings.ambientSkyColor;previousEquatorColor=RenderSettings.ambientEquatorColor;previousGroundColor=RenderSettings.ambientGroundColor;previousAmbientIntensity=RenderSettings.ambientIntensity;previousProbe=RenderSettings.ambientProbe;
                previousFog=RenderSettings.fog;previousFogMode=RenderSettings.fogMode;previousFogColor=RenderSettings.fogColor;previousFogDensity=RenderSettings.fogDensity;
            }
            if(environment)
            {
                if(environment.sandRenderer)environment.sandRenderer.gameObject.SetActive(false);
                if(environment.waterRenderer)environment.waterRenderer.gameObject.SetActive(false);
                environment.enabled=false;
                var sun=environment.sun;
                if(sun){sun.transform.rotation=Quaternion.Euler(43,-38,0);sun.color=new Color(1,.89f,.75f);sun.intensity=2.1f;sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.25f;}
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.69f,.78f,.88f);RenderSettings.ambientEquatorColor=new Color(.74f,.70f,.60f);RenderSettings.ambientGroundColor=new Color(.47f,.43f,.35f);RenderSettings.ambientIntensity=1;
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.43f,.38f,.30f));ambient.AddDirectionalLight(Vector3.up,new Color(.65f,.78f,.91f),.48f);ambient.AddDirectionalLight(Vector3.forward,new Color(1,.71f,.4f),.22f);RenderSettings.ambientProbe=ambient;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.68f,.84f,.91f);RenderSettings.fogDensity=.0017f;
            if(marinaSky)RenderSettings.skybox=marinaSky;
            if(Services.Session?.ActiveCamera)Services.Session.ActiveCamera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
            ownsLighting=true;
        }
        void Water(CandyEnvironment environment)
        {
            Shader shader=Shader.Find("DeadlyJenga/Marina Water");if(!shader)return;
            Material Material(Vector2 center,float water,float extent)
            {
                var m=new Material(shader);m.name="Marina turquoise water";
                m.SetColor("_Shallow",new Color(.035f,.65f,.62f));m.SetColor("_Deep",new Color(.025f,.24f,.48f));m.SetColor("_Foam",new Color(.9f,1,.97f));m.SetFloat("_FoamStrength",.22f);m.SetFloat("_Glints",.34f);m.SetFloat("_Shine",.88f);m.SetFloat("_WaveHeight",extent>10 ? .12f : .025f);m.SetFloat("_WaveSpeed",.55f);m.SetFloat("_WaveChop",.25f);m.SetFloat("_WaveExtent",72);m.SetFloat("_WaterHeight",water);m.SetFloat("_SandTop",water-.72f);m.SetFloat("_SandDepth",3);m.SetFloat("_HasTerrain",0);m.SetFloat("_Submersion",.72f);m.SetFloat("_Plateau",.85f);m.SetVector("_Island",new Vector4(center.x,center.y,extent,extent*.86f));extraMaterials.Add(m);waterMaterials.Add(m);waterMasks.Add(new Vector4(center.x,center.y,extent,extent*.86f));waterLevels.Add(water);return m;
            }
            var oceanMesh=CandyOceanSurface.Create(96,1800,80);extraMeshes.Add(oceanMesh);
            var ocean=new GameObject("Marina ocean",typeof(MeshFilter),typeof(MeshRenderer));ocean.transform.SetParent(visual,false);ocean.transform.localPosition=Vector3.down*.12f;ocean.transform.localScale=new Vector3(1800,1,1800);ocean.GetComponent<MeshFilter>().sharedMesh=oceanMesh;ocean.GetComponent<MeshRenderer>().sharedMaterial=Material(new Vector2(0,3),-.12f,38);ocean.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var vertices=new List<Vector3>{Vector3.zero};var triangles=new List<int>();
            for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;vertices.Add(new Vector3(Mathf.Sin(a)*4.77f,0,Mathf.Cos(a)*4.77f));if(i>0){triangles.Add(0);triangles.Add(i+1);triangles.Add(i);}}
            var poolMesh=new Mesh{name="Marina shallow practice pool"};poolMesh.SetVertices(vertices);poolMesh.SetTriangles(triangles,0);poolMesh.RecalculateNormals();poolMesh.RecalculateBounds();extraMeshes.Add(poolMesh);
            var pool=new GameObject("Clear practice pool water",typeof(MeshFilter),typeof(MeshRenderer));pool.transform.SetParent(visual,false);pool.transform.localPosition=new Vector3(17,.61f,-8);pool.GetComponent<MeshFilter>().sharedMesh=poolMesh;pool.GetComponent<MeshRenderer>().sharedMaterial=Material(new Vector2(17,-8),.61f,6);pool.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        void Signs()
        {
            
            Sign("STYLE",new Vector3(-13,5.35f,4.25f),.12f,new Color(.26f,.12f,.055f));
            Sign("MAYHEM MARINA",new Vector3(-12.6f,1.5f,-12.8f),.055f,new Color(.85f,.92f,.84f),new Vector3(60,24,0));
        }
        void Sign(string text,Vector3 position,float size,Color color,Vector3? rotation=null)
        {
            var go=new GameObject("Marina lettering · "+text);go.transform.SetParent(visual,false);go.transform.localPosition=position;go.transform.localEulerAngles=rotation??Vector3.zero;
            var t=go.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=100;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.fontStyle=FontStyle.Bold;go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
        }
        void OnDestroy()
        {mesh?.Dispose();foreach(var m in extraMeshes)if(m)Destroy(m);foreach(var m in extraMaterials)if(m)Destroy(m);if(volumeProfile)Destroy(volumeProfile);RestoreLighting();if(visual)Destroy(visual.gameObject);}
    }
}
