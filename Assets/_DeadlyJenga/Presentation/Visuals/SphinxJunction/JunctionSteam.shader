Shader "DeadlyJenga/Sphinx Junction/Steam" {
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.c=i.c;return o;}
 half4 frag(V i):SV_Target {float2 p=i.uv*2-1;float r=length(p)+.055*sin(p.x*8+p.y*6)*cos(p.y*9);
 return half4(i.c.rgb,i.c.a*(1-smoothstep(.31,1,r)));}
 ENDHLSL
 }}}
