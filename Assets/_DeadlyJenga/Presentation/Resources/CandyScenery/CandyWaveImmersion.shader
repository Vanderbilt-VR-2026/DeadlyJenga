Shader "Candy Tropical/Wave Immersion"
{
 Properties { _WaterWash("Immersion, entry foam, time", Vector)=(0,0,0,0) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+75"}
  Pass
  {
   Cull Off ZTest Always ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _WaterWash;
   struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=float4(i.positionOS.xy,.5,1);o.uv=i.positionOS.xy*.5+.5;return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv-.5;
    float edge=smoothstep(.16,.7,length(p));
    float foam=.62+.38*sin(i.uv.x*17+i.uv.y*12+_WaterWash.z*6);
    float white=_WaterWash.y*foam;
    float2 q=float2(i.uv.x,frac(i.uv.y-_WaterWash.z*.16));
    float bubble=1-smoothstep(.002,.006,abs(length((q-float2(.17,.31))*float2(1.3,1))-.023));
    bubble+=1-smoothstep(.002,.006,abs(length((q-float2(.83,.62))*float2(1.3,1))-.016));
    white+=bubble*_WaterWash.x*.075;
    float tint=_WaterWash.x*(.18+.19*edge);
    return half4(lerp(float3(.025,.37,.43),float3(.79,.95,.94),white/max(.001,white+tint)),saturate(white+tint));
   }
   ENDHLSL
  }
 }
}
