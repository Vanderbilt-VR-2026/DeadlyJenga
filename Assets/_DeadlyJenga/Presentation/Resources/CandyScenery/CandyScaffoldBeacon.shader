Shader "Candy Tropical/Scaffold Beacon"
{
 Properties {_BaseColor("Beacon",Color)=(.18,.85,1,.5)}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _BaseColor;struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float flow=pow(.5+.5*sin(i.world.y*3-_Time.y*3.5+sin(i.world.x*.7+i.world.z)*.6),5);float rim=pow(1-abs(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world)))),2);return half4(_BaseColor.rgb*(.55+flow*2+rim),_BaseColor.a*(.12+flow*.5+rim*.2));}
 ENDHLSL }
 }
}
