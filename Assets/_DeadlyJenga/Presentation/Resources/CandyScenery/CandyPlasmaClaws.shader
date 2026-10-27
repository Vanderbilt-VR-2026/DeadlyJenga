Shader "DeadlyJenga/Gleaming Plasma Claws"
{
 Properties { _BaseColor("Plasma tint",Color)=(.1,.9,1,1) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float3 axisWS:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.axisWS=TransformObjectToWorldDir(float3(0,0,1));o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS),axis=normalize(i.axisWS);
    float3 across=view-axis*dot(view,axis);float facing=saturate(dot(normalize(i.normalWS),normalize(across+float3(.00001,0,0))));
    float flow=.92+.08*sin(i.uv.y*70-_Time.y*19+sin(i.uv.x*18+i.uv.y*23));
    float core=smoothstep(.87,.96,facing),arc=pow(saturate(sin(i.uv.y*145-_Time.y*30+i.uv.x*8)),28)*.16;
    float3 face=float3(.32,.80,1.3)*(.75+.25*facing);
    float3 color=lerp(face,float3(3.6,4.2,4.5),core)*flow+arc*core;
    return half4(color,.97);
   }
   ENDHLSL
  }
 }
}
