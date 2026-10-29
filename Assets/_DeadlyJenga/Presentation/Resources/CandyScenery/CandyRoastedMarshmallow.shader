Shader "DeadlyJenga/Roasting Fork Surfaces"
{
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) _Wood("Hardwood",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;float _Wood;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 local:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float noise(float3 p)
   {
    float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
   }
   float filteredNoise(float3 p)
   {
    float footprint=max(length(ddx(p)),length(ddy(p)));
    return lerp(noise(p),.5,smoothstep(.35,1.3,footprint));
   }
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.local=i.positionOS.xyz;return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 p=i.local;float fine=filteredNoise(p*700),coarse=filteredNoise(p*31)*.7+filteredNoise(p*73)*.3;
    float toast=smoothstep(.46,.76,coarse),charred=smoothstep(.70,.85,coarse);
    float3 color=lerp(float3(.94,.91,.81),float3(.43,.19,.055),toast*.86);
    float pores=pow(saturate((filteredNoise(p*380)-.62)*3),2);
    color=lerp(color,float3(.065,.027,.013),charred*.8)*(.95+.05*fine-pores*.10);float rough=.9;
    if(_Wood>.5)
    {
     float grain=noise(float3(p.x*55,p.y*2,p.z*55)),lines=pow(abs(sin((p.x+p.z)*100+grain*14)),9);
     color=lerp(float3(.15,.055,.018),float3(.39,.19,.067),grain)*(1-lines*.15);rough=.65;
    }
    float3 n=normalize(i.normal),view=GetWorldSpaceNormalizeViewDir(i.world);Light light=GetMainLight();
    float diffuse=saturate(dot(n,light.direction));float3 ambient=max(SampleSH(n),float3(.09,.09,.09));
    float spec=pow(saturate(dot(n,normalize(light.direction+view))),lerp(90,12,rough))*(1-rough)*.16;
    return half4(color*(ambient+light.color*diffuse*.85)+light.color*spec,1);
   }
   ENDHLSL
  }
 }
}
