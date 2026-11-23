Shader "DeadlyJenga/Sphinx Junction/Dry flowing sand" {
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A {float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID}; struct V {float4 p:SV_POSITION;float2 x:TEXCOORD0;float h:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 w=TransformObjectToWorld(i.p.xyz);o.x=w.xz;o.h=w.y;o.p=TransformWorldToHClip(w);return o;}
 half4 frag(V i):SV_Target {
 float2 uv=i.x/13+float2(.11*sin(i.x.y*.17),.12*sin(i.x.x*.14));
 float2 tile=floor(uv),p=frac(uv)-.5;
 float seed=frac(sin(dot(tile,float2(35.1,77.3)))*31247);
 p+=.16*sin(tile.yx*3.9+seed*5);
 float r=length(p),a=atan2(p.y,p.x);
 float whorl=cos((65+seed*31)*r+2*a+_Time.y*.72)*(1-smoothstep(.07,.36,r))*smoothstep(.2,.75,seed);
 float dune=sin(i.x.x*.83+.38*i.x.y+3.2*sin(i.x.y*.24)+.35*sin(_Time.y*.27+i.x.y*.13));
 float grain=frac(sin(dot(floor(i.x*57),float2(43.1,71.9)))*4371);
 Light sun=GetMainLight(TransformWorldToShadowCoord(float3(i.x.x,i.h,i.x.y)));
 return half4(lerp(.7,1,sun.shadowAttenuation)*lerp(float3(.51,.242,.079),float3(.93,.57,.228),saturate(.54+.18*dune+.24*whorl+.08*grain)),1);
 }
 ENDHLSL
 }}}
