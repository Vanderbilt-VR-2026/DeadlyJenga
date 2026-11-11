Shader "Candy Tropical/Engulfing Block Fire"
{
 Properties {_BaseColor("Tint",Color)=(1,.45,.04,1) _Tongue("Flame tongue",Float)=0 _Cull("Cull",Float)=2}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
  Blend One OneMinusSrcAlpha ZWrite Off Cull [_Cull]
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _BaseColor;
   float _Tongue;
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 local:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   float Hash(float2 p)
   {
    float3 q=frac(float3(p.x,p.y,p.x)*.1031);
    q+=dot(q,q.yzx+33.33);
    return frac((q.x+q.y)*q.z);
   }
   float Noise(float2 p)
   {
    float2 a=floor(p),f=frac(p);
    f=f*f*(3-2*f);
    return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);
   }
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.local=i.positionOS.xyz;
    o.world=TransformObjectToWorld(o.local);
    if(_Tongue>.5)
    {
     float height=i.uv.y;
     float phase=dot(TransformObjectToWorld(float3(0,0,0)).xz,float2(1.8,2.3))+_Time.y*4-height*4;
     o.world.xz+=float2(sin(phase),cos(phase*.83))*(height*height*.23);
    }
    o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float t=_Time.y;
    float2 flow=float2(dot(i.world.xz,float2(.76,.65)),i.world.y-t*1.8);
    float n=Noise(flow*4)+.35*Noise(flow*9);
    if(_Tongue<.5)
    {
     // The shell connects the tongues along the bevels while the face remains readable.
     float lip=smoothstep(.40,.496,abs(i.local.y));
     float band=smoothstep(.37,.90,n);
     float alpha=(.01+.64*lip)*band*_BaseColor.a;
     float3 edgeGlow=lerp(float3(1.12,.055,.002),float3(2.2,.70,.045),lip*band);
     return half4(edgeGlow*alpha,alpha);
    }
    float height=saturate(i.uv.y);
    float x=i.uv.x*2-1;
    float tail=1-smoothstep(.63,.98,height+n*.08);
    float bend=(Noise(float2(flow.x*2,height*4-t*2))-.5)*.52*height;
    float edge=1-smoothstep(.29,.98,abs(x-bend));
    float ribbon=.49+.51*sin(x*6+height*12+n*4-t*4.8);
    float filament=smoothstep(.29,.76,.61*n+.46*ribbon);
    float heat=saturate((1-height)*.35+filament*.64);
    float3 color=lerp(float3(1.45,.15,.009),float3(2.40,.82,.075),heat);
    float alpha=tail*edge*(.18+.68*filament)*_BaseColor.a;
    return half4(color*alpha,alpha);
   }
   ENDHLSL
  }
 }
}
