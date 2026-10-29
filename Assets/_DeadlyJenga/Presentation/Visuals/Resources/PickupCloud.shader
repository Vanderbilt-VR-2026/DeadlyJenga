Shader "DeadlyJenga/Pickup Cloud"
{
 Properties { _BaseColor("Tint",Color)=(.4,.8,1,.12) }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct V {float4 positionCS:SV_POSITION;float3 pos:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
 CBUFFER_START(UnityPerMaterial) float4 _BaseColor; CBUFFER_END
 V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=TransformObjectToWorld(v.positionOS.xyz);o.normal=TransformObjectToWorldNormal(v.normalOS);o.positionCS=TransformWorldToHClip(o.pos);return o;}
 half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float n=frac(sin(dot(floor(i.pos*110+_Time.y*5),float3(12.3,83.7,41.9)))*43758.5);float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.pos)))),2);return half4(_BaseColor.rgb*(.7+n*.5+rim),_BaseColor.a*(.3+n+rim*2));}
 ENDHLSL
 } }
}
