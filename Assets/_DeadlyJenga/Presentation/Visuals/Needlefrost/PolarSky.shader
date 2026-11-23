Shader "DeadlyJenga/Needlefrost/Sky" {
Properties {  }
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

half4 frag(Vary i):SV_Target {
 float3 d=normalize(i.local);float a=atan2(d.x,d.z), h=d.y;
 float3 sky=lerp(float3(.037,.105,.32),float3(.003,.009,.085),saturate(h*2+.10));
 float2 q=d.xz*4;
 float ribbon=.19+.075*sin(a*3+.4)+.07*sin(a*7.3);
 float aur=exp(-abs(h-ribbon)*17)*(.25+.75*noise(float2(a*47,h*1.7)));
 sky+=aur*float3(.006,.52,.39);
 float stars=step(.994,hash(floor(float2(a,h)*510)))*smoothstep(.12,.4,h);
 sky+=stars*.45;
 float moon=distance(d,normalize(float3(.42,.155,1)));
 sky+=float3(.75,.88,1)*(1-smoothstep(.018,.02,moon))*(.78+.22*noise(d.xy*360));
 for(int j=0;j<2;j++){
 float2 t=q*(2.5+j)+j*13;float ridge=.011+.080*noise(t)+.039*noise(t*3)+.023*noise(t*9)+.009*noise(t*21)-j*.041;
 float snow=saturate((h-ridge+.025)*18+1.25*(noise(float2(a*97+h*15,h*81))-.28));
 sky=lerp(sky,lerp(float3(.032,.089,.23),float3(.23,.37,.61),snow)*(1-j*.12),1-smoothstep(ridge-.002,ridge+.002,h)); }
 return half4(sky,1); }
ENDHLSL
} } }
