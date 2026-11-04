Shader "Candy Tropical/Ocean Spirit"
{
 Properties
 {
  _BaseColor("Deep water",Color)=(.012,.12,.19,1)
  _FoamColor("Crest / caustic color",Color)=(.25,.94,1,1)
  _EmissionColor("Bioluminescence",Color)=(.02,.4,.55,1)
  _DetailScale("Water detail",Float)=1.4
  _PlateStrength("Scale plating",Range(0,1))=.45
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_FoamColor,_EmissionColor;float _DetailScale,_PlateStrength;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);
    o.uv=i.uv;o.positionCS=TransformWorldToHClip(o.world);return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 p=i.world*_DetailScale;float t=_Time.y;
    float3 flow=float3(sin(p.z*2.7+p.y*.7-t*1.7),sin(p.x*3.1+p.z*.9+t*.9),cos(p.y*2.5+p.x*.8-t*1.3));
    float veins=sin(p.x*1.7+flow.z)*sin(p.y*2.3+flow.x-t*.5)*sin(p.z*1.9+flow.y);
    float cells=pow(saturate(1-abs(veins)*8),5);
    float micro=sin(p.x*31+p.y*19)*sin(p.z*27-p.y*23+t*2);
    float2 tiles=i.uv*float2(21,15);
    tiles.x+=fmod(floor(tiles.y),2)*.5;
    float2 tile=frac(tiles)-float2(.5,.40);
    float rim=smoothstep(.24,.39,length(tile*float2(1,.73)))*(1-smoothstep(.40,.52,length(tile*float2(1,.73))));
    float inset=saturate(1-length(tile*float2(1,.75))*2.4);
    float3 n=normalize(i.normal+flow*.10+micro*.018);
    float3 v=GetWorldSpaceNormalizeViewDir(i.world);
    float fresnel=pow(1-saturate(dot(n,v)),4);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    float nl=saturate(dot(n,light.direction));
    float spec=pow(saturate(dot(n,normalize(v+light.direction))),160);
    float broader=pow(saturate(dot(n,normalize(v+light.direction))),28);
    float3 deep=_BaseColor.rgb*(.52+nl*.62)*(.8+veins*.2)*(1+_PlateStrength*(inset*.58-rim*.33));
    float3 color=deep*(SampleSH(n)+light.color*light.shadowAttenuation);
    color+=light.color*(spec*2.8+broader*.12)*light.shadowAttenuation;
    color+=_FoamColor.rgb*(fresnel*.64+cells*.23+rim*_PlateStrength*.12);
    color+=_EmissionColor.rgb*(cells*.65+fresnel*.22);
    #ifdef _ADDITIONAL_LIGHTS
     uint count=GetAdditionalLightsCount();
     for(uint j=0;j<min(count,4u);j++){Light l=GetAdditionalLight(j,i.world);color+=l.color*l.distanceAttenuation*(saturate(dot(n,l.direction))*deep+pow(saturate(dot(n,normalize(v+l.direction))),80)*.8);}
    #endif
    return half4(color,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}
