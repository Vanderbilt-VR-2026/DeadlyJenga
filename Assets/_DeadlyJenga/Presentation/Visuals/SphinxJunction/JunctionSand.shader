Shader "DeadlyJenga/Sphinx Junction/Dry flowing sand" {
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID}; struct V {float4 p:SV_POSITION;float2 x:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 w=TransformObjectToWorld(i.p.xyz);o.x=w.xz;o.p=TransformWorldToHClip(w);return o;}
 half4 frag(V i):SV_Target {
 float2 tile=floor(i.x/14);float2 p=frac(i.x/14)-.5; p+=.14*sin(tile.yx*3.9);
 float r=length(p), a=atan2(p.y,p.x); float whorl=cos(63*r+2*a+_Time.y*.72)*smoothstep(.46,.08,r);
 float dune=sin(i.x.x*1.33+.48*i.x.y+1.7*sin(i.x.y*.27)+.35*sin(_Time.y*.27+i.x.y*.13));
 float grain=frac(sin(dot(floor(i.x*57),float2(43.1,71.9)))*4371);
 return half4(lerp(float3(.51,.242,.079),float3(.93,.57,.228),saturate(.54+.18*dune+.24*whorl+.08*grain)),1);
 }
 ENDHLSL
 }}}
