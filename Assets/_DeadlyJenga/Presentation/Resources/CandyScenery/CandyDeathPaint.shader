Shader "Candy Tropical/Death Paint"
{
 Properties { [PerRendererData] _MainTex("Sprite Texture",2D)="white" {} _Age("Seconds since impact",Float)=0 _Aspect("View aspect",Float)=1.6 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest Always
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float _Age, _Aspect;
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i) {V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
   float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p) {float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);}
   float blob(float2 p,float2 center,float radius,float seed)
   {
    float2 q=p-center;float angle=atan2(q.y,q.x);
    float lobes=sin(angle*7+seed)*.10+sin(angle*13-seed)*.045+noise(q*28+seed)*.07;
    return radius*(1+lobes)-length(q);
   }
   float paint(float2 p)
   {
    float spread=lerp(.12,1,saturate(_Age/.16));
    float2 q=p/spread;
    q.y+=max(0,_Age-.18)*.028*(.3+noise(q.xx*17));
    float d=blob(q,float2(-.17,.06),.36,2);
    d=max(d,blob(q,float2(.23,-.14),.29,8));
    d=max(d,blob(q,float2(.31,.22),.21,15));
    d=max(d,blob(q,float2(-.41,-.21),.19,21));
    // Satellites, thrown paint fingers and slowly sagging drips.
    [unroll] for(int k=0;k<18;k++)
    {
     float a=k*2.39996;float2 v=float2(cos(a),sin(a));
     float r=.43+hash(float2(k,7))*.24;
     float2 c=v*r;c.y-=max(0,_Age-.3)*.025*hash(float2(k,12));
     float2 delta=q-c;delta.y/=1+max(0,_Age-.3)*1.4;
     d=max(d,(.012+hash(float2(k,3))*.027)-length(delta));
     float2 finger=q-v*.32;float along=dot(finger,v);float across=abs(dot(finger,float2(-v.y,v.x)));
     d=max(d,.026*(1-saturate(along/.24))-length(finger-v*clamp(along,0,.16)));
    }
    return d*spread;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=(i.uv-.5)*float2(_Aspect,1);
    float d=paint(p);float aa=max(fwidth(d),.001);
    float coverage=smoothstep(-aa,aa,d);
    // A curved squeegee passes left to right, leaving a short wet streak behind it.
    float wipe=saturate((_Age-1.15)/.95);
    float edge=lerp(-1.5,1.5,wipe)+.12*sin(i.uv.y*3.14159);
    float clear=smoothstep(edge-.025,edge+.025,p.x);
    if(_Age<1.15)clear=1;
    float grain=noise(p*170)*.035+noise(p*32)*.06;
    float rim=exp(-abs(d)*190);
    float2 grad=float2(ddx(d),ddy(d));
    float edgeLight=saturate(dot(normalize(float3(grad*100,1)),normalize(float3(-.6,.8,1))));
    float wet=pow(saturate(noise(p*12)*.7+noise(p*31)*.3),6)*.22;
    float edgeBand=exp(-abs(d)*95);
    float3 color=i.color.rgb*(.82+grain-edgeBand*.13)+wet+edgeBand*edgeLight*.3;
    float smear=exp(-abs(p.x-edge)*65)*coverage*.2;
    float alpha=coverage*clear*.97+smear*(1-wipe);
    alpha*=1-smoothstep(2.08,2.2,_Age);
    return half4(color,saturate(alpha)*i.color.a);
   }
   ENDHLSL
  }
 }
}
