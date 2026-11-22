Shader "DeadlyJenga/Needlefrost/Ice" {
Properties { _Tint("Ice colour", Color)=(.22,.55,.8,1)
_Glow("Lamp",Float)=0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline"  }
Pass {
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

CBUFFER_START(UnityPerMaterial)
float4 _Tint; float _Glow;
CBUFFER_END
half4 frag(Vary i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float3 n=normalize(i.normal), v=normalize(GetWorldSpaceViewDir(i.world));
 float2 p=i.world.xz*.65+i.world.y*float2(.48,.26);
 float vein=noise(p*2+noise(p*.9)*2), grain=noise(p*17);
 float frost=saturate((n.y+.16)*.82 + vein*.28);
 float crack=1-smoothstep(.012,.055,abs(vein-.49));
 float3 ice=lerp(_Tint.rgb*float3(.38,.62,.85),float3(.68,.86,.99),frost*.84);
 float key=saturate(dot(n,normalize(float3(-.55,.8,-.45))));
 ice*=.68+.58*key+.11*grain;
 ice+=crack*.09+pow(1-saturate(dot(v,n)),3)*float3(.15,.26,.35);
 float pool=exp(-.16*abs(i.world.x))*saturate(1-i.world.y*.08);
 ice+=pool*float3(0,.25,.41);
 return half4(lerp(ice,float3(1.65,.74,.16)*(1+.25*grain),_Glow),1);
}
ENDHLSL
} } }
