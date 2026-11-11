Shader "Candy Tropical/Charred Block"
{
 Properties
 {
  _BaseColor("Original candy",Color)=(1,.2,.1,1)
  _BurnAmount("Charring",Range(0,1))=1
  _Heat("Embers",Range(0,1))=0
  [HideInInspector] _BurnHeight("Local height range",Vector)=(-.5,1,0,0)
 }
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
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   UNITY_INSTANCING_BUFFER_START(Burn)
    UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float,_BurnAmount)
    UNITY_DEFINE_INSTANCED_PROP(float,_Heat)
    UNITY_DEFINE_INSTANCED_PROP(float4,_BurnHeight)
   UNITY_INSTANCING_BUFFER_END(Burn)
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;float fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.local=i.positionOS.xyz;o.world=TransformObjectToWorld(o.local);o.normal=TransformObjectToWorldNormal(i.normalOS);
    o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
   }
   float OrganicField(float3 p)
   {
    // Crossing, warped waves break the blisters into irregular islands.
    float3 warp=sin(p.zxy*1.28+sin(p.yzx*.82))*1.18;
    float3 q=p+warp;
    return dot(sin(q)+.38*sin(q.yzx*2.4+q*.79),float3(.235,.19,.22));
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float burn=UNITY_ACCESS_INSTANCED_PROP(Burn,_BurnAmount),heat=UNITY_ACCESS_INSTANCED_PROP(Burn,_Heat);
    float2 range=UNITY_ACCESS_INSTANCED_PROP(Burn,_BurnHeight).xy;
    float3 original=UNITY_ACCESS_INSTANCED_PROP(Burn,_BaseColor).rgb;
    float y=saturate((i.local.y-range.x)*range.y);
    float3 p=i.local*25;
    float footprint=max(length(ddx(p)),length(ddy(p)));
    float detail=1-smoothstep(.16,.85,footprint);
    float field=OrganicField(p)+.36*OrganicField(p.zxy*2.17+5.1);
    float pores=OrganicField(p*4.6)*detail;
    float crust=field+.20*pores;
    float edgeAA=max(.025,fwidth(crust));
    float blister=smoothstep(-.04-edgeAA,.055+edgeAA,crust);
    float rim=(1-smoothstep(.025,.13+edgeAA,abs(crust)))*detail;
    float live=smoothstep(.30,.95,heat);
    float exposed=smoothstep(-edgeAA,.13+edgeAA,field+burn*1.60-.54);
    float darkening=smoothstep(.27,.93,burn);
    float3 caramel=lerp(float3(.105,.035,.013),float3(.44,.18,.035),blister*live+.10);
    float3 charcoal=lerp(float3(.044,.042,.047),float3(.088,.046,.027),blister);
    charcoal*=.94+.24*pores;
    float3 albedo=lerp(original*.76,lerp(caramel,charcoal,darkening),exposed);

    float3 normal=normalize(i.normal);
    float3 dx=ddx(i.world),dy=ddy(i.world);
    float height=(blister*.027+pores*.014)*exposed*detail;
    float3 rx=cross(dy,normal),ry=cross(normal,dx);
    float det=dot(dx,rx);
    float3 gradient=sign(det)*(rx*ddx(height)+ry*ddy(height));
    normal=normalize(normal-gradient/max(abs(det),1e-6));
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    float diffuse=saturate(dot(normal,sun.direction))*sun.shadowAttenuation;
    float3 lit=albedo*(max(SampleSH(normal),.09)+sun.color*diffuse);
    float3 view=GetWorldSpaceNormalizeViewDir(i.world);
    float spec=pow(saturate(dot(normal,normalize(sun.direction+view))),lerp(86,38,exposed));
    lit+=sun.color*spec*lerp(.34,.24+rim*.15,exposed)*diffuse;
    float glaze=pow(saturate(dot(normal,normalize(view+float3(.31,1,.45)))),21);
    lit+=float3(.21,.17,.14)*glaze*exposed*(.18+rim*.58);

    float lower=1-smoothstep(.020,.092,y);
    float upper=smoothstep(.92,.995,y)*live*.55;
    float coal=(lower+upper)*smoothstep(-.28,.42,field+pores*.50);
    float glow=(coal*(.44+.74*rim)+rim*live*.095)*heat;
    glow*=.85+.15*sin(_Time.y*3.1+i.local.x*8.6);
    lit+=float3(2.5,.34,.022)*glow;
    return half4(MixFog(lit,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
