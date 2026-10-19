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
   float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.local=i.positionOS.xyz;o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.world);return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float burn=UNITY_ACCESS_INSTANCED_PROP(Burn,_BurnAmount),heat=UNITY_ACCESS_INSTANCED_PROP(Burn,_Heat);
    float3 original=UNITY_ACCESS_INSTANCED_PROP(Burn,_BaseColor).rgb;
    float grain=hash(floor(i.local*210));
    float3 cell=abs(frac(i.local*23+sin(i.local.zxy*31)*.17)-.5);
    float crack=1-smoothstep(.015,.055,min(cell.x,min(cell.y,cell.z)));
    float3 charcoal=lerp(float3(.018,.012,.01),float3(.075,.06,.045),grain)*(1-crack*.65);
    float3 albedo=lerp(original,charcoal,burn);
    float3 normal=normalize(i.normal),view=normalize(GetWorldSpaceViewDir(i.world));
    Light main=GetMainLight(TransformWorldToShadowCoord(i.world));
    float3 lit=albedo*(max(SampleSH(normal),.08)+main.color*saturate(dot(normal,main.direction))*main.shadowAttenuation);
    float spec=pow(saturate(dot(normal,normalize(main.direction+view))),lerp(100,28,burn));
    lit+=main.color*spec*lerp(.35,.1,burn)*main.shadowAttenuation;
    lit+=crack*heat*burn*float3(.55,.045,.002)*(.7+.3*sin(_Time.y*3+i.local.x*19));
    return half4(lit,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
