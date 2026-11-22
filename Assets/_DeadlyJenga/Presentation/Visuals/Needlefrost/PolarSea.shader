Shader "DeadlyJenga/Needlefrost/Sea" {
Properties {  }
SubShader { Tags { "RenderPipeline"="UniversalPipeline"  }
Pass { Cull Off
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
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float2 p=i.world.xz;float t=_Time.y;
 float h=noise(p*.52+float2(t*.36,0))+.34*noise(p*1.83-float2(0,t*.42));
 float wave=sin(p.y*2.5+h*5+t*1.6);
 float channel=exp(-abs(p.x)*.30)*smoothstep(-30,-14,p.y)*(1-smoothstep(15,29,p.y));
 float3 water=lerp(float3(.008,.035,.15),float3(.014,.16,.38),h);
 water+=channel*float3(0,.21,.34);
 float2 cell=abs(frac((p-float2(0,-19))/float2(4,8)+.5)-.5)*float2(4,8);
 float coast=saturate(1-abs(cell.y-2.3)*1.9)*step(4,abs(p.x))*step(abs(p.x),27)*step(abs(p.y),23);
 float crest=smoothstep(.84,.99,wave)*smoothstep(.53,.91,h);
 water+=float3(.44,.69,.87)*(crest*.62+coast*crest);
 return half4(water,1); }
ENDHLSL
} } }
