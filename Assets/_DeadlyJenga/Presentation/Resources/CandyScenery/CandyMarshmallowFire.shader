Shader "DeadlyJenga/Marshmallow Fire"
{
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
  Blend One OneMinusSrcAlpha ZWrite Off Cull Back
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 origin:TEXCOORD1;float scale:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float noise(float3 p)
   {
    float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
   }
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.origin=TransformObjectToWorld(float3(0,0,0));o.scale=length(TransformObjectToWorldDir(float3(1,0,0),false));
    o.world=o.origin+i.positionOS.xyz*o.scale;o.positionCS=TransformWorldToHClip(o.world);return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 ro=(GetCameraPositionWS()-i.origin)/max(i.scale,.0001),rd=normalize(i.world-GetCameraPositionWS());
    float3 inv=rcp(rd+float3(.00001,.00001,.00001));
    float3 ta=(float3(-.14,-.07,-.14)-ro)*inv,tb=(float3(.14,.37,.14)-ro)*inv;
    float3 nearPlane=min(ta,tb),farPlane=max(ta,tb);float begin=max(0,max(nearPlane.x,max(nearPlane.y,nearPlane.z))),end=min(farPlane.x,min(farPlane.y,farPlane.z));
    if(end<=begin)return 0;
    float stepSize=(end-begin)/14;float4 result=0;float time=_Time.y;
    [unroll]for(int s=0;s<14;s++)
    {
     float3 p=ro+rd*(begin+(s+.5)*stepSize);float y=saturate((p.y+.045)/.405);
     float3 q=p*float3(28,15,28)-float3(time*.8,time*4.2,time*.36);
     float n=noise(q)*.58+noise(q*2.07+7.1)*.29+noise(q*4.13-3.7)*.13;
     float2 swirl=float2(sin(y*9-time*4),cos(y*7-time*3.1))*.024*y;
     float radius=.095*pow(saturate(1-y),.6)+.008;
     float envelope=saturate(1-length(p.xz+swirl)/radius);
     float tongues=saturate((n-.30-y*.22)*5);
     float density=envelope*envelope*tongues*smoothstep(0,.14,y)*(1-smoothstep(.82,1,y));
     float opacity=1-exp(-density*stepSize*32);
     float heat=saturate((1-y)*.7+n*.5+envelope*.15);
     float3 emission=lerp(float3(1.5,.12,.009),float3(2.6,.85,.095),smoothstep(.28,.68,heat));
     emission=lerp(emission,float3(3.3,2.25,.95),smoothstep(.73,1,heat));
     result.rgb+=(1-result.a)*emission*opacity;result.a+=(1-result.a)*opacity;
    }
    return half4(result.rgb*_BaseColor.rgb,result.a*_BaseColor.a);
   }
   ENDHLSL
  }
 }
}
