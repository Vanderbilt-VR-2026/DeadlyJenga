Shader "DeadlyJenga/Needlefrost/Sea" {
Properties { _ContactFoam("Contact foam",Float)=0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline"  }
Pass { Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct Input { float4 position:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Vary { float4 position:SV_POSITION; float3 world:TEXCOORD0; float rim:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
Vary vert(Input v) {
 UNITY_SETUP_INSTANCE_ID(v); Vary o=(Vary)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 o.world=TransformObjectToWorld(v.position.xyz);
 float2 p=o.world.xz; float t=_Time.y;
 o.world.y+=(sin(dot(p,float2(.57,.86))+t*1.47)*.075+sin(dot(p,float2(-.91,.31))-t*1.1)*.048)
     *saturate(1-length(p)/105);
 o.position=TransformWorldToHClip(o.world);
 o.rim=v.uv.x; return o;
}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
 return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}

CBUFFER_START(UnityPerMaterial)
float _ContactFoam;
CBUFFER_END
half4 frag(Vary i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float2 p=i.world.xz;float t=_Time.y;
 float2 drift=float2(t*.21,-t*.09);
 float swell=sin(dot(p,float2(.57,.86))+t*1.47);
 float crossSea=sin(dot(p,float2(-.91,.31))-t*1.1);
 float h=noise(p*.63+drift),chop=noise(p*1.88-drift+h);
 float channel=exp(-abs(p.x)*.30)*smoothstep(-30,-14,p.y)*(1-smoothstep(15,29,p.y));
 float3 water=lerp(float3(.003,.013,.062),float3(.018,.095,.255),saturate(.42+.23*swell+.17*crossSea+.23*h));
 water+=channel*float3(.002,.18,.31);
 float facet=sin(dot(p,float2(.55,2.08))+h*4.5+chop*2.8+t*1.31);
 float crest=smoothstep(.75,.96,facet)*smoothstep(.54,.89,chop)*(.38+.62*saturate(swell+crossSea));
 water+=crest*float3(.32,.56,.75);
 if(_ContactFoam>.5){
   float foam=(1-i.rim)*.9+(chop-.55)*1.4+.16*facet;
   clip(foam-.31);
   water=lerp(float3(.11,.48,.66),float3(.78,.95,1),smoothstep(.30,.76,foam));
 }
 return half4(water,1); }
ENDHLSL
} } }
