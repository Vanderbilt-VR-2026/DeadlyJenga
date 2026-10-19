Shader "Candy Tropical/Meteor Stone"
{
 Properties {_BaseColor("Stone",Color)=(.13,.085,.065,1) _Heat("Heat",Range(0,1))=1}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _BaseColor;float _Heat;
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.p=i.positionOS.xyz;o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float n=noise(i.p*18)+noise(i.p*47)*.25;float fissure=1-smoothstep(.025,.075,abs(n-.58));float light=.2+.8*saturate(dot(normalize(i.normal),normalize(float3(-.3,.8,-.5))));float3 stone=_BaseColor.rgb*light*(.65+n*.75);return half4(stone+fissure*_Heat*float3(2.3,.38,.025),1);}
 ENDHLSL }
 }
}
