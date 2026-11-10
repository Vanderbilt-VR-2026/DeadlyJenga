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
        public static readonly Vector3 CodeKeyboard = new(-4.87f, 2.84f, 10.30f);
        public static readonly Quaternion CodeKeyboardFacing = Quaternion.Euler(61, 0, 0);
        public static readonly Vector3 StyleScreen = new(-10.7f, 3.05f, 9.22f);
        public static readonly Quaternion StyleScreenFacing = Quaternion.Euler(0, 65, 0);
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
            }
            if (poolWater)
            {
                var center = transform.TransformPoint(new Vector3(17, .61f, -8));
                poolWater.SetVector("_Island", new Vector4(center.x, center.z, 6, 5.16f));
                poolWater.SetFloat("_WaterHeight", center.y);
                poolWater.SetFloat("_SandTop", center.y - .72f);
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
            if (state=="LobbyParked")
            {
                RestoreLighting();
                if (visual) visual.gameObject.SetActive(false);
                HideLegacyGround(coastalEnvironment);
            }
            else if (state=="LobbyResumed")
            {
                ApplyLighting(coastalEnvironment);
                if (visual) visual.gameObject.SetActive(true);
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
            ShoreAndCourts();Pavilion();StyleShop();Lounge();UpperTerraces();Landmark();Planting();DistantIslands();
            BeaconWalk();MarshAndBeach();JetSkiDock();LightingFixtures();SignMounts();
            mesh.Disk(new Vector3(0, OceanLevel - SeabedDepth - .12f, 3), 38, .12f, MarinaFinish.Sand, false, 80);
            mesh.Flush(visual);
            if(controls!=null)controls.SetFloor(mesh.CollisionMesh,visual);
            Environment(environment);Water(environment);Signs();BuildProjectorBeam();
            Mark("Arrival",Arrival);Mark("Wardrobe",Wardrobe);Mark("Play screen",PlayScreen);
            Mark("Play interaction",new Vector3(0,1.285f,3.4f));Mark("Lounge",new Vector3(13,1.28f,5));
            Mark("Beacon Walk", Vector3.Lerp(BeaconWalkStart, BeaconWalkEnd, .5f));
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
            var go=new GameObject("Marina warm coastal grading");go.transform.SetParent(visual,false);var volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=3;
            volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();volume.profile=volumeProfile;
            var tone=volumeProfile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            var color=volumeProfile.Add<ColorAdjustments>();color.postExposure.Override(-.1f);color.contrast.Override(13);color.saturation.Override(-2);
            var bloom=volumeProfile.Add<Bloom>();bloom.intensity.Override(.14f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.55f);
            var probeObject=new GameObject("Marina reflection capture");probeObject.transform.SetParent(visual,false);probeObject.transform.localPosition=new Vector3(0,4,3);
            var probe=probeObject.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=256;probe.size=new Vector3(56,24,65);probe.boxProjection=true;probe.intensity=.8f;probe.nearClipPlane=.3f;probe.farClipPlane=250;probe.clearFlags=ReflectionProbeClearFlags.Skybox;reflection=probe;StartCoroutine(CaptureReflection());
            var amber = new Color(1, .68f, .34f);
            var cool = new Color(.35f, .78f, 1);
            Accent("Play Pavilion", new Vector3(0, 4.7f, 7), new Color(1,.96f,.91f), 2.4f, 8);
            Accent("Style", new Vector3(-13, 3.5f, 8.6f), new Color(1, .79f, .61f), 3, 6);
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
                if(sun){sun.transform.rotation=Quaternion.Euler(29,-53,0);sun.color=new Color(1,.85f,.69f);sun.intensity=2.05f;sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.25f;}
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.61f,.75f,.93f);RenderSettings.ambientEquatorColor=new Color(.58f,.66f,.74f);RenderSettings.ambientGroundColor=new Color(.41f,.35f,.30f);RenderSettings.ambientIntensity=1;
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.33f,.34f,.35f));ambient.AddDirectionalLight(Vector3.up,new Color(.59f,.76f,.97f),.47f);ambient.AddDirectionalLight(Vector3.forward,new Color(1,.68f,.36f),.21f);RenderSettings.ambientProbe=ambient;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.68f,.84f,.91f);RenderSettings.fogDensity=.0017f;
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
            }
            if (!poolShader) { LateUpdate(); return; }
            var vertices=new List<Vector3>{Vector3.zero};var triangles=new List<int>();
            for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;vertices.Add(new Vector3(Mathf.Sin(a)*4.77f,0,Mathf.Cos(a)*4.77f));if(i>0){triangles.Add(0);triangles.Add(i+1);triangles.Add(i);}}
            var poolMesh=new Mesh{name="Marina shallow practice pool"};poolMesh.SetVertices(vertices);poolMesh.SetTriangles(triangles,0);poolMesh.RecalculateNormals();poolMesh.RecalculateBounds();extraMeshes.Add(poolMesh);
            poolWater = CreateBasinWater(poolShader);
            WaterSurface("Clear practice pool water", new Vector3(17, .61f, -8), poolMesh, poolWater);
            LateUpdate();
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
            Sign("PLAY  /  AHEAD", new Vector3(-9.2f, 2.68f, -5.082f), .034f, cream, new Vector3(0, 12, 0));
            Sign("LOUNGE  /  POOL  >", new Vector3(6.65f, 2.635f, 3.56f), .032f, cream);
            Sign("BEACHSIDE CINEMA", new Vector3(0, 5.58f, 3.58f), .062f, cream);
            Sign("STYLE", new Vector3(-13, 5.18f, 4.85f), .095f, cream);
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
