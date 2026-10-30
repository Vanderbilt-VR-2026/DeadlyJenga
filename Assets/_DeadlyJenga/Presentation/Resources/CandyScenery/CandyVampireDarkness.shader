Shader "Candy Tropical/Vampire Darkness"
{
 Properties {_Darkness("Darkness",Range(0,1))=1}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+200" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off ZTest LEqual
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _Darkness;struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(.0004,.0005,.0008,_Darkness);}
 ENDHLSL }
 }
}
