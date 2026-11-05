Shader "Candy Tropical/Charred Block"
{
 Properties {_BaseColor("Original candy",Color)=(1,.2,.1,1) _BurnAmount("Charring",Range(0,1))=1 _Heat("Embers",Range(0,1))=0}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
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
   UNITY_INSTANCING_BUFFER_START(Burn)
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float,_BurnAmount)
    UNITY_DEFINE_INSTANCED_PROP(float,_Heat)
   UNITY_INSTANCING_BUFFER_END(Burn)
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.local=i.positionOS.xyz;o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.world);return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float burn=UNITY_ACCESS_INSTANCED_PROP(Burn,_BurnAmount),heat=UNITY_ACCESS_INSTANCED_PROP(Burn,_Heat);
    float3 original=UNITY_ACCESS_INSTANCED_PROP(Burn,_BaseColor).rgb;
    float plate=.5+.5*sin(i.local.x*4.3+sin(i.local.z*3.4))*sin(i.local.y*5.2-i.local.z*2.1);
    float3 pattern=i.local*6+sin(i.local.zxy*4)*.16;
    float3 cell=abs(frac(pattern)-.5);
    float seam=min(cell.x,min(cell.y,cell.z));
    float aa=max(fwidth(seam),.004);
    float footprint=max(fwidth(pattern.x),max(fwidth(pattern.y),fwidth(pattern.z)));
    float crack=(1-smoothstep(max(0,.028-aa),.028+aa,seam))*(1-smoothstep(.08,.24,footprint));
    float charMix=smoothstep(plate*.16,.70+plate*.16,saturate(burn));
    float3 charcoal=lerp(float3(.040,.035,.045),float3(.12,.105,.115),plate)*(1-crack*.35);
    float3 albedo=lerp(original,charcoal,charMix);
    float3 normal=normalize(i.normal),view=normalize(GetWorldSpaceViewDir(i.world));
    Light main=GetMainLight(TransformWorldToShadowCoord(i.world));
    float3 lit=albedo*(max(SampleSH(normal),.08)+main.color*saturate(dot(normal,main.direction))*main.shadowAttenuation);
    float spec=pow(saturate(dot(normal,normalize(main.direction+view))),lerp(100,36,charMix));
    lit+=main.color*spec*lerp(.35,.16,charMix)*saturate(dot(normal,main.direction))*main.shadowAttenuation;
    lit+=crack*heat*charMix*float3(.65,.065,.003)*(.8+.2*sin(_Time.y*3+i.local.x*7));
    return half4(lit,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
