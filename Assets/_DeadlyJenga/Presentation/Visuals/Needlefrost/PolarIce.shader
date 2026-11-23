Shader "DeadlyJenga/Needlefrost/Ice" {
Properties { _Tint("Ice colour", Color)=(.22,.55,.8,1)
_Grain("Glacier grain",2D)="white" {}
_Glow("Lamp",Float)=0
[HideInInspector] _Cull("Cull",Float)=2 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline"  }
Pass {
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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

TEXTURE2D(_Grain); SAMPLER(sampler_Grain);
CBUFFER_START(UnityPerMaterial)
float4 _Tint; float _Glow;
CBUFFER_END
float4 _NeedlefrostPracticals[4];
half4 frag(Vary i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float3 n=normalize(i.normal), v=normalize(GetWorldSpaceViewDir(i.world));
 float3 axis=abs(n);
 float2 uv=axis.y>max(axis.x,axis.z)?i.world.xz:(axis.x>axis.z?i.world.zy:i.world.xy);
 float3 strata=SAMPLE_TEXTURE2D(_Grain,sampler_Grain,uv*.32).rgb;
 float top=saturate((n.y-.08)*1.2);
 float key=saturate(dot(n,normalize(float3(-.55,.8,-.45))));
 float3 ice=lerp(strata*(.43+.64*key)*(.6+_Tint.rgb),float3(.65,.82,.98)*(.75+.33*strata.g),top*.85);
 float shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.world));
 ice*=lerp(float3(.35,.49,.68),float3(1,1,1),shadow);
 float rim=pow(1-saturate(dot(v,n)),3);
 ice+=rim*float3(.035,.13,.22);
 float channel=exp(-.34*abs(i.world.x))*exp(-.12*abs(i.world.y-5));
 ice+=channel*float3(0,.32,.58);
 [unroll] for(int j=0;j<4;j++){
   float3 delta=_NeedlefrostPracticals[j].xyz-i.world;
   float dist2=max(dot(delta,delta),.04);
   float lambert=.22+.78*saturate(dot(n,delta*rsqrt(dist2)));
   float attenuation=_NeedlefrostPracticals[j].w*lambert/(1+dist2*.30);
   ice+=attenuation*(j<2?float3(1.75,.56,.10):float3(.004,.94,1.55));
 }
 return half4(lerp(ice,float3(2.2,.72,.105)*(1+.36*strata.g),_Glow),1);
}
ENDHLSL
}
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
} }
