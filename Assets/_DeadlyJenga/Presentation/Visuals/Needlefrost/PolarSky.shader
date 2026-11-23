Shader "DeadlyJenga/Needlefrost/Sky" {
Properties { _Horizon("Polar horizon",2D)="black" {} }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" }
Pass { Cull Off ZWrite Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct Input { float4 position:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Vary { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
Vary vert(Input v) {
 UNITY_SETUP_INSTANCE_ID(v); Vary o=(Vary)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 o.world=TransformObjectToWorld(v.position.xyz); o.position=TransformWorldToHClip(o.world);
 o.normal=TransformObjectToWorldNormal(v.normal); o.local=v.position.xyz; return o;
}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
 return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}

TEXTURE2D(_Horizon); SAMPLER(sampler_Horizon);
half4 frag(Vary i):SV_Target {
 float3 d=normalize(i.local);
 float a=atan2(d.x,d.z);
 float2 uv=float2(a*(3.0/6.2831853)+.53,saturate(.459+asin(clamp(d.y,-1,1))*2.05));
 float3 sky=SAMPLE_TEXTURE2D(_Horizon,sampler_Horizon,uv).rgb;
 float moon=distance(d,normalize(float3(.27,.105,1)));
 float maria=noise(d.xy*350)*.25+noise(d.xy*100)*.15;
 sky+=float3(.74,.85,1)*(1-smoothstep(.018,.0195,moon))*(1-maria);
 return half4(sky,1);
}
ENDHLSL
} } }
