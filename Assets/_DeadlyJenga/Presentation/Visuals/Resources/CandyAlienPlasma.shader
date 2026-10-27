Shader "DeadlyJenga/Alien Plasma"
{
 Properties { _BaseColor("Plasma tint",Color)=(0.12,0.9,1,0.65) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha One
   ZWrite Off
   Cull Back
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   CBUFFER_END
   struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO };
   V Vert(A a)
   {
    V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 p=a.positionOS.xyz;float wobble=sin(p.x*13+_Time.y*7)*sin(p.y*11-_Time.y*5)*sin(p.z*9+_Time.y*6);
    p*=1+wobble*.13;o.local=p;o.world=TransformObjectToWorld(p);o.normal=TransformObjectToWorldNormal(a.normalOS);o.positionCS=TransformWorldToHClip(o.world);return o;
   }
   half4 Frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 p=i.local*7;float cloud=sin(p.x+_Time.y*2)*sin(p.y*1.3-_Time.y*3)+sin(p.z*1.7+p.x*.4+_Time.y)*.5;
    float filaments=pow(saturate(1-abs(cloud)*2.8),4);
    float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world))))),2);
    float alpha=saturate(.12+rim*.55+filaments*.35)*_BaseColor.a;
    return half4(lerp(_BaseColor.rgb,float3(.7,1,1),filaments)*(.8+rim+filaments),alpha);
   }
   ENDHLSL
  }
 }
}
