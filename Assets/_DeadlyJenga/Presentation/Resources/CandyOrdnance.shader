Shader "Candy/Ordnance Energy"
{
 Properties
 {
  [HDR] _BaseColor("Energy tint",Color)=(1,1,1,1)
  _Ribbon("Ribbon cross section",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Blend One OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;
   float _Ribbon;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   V vert(A i)
   {
    V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.color=i.color; o.uv=i.uv; return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float across=saturate(1-abs(i.uv.y*2-1));
    float radial=saturate(1-length(i.uv*2-1));
    float shape=_Ribbon>.5?across:radial;
    float filament=pow(shape,4);
    float grain=.82+.18*sin(i.uv.x*39-i.uv.y*15-_Time.y*27);
    float3 energy=_BaseColor.rgb*i.color.rgb;
    float alpha=saturate(shape*.93+filament*.25)*_BaseColor.a*i.color.a;
    float3 rgb=energy*(.61+filament*grain*.53)+lerp(energy,1,.85)*pow(shape,12)*1.05;
    return half4(rgb*alpha,alpha);
   }
   ENDHLSL
  }
 }
}
