Shader "Candy Tropical/Neon Outage"
{
 Properties
 {
  _BaseColor("Source color",Color)=(.5,.2,1,1)
  _BaseMap("Source map",2D)="white"{}
  _Filled("Selected block",Float)=0
  _Character("Rounded character",Float)=0
  _Rim("Rim visibility distance",Float)=9
  _BoundsCenter("Center",Vector)=(0,0,0,0)
  _BoundsSize("Size",Vector)=(1,1,1,0)
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   ZWrite On Cull Back
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   float4 _BaseMap_ST,_BoundsCenter,_BoundsSize;
   float _Filled,_Character,_Rim;
   UNITY_INSTANCING_BUFFER_START(Neon)
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
   UNITY_INSTANCING_BUFFER_END(Neon)
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 q:TEXCOORD1;half3 normal:TEXCOORD2;float2 uv:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
    o.q=(i.positionOS.xyz-_BoundsCenter.xyz)/max(.001,_BoundsSize.xyz*.5);
    o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*UNITY_ACCESS_INSTANCED_PROP(Neon,_BaseColor).rgb;
    float3 q=abs(i.q);float edge=max(min(q.x,q.y),max(min(q.y,q.z),min(q.x,q.z)));
    float aa=max(fwidth(edge),.002);
    float broad=smoothstep(.80-aa,.91+aa,edge);
    float core=smoothstep(.945-aa,.985+aa,edge);
    float reach=max(.1,_Rim);float nearby=1-smoothstep(reach*.35,reach,distance(i.world,_WorldSpaceCameraPos));
    half facing=saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
    half fresnel=(1-facing)*(1-facing)*(1-facing);
    half edgeGlow=(broad*.28+core*.55)*nearby;
    half body=.12+_Filled*.76;
    // Character colors remain identifiers; rounded silhouettes use a softer view rim.
    half gain=lerp(body+edgeGlow,.48+fresnel*.55,saturate(_Character));
    half3 result=color*gain+half3(.004,.008,.018)*(1-saturate(gain));
    return half4(result,1);
   }
   ENDHLSL
  }
 }
}
