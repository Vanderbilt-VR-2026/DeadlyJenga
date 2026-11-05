Shader "Candy Tropical/Engulfing Block Fire"
{
 Properties {_BaseColor("Tint",Color)=(1,.45,.04,1) _Tongue("Flame tongue",Float)=0 _Cull("Cull",Float)=2}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull [_Cull]
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _BaseColor;float _Tongue;
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);
    float weight=_Tongue>.5?i.uv.y:1;
    o.world.x+=sin(o.world.x*5+o.world.z*4+_Time.y*8)*.045*weight;
    o.world.y+=sin(o.world.z*6+_Time.y*11)*.06*weight;
    o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float t=_Time.y;
    float2 p=float2(i.world.x+i.world.z*.73,i.world.y-t*2.4);
    float n=noise(p*3.5)+.45*noise(p*8+float2(t*.5,0));
    float core=saturate((n-.25)*1.4);
    float3 color=lerp(float3(1.25,.10,.008),float3(1.85,1.05,.17),core);
    float alpha=smoothstep(.72,1.05,n)*(.06+.14*core);
    if(_Tongue>.5)
    {
     float y=saturate(i.uv.y);
     float facing=saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
     float heat=saturate((1-y)*.8+core*.35);
     color=lerp(float3(1.25,.09,.006),float3(1.95,1.25,.28),heat);
     alpha=(.55+.28*core)*(1-smoothstep(.75,1,y))*smoothstep(.02,.35,facing);
    }
    return half4(color,alpha*_BaseColor.a);
   }
   ENDHLSL
  }
 }
}
