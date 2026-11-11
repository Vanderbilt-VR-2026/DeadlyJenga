Shader "Candy Tropical/Neon Outage"
{
 Properties
 {
  _BaseColor("Source color",Color)=(.5,.2,1,1)
  _BaseMap("Source map",2D)="white"{}
  _BumpMap("Source normals",2D)="bump"{}
  _BumpScale("Normal strength",Float)=0
  _Smoothness("Source gloss",Range(0,1))=.6
  _Metallic("Source metal",Range(0,1))=0
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
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   float4 _BaseMap_ST,_BumpMap_ST,_BoundsCenter,_BoundsSize;
   float _Filled,_Character,_Rim,_Smoothness,_Metallic,_BumpScale;
   UNITY_INSTANCING_BUFFER_START(Neon)
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
   UNITY_INSTANCING_BUFFER_END(Neon)
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 q:TEXCOORD1;half3 normal:TEXCOORD2;float2 uv:TEXCOORD3;half4 tangent:TEXCOORD4;float2 bumpUV:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
    o.q=(i.positionOS.xyz-_BoundsCenter.xyz)/max(.001,_BoundsSize.xyz*.5);
    o.normal=TransformObjectToWorldNormal(i.normalOS);
    o.tangent=half4(TransformObjectToWorldDir(i.tangentOS.xyz),i.tangentOS.w*GetOddNegativeScale());
    o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.bumpUV=TRANSFORM_TEX(i.uv,_BumpMap);return o;
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
    half3 n=normalize(i.normal);
    if(_BumpScale>.001 && dot(i.tangent.xyz,i.tangent.xyz)>.01)
    {
     half3 tangent=normalize(i.tangent.xyz);
     half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.bumpUV),_BumpScale);
     n=normalize(tangent*normalTS.x+cross(n,tangent)*i.tangent.w*normalTS.y+n*normalTS.z);
    }
    half3 view=normalize(_WorldSpaceCameraPos-i.world);
    half3 blue=half3(.42,.73,.54),pink=half3(-.69,.45,-.56);
    half key=saturate(dot(n,blue));
    half fill=saturate(dot(n,pink));
    half fresnel=pow(1-saturate(dot(n,view)),3);
    half gloss=lerp(12,84,_Smoothness);
    half blueGlint=pow(saturate(dot(n,normalize(blue+view))),gloss);
    half pinkGlint=pow(saturate(dot(n,normalize(pink+view))),gloss*.6);
    half3 night=half3(.022,.035,.075)+color*(.11+key*.21+fill*.11+_Character*.12);
    half3 reflection=lerp(half3(.7,.86,1),max(color,.14),_Metallic*.74);
    night+=reflection*(blueGlint*half3(.18,.48,.95)+pinkGlint*half3(.65,.13,.44))*(.35+_Smoothness*.65);
    half3 emitter=max(half3(.15,.28,.48),color);
    half pulse=.88+.12*sin(_Time.y*2.2+i.world.x*.12+i.world.z*.09);
    half trim=(broad*.16+core*.84)*nearby*lerp(.43,1.8,_Filled)*pulse;
    half3 result=night+emitter*(trim*(1-_Character)+fresnel*(.12+_Character*.28));
    float visibility=1-smoothstep(.85,2.35,distance(i.world,_WorldSpaceCameraPos));
    return half4(lerp(half3(.006,.004,.016),result,visibility),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
