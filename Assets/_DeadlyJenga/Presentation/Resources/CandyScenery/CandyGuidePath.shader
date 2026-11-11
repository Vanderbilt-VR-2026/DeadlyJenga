Shader "DeadlyJenga/Guide Path"
{
 // Floor chevrons leading new players to the play screen. A bright band travels forward along the path.
 // uv.x = metres along the path. One mesh, one draw call, animated on the GPU.
 Properties
 {
  _Color("Color",Color)=(.0,.55,.45,1)
  _Speed("Travel speed",Float)=3
  _Spacing("Band spacing",Float)=6
  _Fade("Fade",Range(0,1))=1
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass
  {
   Name "Guide path"
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;
   float _Speed,_Spacing,_Fade;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float wave=frac((i.uv.x-_Time.y*_Speed)/_Spacing);
    float band=saturate(1-(1-wave)*3.5);     // sharp front edge, soft tail behind it
    half alpha=saturate((.88+.12*band)*_Fade);
    return half4(lerp(_Color.rgb*(.75+.25*i.color.r),half3(.55,1,.85),band*.7),alpha);
   }
   ENDHLSL
  }
 }
}
