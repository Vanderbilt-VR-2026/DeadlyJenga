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
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(i.positionOS.xyz);
    float sway=sin(o.world.x*5+o.world.z*4+_Time.y*8)*.045;
    float weight=_Tongue>.5?i.uv.y:1;
    o.world.x+=sway*weight;o.world.y+=sin(o.world.z*6+_Time.y*11)*.06*weight;
    o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float t=_Time.y;
    float2 p=float2(i.world.x+i.world.z*.73,i.world.y-t*2.4);
    if(_Tongue>.5)p=float2(i.uv.x*2+i.world.x*.17,i.uv.y*2.3-t*2.2);
    float n=noise(p*3.5)+.45*noise(p*8+float2(t*.5,0));
    float core=saturate((n-.25)*1.4);
    float3 color=lerp(float3(1.35,.07,.004),float3(1.9,.95,.09),core);
    color=lerp(color,float3(2.1,1.5,.4),pow(core,7)*.3);
    float alpha=.22+.4*core;
    if(_Tongue>.5)
    {
     float y=i.uv.y,x=(i.uv.x-.5)+sin(y*9-t*7+i.world.z)*y*.12;
     float width=lerp(.49,.025,y);
     float edge=1-smoothstep(width*.35,width,abs(x)+(noise(p*6)-.5)*.16*y);
     alpha=edge*(1-smoothstep(.65,1,y+n*.08))*.92;
     color=lerp(float3(1.7,.12,.008),color,saturate(1-y));
    }
    return half4(color,alpha*_BaseColor.a);
   }
   ENDHLSL
  }
 }
}
