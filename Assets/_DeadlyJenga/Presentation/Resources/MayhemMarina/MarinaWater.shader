Shader "DeadlyJenga/Marina Water"
{
 Properties
 {
  _Shallow("Lagoon turquoise",Color)=(.035,.65,.62,1)
  _Deep("Offshore blue",Color)=(.025,.24,.48,1)
  _Foam("Shore foam",Color)=(.9,1,.97,1)
  _WaveHeight("Wave height",Float)=.12
  _WaveSpeed("Wave speed",Float)=.55
  _WaveChop("Wave chop",Float)=.25
  _WaveExtent("Detail extent",Float)=72
  _WaterHeight("Water height",Float)=-.12
  _Island("Lagoon center and extents",Vector)=(0,3,38,32)
  _Glints("Sun sparkle",Float)=.34
  _Shine("Shine",Float)=.88
  _FoamStrength("Foam amount",Float)=.22
  _SandTop("Seabed reference",Float)=-.84
  _SandDepth("Offshore depth",Float)=3
  _HasTerrain("Terrain mask",Float)=0
  _Submersion("Shallow depth",Float)=.72
  _Plateau("Shelf radius",Float)=.85
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass
  {
   Name "Coastal water"
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Shallow,_Deep,_Foam;
   float4 _Island;
   float _WaveHeight,_WaveSpeed,_WaveChop,_WaveExtent,_WaterHeight,_Glints,_Shine,_FoamStrength,_SandTop,_SandDepth,_HasTerrain,_Submersion,_Plateau;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   void Wave(float2 d,float wavelength,float height,float2 p,float t,inout float y,inout float2 slope)
   {
    float k=6.2831853/wavelength,phase=k*dot(d,p)-t*sqrt(9.81*k);
    y+=sin(phase)*height;slope+=d*cos(phase)*height*k;
   }
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 p=TransformObjectToWorld(i.positionOS.xyz);float2 slope=0;float height=0;
    float2 local=p.xz-_Island.xy;
    float fade=1-smoothstep(_WaveExtent*.7,_WaveExtent,max(abs(local.x),abs(local.y)));
    float t=_Time.y*_WaveSpeed;
    Wave(normalize(float2(1,.4)),8,_WaveHeight*fade*.6,local,t,height,slope);
    Wave(normalize(float2(-.3,1)),4.3,_WaveHeight*fade*.28,local,t,height,slope);
    Wave(normalize(float2(.8,-.6)),2.1,_WaveHeight*fade*.12,local,t,height,slope);
    p.y+=height;o.world=p;o.normal=normalize(float3(-slope.x,1,-slope.y));o.positionCS=TransformWorldToHClip(p);o.fog=ComputeFogFactor(o.positionCS.z);return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 uv=i.positionCS.xy/_ScaledScreenParams.xy;
    float sceneDepth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
    float surfaceDepth=-TransformWorldToView(i.world).z;
    float depth=max(.001,sceneDepth-surfaceDepth);
    float2 p=i.world.xz;float time=_Time.y*.35;
    float detail=1-smoothstep(14,75,distance(_WorldSpaceCameraPos.xz,p));
    float2 ripple=float2(sin(p.x*4.7+p.y*1.2+time),cos(p.y*5.3-p.x*.8-time))*detail;
    float3 normal=normalize(i.normal+float3(ripple.x*.09,0,ripple.y*.09));
    float3 view=normalize(GetWorldSpaceViewDir(i.world));
    float distanceFromCove=length((p-_Island.xy)/max(_Island.zw,float2(1,1)));
    float offshore=smoothstep(.35,2.7,distanceFromCove);
    half3 color=lerp(_Shallow.rgb,_Deep.rgb,offshore);
    float shallow=max(1-smoothstep(.1,2.3,depth),(1-smoothstep(.2,1.1,distanceFromCove))*.65);
    float caustic=pow(saturate(1-abs(sin(p.x*3+sin(p.y*2+time))+sin(p.y*3.2-time))*.8),5);
    color+=half3(.12,.25,.22)*caustic*shallow*detail;
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    float3 h=normalize(view+sun.direction);
    float spec=pow(saturate(dot(normal,h)),180)*_Glints*sun.shadowAttenuation;
    float fresnel=.025+.47*pow(1-saturate(dot(normal,view)),5);
    half3 reflection=GlossyEnvironmentReflection(reflect(-view,normal),i.world,.14h,1.0h);
    color=lerp(color,reflection,fresnel*_Shine)+sun.color*spec*.8;
    float foam=(1-smoothstep(.025,.95,depth))*(.65+.35*sin(depth*32-time*4+ripple.x));
    color=lerp(color,_Foam.rgb,saturate(foam*(.65+_FoamStrength)));
    color=MixFog(color,i.fog);
    return half4(color,lerp(.90,.99,saturate(offshore*.7+fresnel)));
   }
   ENDHLSL
  }
 }
}
