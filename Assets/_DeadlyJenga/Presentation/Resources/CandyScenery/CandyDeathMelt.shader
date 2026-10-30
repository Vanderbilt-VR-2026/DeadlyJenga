Shader "Candy Tropical/Slow Liquid Death"
{
 Properties
 {
  _BaseColor("Candy color",Color)=(1,.2,.1,1)
  _BaseMap("Avatar texture",2D)="white" {}
  _PigmentColor("Molten pigment",Color)=(1,.2,.1,1)
  _MeltCenter("Puddle center",Vector)=(0,0,0,0)
  _BodyHeight("Body height",Float)=1
  _MeltProgress("Liquefying",Range(0,1))=0
  _DryProgress("Drying",Range(0,1))=0
  _PuddleMesh("Puddle mesh",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor, _MeltCenter, _BaseMap_ST, _PigmentColor;
   float _BodyHeight, _MeltProgress, _DryProgress, _PuddleMesh;
   CBUFFER_END
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings vert(Attributes input)
   {
    Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 p=TransformObjectToWorld(input.positionOS.xyz);
    float3 n=TransformObjectToWorldNormal(input.normalOS);
    if(_PuddleMesh<.5)
    {
     float melt=saturate(_MeltProgress);
     float h=max(0,p.y-_MeltCenter.y);
     float normalizedHeight=saturate(h/max(.1,_BodyHeight));
     // Feet soften first. The remaining body keeps its full width while flowing down.
     float collapse=pow(1-melt,lerp(2.2,1.3,normalizedHeight));
     float2 radial=p.xz-_MeltCenter.xz;
     p.xz=_MeltCenter.xz+radial*(1+melt*.35);
     p.xz+=float2(sin(normalizedHeight*8+radial.y*4),cos(normalizedHeight*7+radial.x*4))*sin(melt*PI)*_BodyHeight*.025;
     p.y=lerp(p.y,_MeltCenter.y+.012+h*collapse,melt);
     n=normalize(lerp(n,float3(0,1,0),melt*.98));
    }
    output.world=p;output.normal=n;output.uv=input.uv*_BaseMap_ST.xy+_BaseMap_ST.zw;output.positionCS=TransformWorldToHClip(p);return output;
   }
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
   half4 frag(Varyings input):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float dry=saturate(_DryProgress);
    float2 radial=(input.world.xz-_MeltCenter.xz)/max(.1,_BodyHeight);
    float edge=length(radial*float2(1/ .72,1/ .59));
    float grain=noise(radial*23)*.09;
    // The wet rim retreats inward before the last tiny islands evaporate.
    if(dry>.0001)clip(1.12-dry*1.25-edge+grain);
    float3 n=normalize(input.normal),v=SafeNormalize(_WorldSpaceCameraPos-input.world);
    Light light=GetMainLight(TransformWorldToShadowCoord(input.world));
    float spec=pow(saturate(dot(n,SafeNormalize(light.direction+v))),lerp(150,24,dry));
    float fresnel=pow(1-saturate(dot(n,v)),4);
    // Fur markings remain intact during the fall, then dissolve into molten pigment.
    float3 pattern=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
    float3 pigment=lerp(_BaseColor.rgb*pattern,_PigmentColor.rgb,smoothstep(.1,.9,_MeltProgress));
    float3 color=lerp(pigment,pigment*.65+float3(.2,.2,.2),dry*.5);
    float3 lit=color*(SampleSH(n)+light.color*saturate(dot(n,light.direction))*light.shadowAttenuation);
    lit+=light.color*spec*(1-dry)*.9+fresnel*color*.18*(1-dry);
    return half4(lit,1);
   }
   ENDHLSL
  }
 }
}
