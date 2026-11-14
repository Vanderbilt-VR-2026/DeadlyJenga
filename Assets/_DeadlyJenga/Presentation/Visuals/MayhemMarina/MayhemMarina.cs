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
        // Showtime Plaza: one open deck between the arrival island and the cinema.
        // The wardrobe stage and the Hall of Fame face each other across it.
        public static readonly Vector3 PlazaCenter = new(0, 1.31f, -3.6f);
        public const float PlazaRadius = 7.2f;
        static readonly Quaternion StyleStageTurn = Quaternion.Euler(0, -58, 0);
        static readonly Quaternion FameTurn = Quaternion.Euler(0, 58, 0);
        public static readonly Vector3 Wardrobe = PlazaCenter + StyleStageTurn * new Vector3(0, .22f, 4.3f);
        public static readonly Vector3 PlayScreen=new(0,3.85f,11.1f);
        public static readonly Vector3 CodeKeyboard = new(-4.87f, 2.84f, 10.30f);
        public static readonly Quaternion CodeKeyboardFacing = Quaternion.Euler(61, 0, 0);
        public static readonly Vector3 StyleScreen = Wardrobe + StyleStageTurn * new Vector3(2.3f, 1.5f, 1.02f);
        public static readonly Quaternion StyleScreenFacing = StyleStageTurn * Quaternion.Euler(0, 65, 0);
        public static readonly Vector3 StyleMirror = Wardrobe + StyleStageTurn * new Vector3(0, 1.85f, 1.88f);
        public static readonly Quaternion StyleMirrorFacing = StyleStageTurn;
        // The last-match board stands at the far side of the Hall of Fame, readable from the plaza.
        public static readonly Vector3 FameBoard = PlazaCenter + FameTurn * new Vector3(0, 2.25f, 5.85f);
        public static readonly Quaternion FameFacing = FameTurn;
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
        const float OceanLevel = -.12f;
        const float SeabedDepth = 1.55f;
        Renderer ocean;
        MaterialPropertyBlock oceanProperties;
        Material poolWater, signMaterial;
        Vector3 waterOrigin = new(float.PositiveInfinity, 0, 0);
        ReflectionProbe reflection;
        Renderer paintedHorizon;
        void LateUpdate()
        {
            if (waterOrigin == transform.position) return;
            waterOrigin = transform.position;
            if (ocean)
            {
                var center = transform.TransformPoint(Vector3.forward * 3);
                oceanProperties.SetVector("_Island", new Vector4(center.x, center.z, 24, 23));
                oceanProperties.SetFloat("_WaterHeight", center.y + OceanLevel);
                oceanProperties.SetFloat("_SandTop", center.y + OceanLevel - SeabedDepth);
                ocean.SetPropertyBlock(oceanProperties);
                if(marinaSky && marinaSky.HasProperty("_CoastWaterLevel"))
                    marinaSky.SetFloat("_CoastWaterLevel",center.y+OceanLevel);
            }
            if (poolWater)
            {
                var center = transform.TransformPoint(new Vector3(17, .61f, -8));
                poolWater.SetVector("_Island", new Vector4(center.x, center.z, 6, 5.16f));
                poolWater.SetFloat("_WaterHeight", center.y);
                poolWater.SetFloat("_SandTop", center.y - .72f);
            }
            if(paintedHorizon)
            {
                var horizonProperties=new MaterialPropertyBlock();
                horizonProperties.SetFloat("_CoastStyle",3);
                horizonProperties.SetFloat("_CoastWater",transform.position.y+OceanLevel);
                paintedHorizon.SetPropertyBlock(horizonProperties);
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
        bool visiting,visitCameraRendering,matchFog;
        float matchOutage;
        Material matchSky;
        bool visitSunEnabled;
        AmbientMode matchAmbientMode;
        Color matchSkyColor,matchEquatorColor,matchGroundColor;
        float matchAmbientIntensity;
        SphericalHarmonicsL2 matchAmbientProbe;
        void OnEnable()
        {
            GameEvents.Emoted+=Residence;
            RenderPipelineManager.beginCameraRendering+=BeginVisitCamera;
            RenderPipelineManager.endCameraRendering+=EndVisitCamera;
        }
        void OnDisable()
        {
            GameEvents.Emoted-=Residence;
            RenderPipelineManager.beginCameraRendering-=BeginVisitCamera;
            RenderPipelineManager.endCameraRendering-=EndVisitCamera;
            RestoreVisitCamera();
            RestoreLighting();
        }
        void BeginVisitCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!visiting||visitCameraRendering||camera!=(controls as ISessionInfo)?.ActiveCamera)return;
            visitCameraRendering=true;
            matchFog=RenderSettings.fog;matchSky=RenderSettings.skybox;
            matchOutage=Shader.GetGlobalFloat("_CandyOutage");
            matchAmbientMode=RenderSettings.ambientMode;
            matchSkyColor=RenderSettings.ambientSkyColor;
            matchEquatorColor=RenderSettings.ambientEquatorColor;
            matchGroundColor=RenderSettings.ambientGroundColor;
            matchAmbientIntensity=RenderSettings.ambientIntensity;
            matchAmbientProbe=RenderSettings.ambientProbe;
            ApplyDaylightAmbient();
            RenderSettings.fog=false;
            if(marinaSky)RenderSettings.skybox=marinaSky;
            Shader.SetGlobalFloat("_CandyOutage",0);
            if(coastalEnvironment&&coastalEnvironment.sun)
            {
                visitSunEnabled=coastalEnvironment.sun.enabled;
                coastalEnvironment.sun.enabled=true;
            }
        }
        void EndVisitCamera(ScriptableRenderContext context,Camera camera)
        {
            if(camera==(controls as ISessionInfo)?.ActiveCamera)RestoreVisitCamera();
        }
        void RestoreVisitCamera()
        {
            if(!visitCameraRendering)return;
            visitCameraRendering=false;
            RenderSettings.fog=matchFog;RenderSettings.skybox=matchSky;
            Shader.SetGlobalFloat("_CandyOutage",matchOutage);
            RenderSettings.ambientMode=matchAmbientMode;
            RenderSettings.ambientSkyColor=matchSkyColor;
            RenderSettings.ambientEquatorColor=matchEquatorColor;
            RenderSettings.ambientGroundColor=matchGroundColor;
            RenderSettings.ambientIntensity=matchAmbientIntensity;
            RenderSettings.ambientProbe=matchAmbientProbe;
            if(coastalEnvironment&&coastalEnvironment.sun)coastalEnvironment.sun.enabled=visitSunEnabled;
        }
        void Residence(GameObject owner,string state)
        {
            if(owner!=gameObject)return;
            if(state is "LobbyParked" or "LobbyResumed" or "LobbyVisitOpened")visiting=state=="LobbyVisitOpened";
            if (state=="LobbyParked")
            {
                RestoreLighting();
                if (visual) visual.gameObject.SetActive(false);
                HideLegacyGround(coastalEnvironment);
            }
            else if (state=="LobbyResumed" || state=="LobbyVisitOpened")
            {
                bool resumed=state=="LobbyResumed";
                if (resumed) ApplyLighting(coastalEnvironment);
                if (visual) visual.gameObject.SetActive(true);
                if (volumeProfile && visual && visual.GetComponentInChildren<Volume>() is Volume grading) grading.enabled=resumed;
            }
        }
        void RestoreLighting()
        {
            if(!ownsLighting)return;ownsLighting=false;
            if(RenderSettings.skybox!=marinaSky)return;
            RenderSettings.skybox=previousSky;RenderSettings.ambientMode=previousAmbientMode;
            RenderSettings.ambientSkyColor=previousSkyColor;RenderSettings.ambientEquatorColor=previousEquatorColor;RenderSettings.ambientGroundColor=previousGroundColor;RenderSettings.ambientIntensity=previousAmbientIntensity;RenderSettings.ambientProbe=previousProbe;
            RenderSettings.fog=previousFog;RenderSettings.fogMode=previousFogMode;RenderSettings.fogColor=previousFogColor;RenderSettings.fogDensity=previousFogDensity;
        }
        static void ApplyDaylightAmbient()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.61f,.75f,.93f);
            RenderSettings.ambientEquatorColor=new Color(.58f,.66f,.74f);
            RenderSettings.ambientGroundColor=new Color(.41f,.35f,.30f);
            RenderSettings.ambientIntensity=1;
            var ambient=new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.33f,.34f,.35f));
            ambient.AddDirectionalLight(Vector3.up,new Color(.59f,.76f,.97f),.47f);
            ambient.AddDirectionalLight(Vector3.forward,new Color(1,.68f,.36f),.21f);
            RenderSettings.ambientProbe=ambient;
        }

        static void HideLegacyGround(CandyEnvironment environment)
        {
            if (!environment) return;
            if (environment.sandRenderer) environment.sandRenderer.gameObject.SetActive(false);
            if (environment.waterRenderer) environment.waterRenderer.gameObject.SetActive(false);
        }
        VolumeProfile volumeProfile;
        public Transform Anchor(string id)=>anchors.TryGetValue(id,out var point)?point:null;

        public void Build(ILobbyControls lobby,CandyEnvironment environment)
        {
            if(Built)return;controls=lobby;mesh=new MarinaMesh();
            visual=new GameObject("Visual · Mayhem Marina").transform;visual.SetParent(transform,false);
            ShoreAndCourts();ShowtimePlaza();Pavilion();StyleStage();HallOfFame();GardenLounge();Lounge();UpperTerraces();Landmark();Planting();
            BeaconWalk();MarshAndBeach();JetSkiDock();LightingFixtures();SignMounts();
            mesh.Disk(new Vector3(0, OceanLevel - SeabedDepth - .12f, 3), 38, .12f, MarinaFinish.Sand, false, 80);
            mesh.Flush(visual,transform.TransformPoint(Vector3.up*OceanLevel).y);
            if(controls!=null)controls.SetFloor(mesh.CollisionMesh,visual);
            Environment(environment);PaintedHorizon();Water(environment);CruiseShips();Signs();BuildProjectorBeam();
            Mark("Arrival",Arrival);Mark("Wardrobe",Wardrobe);Mark("Play screen",PlayScreen);
            Mark("Play interaction",new Vector3(0,1.285f,3.4f));Mark("Lounge",new Vector3(13,1.28f,5));
            Mark("Beacon Walk", Vector3.Lerp(BeaconWalkStart, BeaconWalkEnd, .5f));
            Mark("Pool",new Vector3(17,.3f,-8));Mark("Roof terrace",new Vector3(-12,4.05f,17));
            Mark("Showtime plaza",PlazaCenter);Mark("Hall of fame",FameBoard);
            Mark("Overview camera",new Vector3(-29,18,-39));Mark("Arrival camera",new Vector3(-11,3.1f,-8.9f));
            Built=true;
        }
        void Mark(string id,Vector3 position)
        {var point=new GameObject("Marina anchor · "+id).transform;point.SetParent(visual,false);point.localPosition=position;anchors[id]=point;}
        void Environment(CandyEnvironment environment)
        {
            coastalEnvironment=environment;
            var skyShader=Resources.Load<Shader>("CandyScenery/CandyScenicSky");
            var panorama=Resources.Load<Texture2D>("CandyScenery/CatwalksDayPanorama");
            if(skyShader && panorama)
            {
                marinaSky=new Material(skyShader){name="Marina · Catwalks coastal sky"};
                extraMaterials.Add(marinaSky);
                marinaSky.SetFloat("_CoastDay",1);
                marinaSky.SetFloat("_CoastHasPanorama",1);
                marinaSky.SetTexture("_CoastPanorama",panorama);
                marinaSky.SetColor("_Zenith",new Color(.018f,.25f,.82f));
                marinaSky.SetColor("_Horizon",new Color(.27f,.67f,1));
                marinaSky.SetVector("_SunDirection",CandyCatwalksCoast.SunDirection);
                marinaSky.SetColor("_SunColor",new Color(1,.90f,.74f));
                marinaSky.SetFloat("_CoastWaterLevel",transform.position.y+OceanLevel);
            }
            else marinaSky=Resources.Load<Material>("MayhemMarina/MarinaSky");
            ApplyLighting(environment);
            var go=new GameObject("Marina warm coastal grading");go.transform.SetParent(visual,false);var volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=3;
            volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();volume.profile=volumeProfile;
            var tone=volumeProfile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            var color=volumeProfile.Add<ColorAdjustments>();color.postExposure.Override(0);color.contrast.Override(7);color.saturation.Override(3);
            var bloom=volumeProfile.Add<Bloom>();bloom.intensity.Override(.14f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.55f);
            var probeObject=new GameObject("Marina reflection capture");probeObject.transform.SetParent(visual,false);probeObject.transform.localPosition=new Vector3(0,4,3);
            var probe=probeObject.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=256;probe.size=new Vector3(56,24,65);probe.boxProjection=true;probe.intensity=.8f;probe.nearClipPlane=.3f;probe.farClipPlane=250;probe.clearFlags=ReflectionProbeClearFlags.Skybox;reflection=probe;StartCoroutine(CaptureReflection());
            var amber = new Color(1, .68f, .34f);
            var cool = new Color(.35f, .78f, 1);
            Accent("Play Pavilion", new Vector3(0, 4.7f, 7), new Color(1,.96f,.91f), 2.4f, 8);
            Accent("Garden lounge", new Vector3(-15, 3.9f, 4.75f), amber, 2.2f, 5.5f);
            Accent("Waterfront lounge", new Vector3(13.7f, 3.8f, 5.4f), amber, 3.2f, 6.5f);
            Accent("Arrival", new Vector3(-12.5f, 2.45f, -10.25f), amber, 2, 5.5f);
            Accent("Practice pool", new Vector3(19, 2.1f, -3.25f), cool, 2.5f, 5.5f);
            Accent("West terrace", new Vector3(-13.8f, 6.65f, 17.9f), amber, 3, 6.5f);
            Accent("East terrace", new Vector3(16.5f, 5.1f, 17.8f), cool, 2, 5);
        }
        void Accent(string zone, Vector3 position, Color color, float intensity, float range)
        {
            var fixture = new GameObject("Marina light · " + zone);
            fixture.transform.SetParent(visual, false);
            fixture.transform.localPosition = position;
            var light = fixture.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
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
                // Match skins own a temporary profile and update global ambient lighting every frame.
                var matchSkins = environment.GetComponent<CandySceneSkins>();
                if (matchSkins) matchSkins.enabled = false;
                HideLegacyGround(environment);
                environment.enabled=false;
                var sun=environment.sun;
                if(sun){sun.transform.rotation=Quaternion.LookRotation(-CandyCatwalksCoast.SunDirection);sun.color=new Color(1,.94f,.83f);sun.intensity=1.85f;sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.25f;}
            }
            ApplyDaylightAmbient();
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.37f,.66f,.83f);RenderSettings.fogDensity=.00045f;
            if(marinaSky)RenderSettings.skybox=marinaSky;
            if(Services.Session?.ActiveCamera)Services.Session.ActiveCamera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
            ownsLighting=true;
        }
        void Water(CandyEnvironment environment)
        {
            var profile = environment ? environment.profile : null;
            var poolShader = Shader.Find("DeadlyJenga/Marina Water");
            var oceanMaterial = profile ? profile.water : null;
            if (!oceanMaterial && poolShader) oceanMaterial = CreateBasinWater(poolShader);
            if (oceanMaterial)
            {
                float size = profile ? Mathf.Max(100, profile.oceanSize) : 1800;
                float extent = profile ? Mathf.Clamp(profile.waveExtent, 16, size * .4f) : 80;
                var oceanMesh = CandyOceanSurface.Create(profile ? profile.waterResolution : 96, size, extent);
                extraMeshes.Add(oceanMesh);
                ocean = WaterSurface("Marina ocean", new Vector3(0, OceanLevel, 3), oceanMesh, oceanMaterial);
                ocean.transform.localScale = new Vector3(size, 1, size);
                oceanProperties = new MaterialPropertyBlock();
                oceanProperties.SetFloat("_HasTerrain", 0);
                oceanProperties.SetFloat("_Submersion", SeabedDepth);
                oceanProperties.SetFloat("_SandDepth", profile ? profile.sandDepth : 3);
                oceanProperties.SetFloat("_Plateau", profile ? profile.sandPlateau : .85f);
                oceanProperties.SetFloat("_WaveExtent", extent);
                oceanProperties.SetFloat("_WaveHeight", profile ? profile.waveHeight : .12f);
                oceanProperties.SetFloat("_WaveSpeed", profile ? profile.waveSpeed : .55f);
                oceanProperties.SetFloat("_WaveChop", profile ? profile.waveChop : .25f);
                oceanProperties.SetColor("_Horizon", RenderSettings.fogColor);
                // Reuse the castle's water shading without its offshore geometry or render cameras.
                var spectrum=Resources.Load<Texture2D>("CandyScenery/CatwalksWaterSpectrum");
                var panorama=Resources.Load<Texture2D>("CandyScenery/CatwalksDayPanorama");
                var caustics=Resources.Load<Texture2D>("CandyScenery/CatwalksRefractedLight");
                if(spectrum)
                {
                    oceanProperties.SetFloat("_CatwalksCoast",1);
                    oceanProperties.SetFloat("_CoastDayWater",1);
                    oceanProperties.SetTexture("_CoastSpectrum",spectrum);
                    oceanProperties.SetVector("_CoastSun",CandyCatwalksCoast.SunDirection);
                    oceanProperties.SetFloat("_CoastHasPanorama",panorama?1:0);
                    if(panorama)oceanProperties.SetTexture("_CoastPanorama",panorama);
                    if(caustics)oceanProperties.SetTexture("_CoastCaustics",caustics);
                    oceanProperties.SetFloat("_CoastReflectionReady",0);
                    var absent=new Vector4(-100000,-100000,1,1);
                    foreach(var region in new[]{"_ReefA","_ReefB","_ReefC","_ReefD","_CoastIslet","_CoastIsletB","_CoastIsletC"})
                        oceanProperties.SetVector(region,absent);
                }
            }
            if (!poolShader) { LateUpdate(); return; }
            var vertices=new List<Vector3>{Vector3.zero};var triangles=new List<int>();
            for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;vertices.Add(new Vector3(Mathf.Sin(a)*4.77f,0,Mathf.Cos(a)*4.77f));if(i>0){triangles.Add(0);triangles.Add(i+1);triangles.Add(i);}}
            var poolMesh=new Mesh{name="Marina shallow practice pool"};poolMesh.SetVertices(vertices);poolMesh.SetTriangles(triangles,0);poolMesh.RecalculateNormals();poolMesh.RecalculateBounds();extraMeshes.Add(poolMesh);
            poolWater = CreateBasinWater(poolShader);
            WaterSurface("Clear practice pool water", new Vector3(17, .61f, -8), poolMesh, poolWater);
            LateUpdate();
        }
        void PaintedHorizon()
        {
            var shader=Resources.Load<Shader>("CandyScenery/CandyCatwalksCoast");
            var artwork=Resources.Load<Texture2D>("CandyScenery/CatwalksHorizon");
            if(!shader || !artwork)return;
            var material=new Material(shader){name="Marina · distant painted land"};
            material.SetTexture("_CoastHorizon",artwork);
            extraMaterials.Add(material);
            const int spans=40;
            var vertices=new Vector3[(spans+1)*2];
            var uv=new Vector2[vertices.Length];
            var colors=new Color[vertices.Length];
            var triangles=new int[spans*6];
            for(int i=0;i<=spans;i++)
            {
                float t=i/(float)spans,angle=Mathf.Lerp(124,43,t)*Mathf.Deg2Rad;
                var point=new Vector3(Mathf.Cos(angle)*446,OceanLevel-6.8f,Mathf.Sin(angle)*446);
                vertices[i*2]=point;vertices[i*2+1]=point+Vector3.up*72;
                uv[i*2]=new Vector2(t,.222f);uv[i*2+1]=new Vector2(t,.52f);
                colors[i*2]=colors[i*2+1]=Color.white;
                if(i==spans)continue;
                int a=i*2,j=i*6;
                triangles[j]=a;triangles[j+1]=a+1;triangles[j+2]=a+2;
                triangles[j+3]=a+1;triangles[j+4]=a+3;triangles[j+5]=a+2;
            }
            var strip=new Mesh{name="Marina painted horizon",vertices=vertices,uv=uv,colors=colors,triangles=triangles};
            strip.RecalculateNormals();strip.RecalculateBounds();extraMeshes.Add(strip);
            paintedHorizon=WaterSurface("Marina distant island backdrop",Vector3.zero,strip,material);
            paintedHorizon.receiveShadows=false;
            paintedHorizon.lightProbeUsage=LightProbeUsage.Off;
            paintedHorizon.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        Renderer WaterSurface(string label, Vector3 position, Mesh surface, Material material)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(visual, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = surface;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }
        Material CreateBasinWater(Shader shader)
        {
            var material = new Material(shader) { name = "Marina turquoise water" };
            material.SetColor("_Shallow", new Color(.035f, .65f, .62f));
            material.SetColor("_Deep", new Color(.025f, .24f, .48f));
            material.SetColor("_Foam", new Color(.9f, 1, .97f));
            material.SetFloat("_FoamStrength", .22f);
            material.SetFloat("_Glints", .34f);
            material.SetFloat("_Shine", .88f);
            material.SetFloat("_WaveHeight", .025f);
            material.SetFloat("_WaveSpeed", .55f);
            material.SetFloat("_WaveChop", .25f);
            material.SetFloat("_WaveExtent", 72);
            material.SetFloat("_SandDepth", 3);
            material.SetFloat("_HasTerrain", 0);
            material.SetFloat("_Submersion", .72f);
            material.SetFloat("_Plateau", .85f);
            extraMaterials.Add(material);
            return material;
        }
        void BuildProjectorBeam()
        {
            var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!shader) return;

            var material=new Material(shader){name="Marina soft projector light"};
            material.SetOverrideTag("RenderType","Transparent");
            material.SetFloat("_Surface",1);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.One);
            material.SetFloat("_ZWrite",0);
            material.SetFloat("_Cull",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue=(int)RenderQueue.Transparent;
            material.SetColor("_BaseColor",new Color(1,.87f,.68f,.10f));
            extraMaterials.Add(material);

            var corners=new[]{
                new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)
            };
            var vertices=new Vector3[12];
            var colors=new Color[12];
            var indices=new List<int>();
            var screen=ProjectionSurface+Vector3.back*.045f;
            for (int ring=0;ring<3;ring++)
            {
                float fraction=ring==0?0:ring==1?.20f:1;
                float opacity=ring==0?.92f:ring==1?.70f:.08f;
                for (int side=0;side<4;side++)
                {
                    var corner=corners[side];
                    var start=ProjectorLens+new Vector3(corner.x*.095f,corner.y*.075f,0);
                    var end=screen+new Vector3(corner.x*3.37f,corner.y*1.50f,0);
                    int index=ring*4+side;
                    vertices[index]=Vector3.Lerp(start,end,fraction);
                    colors[index]=new Color(1,1,1,opacity);
                    if (ring==2) continue;
                    int next=ring*4+(side+1)%4;
                    indices.AddRange(new[]{index,next,index+4,next,next+4,index+4});
                }
            }

            var beamMesh=new Mesh{name="Marina projector light frustum",vertices=vertices,colors=colors,triangles=indices.ToArray()};
            beamMesh.RecalculateBounds();
            extraMeshes.Add(beamMesh);
            var beam=new GameObject("Visual - beachside projector beam",typeof(MeshFilter),typeof(MeshRenderer));
            beam.transform.SetParent(visual,false);
            beam.GetComponent<MeshFilter>().sharedMesh=beamMesh;
            var renderer=beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        void Signs()
        {
            var cream = new Color(.95f, .88f, .69f);
            Sign("JET SKIS", new Vector3(-22.7f, 2.69f, -24.115f), .047f, cream, new Vector3(0, 180, 0));
            Sign("SOLO  /  E OR GRIP", new Vector3(-22.7f, 2.46f, -24.115f), .024f, cream, new Vector3(0, 180, 0));
            Sign("BEACON WALK", BeaconWalkGate + BeaconWalkFacing * Vector3.back * .13f, .048f, cream,
                BeaconWalkFacing.eulerAngles);
            Sign("LOUNGE  /  POOL  >", new Vector3(6.65f, 2.635f, 3.56f), .032f, cream);
            Sign("BEACHSIDE CINEMA", new Vector3(0, 5.58f, 3.58f), .062f, cream);
            var styleBase = new Vector3(Wardrobe.x, PlazaTop, Wardrobe.z);
            Sign("STYLE", styleBase + StyleStageTurn * new Vector3(0, 4.72f, 1.95f), .085f, cream, StyleStageTurn.eulerAngles);
            var fameBase = new Vector3(FameBoard.x, PlazaTop, FameBoard.z);
            Sign("HALL OF FAME", fameBase + FameTurn * new Vector3(0, 3.98f, .04f), .05f, cream, FameTurn.eulerAngles);
            Sign("SHOWTIME PLAZA", OnPlaza(-114, 4.7f, PlazaTop - PlazaCenter.y + .012f), .05f,
                new Color(.23f, .16f, .10f), new Vector3(90, 66, 0));
            Sign("WATERFRONT LOUNGE", new Vector3(13.7f, 4.42f, 2.49f), .044f, cream);
            Sign("PRACTICE POOL", new Vector3(19, 1.76f, -3.24f), .026f, cream, new Vector3(0, 180, 0));
            Sign("MAYHEM MARINA", new Vector3(-10, 1.352f, -10.1f), .06f, new Color(.23f, .16f, .10f), new Vector3(90, 0, 0));
        }
        void Sign(string text,Vector3 position,float size,Color color,Vector3? rotation=null)
        {
            var go=new GameObject("Marina lettering · "+text);go.transform.SetParent(visual,false);go.transform.localPosition=position;go.transform.localEulerAngles=rotation??Vector3.zero;
            var t=go.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=100;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.fontStyle=FontStyle.Bold;var renderer = go.GetComponent<MeshRenderer>();
            if (!signMaterial)
            {
                var shader = Resources.Load<Shader>("VillageLettering");
                if (shader)
                {
                    signMaterial = new Material(shader) { name = "Marina depth-tested lettering" };
                    extraMaterials.Add(signMaterial);
                }
            }
            renderer.sharedMaterial = signMaterial ? signMaterial : t.font.material;
            if (signMaterial) go.AddComponent<ChristmasTownText>();
        }
        void OnDestroy()
        {mesh?.Dispose();foreach(var m in extraMeshes)if(m)Destroy(m);foreach(var m in extraMaterials)if(m)Destroy(m);if(volumeProfile)Destroy(volumeProfile);RestoreLighting();if(visual)Destroy(visual.gameObject);}
    }
}
