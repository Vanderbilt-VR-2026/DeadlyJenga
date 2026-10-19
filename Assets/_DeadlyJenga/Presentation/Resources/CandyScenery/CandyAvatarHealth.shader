Shader "Candy Tropical/Avatar Health"
{
 Properties {_BaseColor("Candy color",Color)=(1,.2,.1,1) _Smoothness("Gloss",Float)=.8 _Health("Health",Float)=1 _Bottom("Feet",Float)=0 _Top("Head",Float)=2 _MeltStart("Melt start",Float)=-10000 _Sink("Sink progress",Float)=0 _Recovering("Recovering",Float)=0}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {Tags {"LightMode"="UniversalForward"} HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _Smoothness,_Health,_Bottom,_Top,_MeltStart,_Sink,_Recovering;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float melt:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 p=TransformObjectToWorld(i.positionOS.xyz);float age=_Time.y-_MeltStart;float melt=_MeltStart<0?0:saturate(age/.65);float3 center=TransformObjectToWorld(float3(0,0,0));p.xz=center.xz+(p.xz-center.xz)*(1+melt*.65);p.y=lerp(p.y,_Bottom+.018,melt)-saturate(_Sink)*max(.1,_Top-_Bottom)*.8;o.world=p;o.positionCS=TransformWorldToHClip(p);o.normal=normalize(lerp(TransformObjectToWorldNormal(i.normalOS),float3(0,1,0),melt));o.melt=_MeltStart<0?0:saturate((age-.75)/.7);return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float organic=noise(i.world.xz*4)*.7+noise(i.world.xz*9+3)*.3;clip(organic-i.melt);float level=(_Top-_Bottom)*_Health+_Bottom;float h=_Health>=.9999?0:(_Health<=.0001?1:smoothstep(level-.015,level+.015,i.world.y));float3 c=lerp(_BaseColor.rgb,lerp(_BaseColor.rgb,float3(1,1,1),.48),h);Light light=GetMainLight(TransformWorldToShadowCoord(i.world));float3 n=normalize(i.normal),v=normalize(_WorldSpaceCameraPos-i.world);float spec=pow(saturate(dot(n,normalize(light.direction+v))),lerp(24,180,_Smoothness));float width=max(.012,(_Top-_Bottom)*.006);float rim=(1-smoothstep(width*.35,width,abs(i.world.y-level)))*step(.001,_Health)*(1-step(.999,_Health));float3 bright=_BaseColor.rgb/max(.12,max(_BaseColor.r,max(_BaseColor.g,_BaseColor.b)));float pulse=1+_Recovering*.2*sin(_Time.y*6);return half4(c*(SampleSH(n)+light.color*saturate(dot(n,light.direction))*light.shadowAttenuation)+light.color*spec*.7+bright*rim*pulse,1);}
 ENDHLSL }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}
