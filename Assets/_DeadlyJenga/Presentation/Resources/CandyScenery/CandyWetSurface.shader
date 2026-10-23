Shader "Candy Tropical/Wet Splash Coating"
{
 Properties {_BaseColor("Water stain",Color)=(.02,.12,.15,.35) _Wetness("Wetness",Range(0,1))=1}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Offset -1,-1
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   float4 _BaseColor;
   UNITY_INSTANCING_BUFFER_START(Water)
    UNITY_DEFINE_INSTANCED_PROP(float,_Wetness)
   UNITY_INSTANCING_BUFFER_END(Water)
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.world);return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float wet=UNITY_ACCESS_INSTANCED_PROP(Water,_Wetness);
    float3 p=i.world*7;float time=_Time.y;
    float runs=sin(p.x*2+sin(p.z*3))*sin(p.y*1.7+time*.8+p.z);
    float drops=pow(saturate(sin(p.x*6+p.z*3)*sin(p.y*7-time*2)),14);
    float3 n=normalize(i.normal+float3(cos(p.z+time)*.045,drops*.13,sin(p.x-time)*.045));
    float3 v=GetWorldSpaceNormalizeViewDir(i.world);Light light=GetMainLight();
    float spec=pow(saturate(dot(n,normalize(v+light.direction))),180);
    float rim=pow(1-saturate(dot(n,v)),4);
    float3 color=_BaseColor.rgb+float3(.23,.55,.65)*rim+light.color*spec*2.3+drops*.23;
    float stain=saturate(.55+runs*.35+drops*.6);
    return half4(color,wet*saturate(_BaseColor.a*stain+spec*.6+rim*.16));
   }
   ENDHLSL
  }
 }
}
