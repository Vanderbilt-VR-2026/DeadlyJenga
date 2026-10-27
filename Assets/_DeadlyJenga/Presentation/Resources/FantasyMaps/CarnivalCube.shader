Shader "DeadlyJenga/Carnival Cube Sky"
{
 Properties {
  _Front("Front",2D)="white" {} _Right("Right",2D)="white" {} _Back("Back",2D)="white" {}
  _Left("Left",2D)="white" {} _Up("Up",2D)="white" {} _Down("Down",2D)="white" {}
  _Exposure("Exposure",Range(.25,2))=1 _Rotation("Rotation",Range(0,360))=0
 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma target 3.5
   #pragma multi_compile_instancing
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_Front);TEXTURE2D(_Right);TEXTURE2D(_Back);TEXTURE2D(_Left);TEXTURE2D(_Up);TEXTURE2D(_Down);
   SAMPLER(sampler_Front);
   CBUFFER_START(UnityPerMaterial)
   float _Exposure,_Rotation;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.direction=i.positionOS.xyz;return o;}
   float2 FaceUV(float2 uv){return .5+atan((uv-.5)*3)/3.14159265359;}
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 d=normalize(i.direction);float s,c;sincos(_Rotation*.01745329252,s,c);d.xz=mul(float2x2(c,-s,s,c),d.xz);
    float3 a=max(abs(d),.00001);float3 w=pow(a/max(a.x,max(a.y,a.z)),12);w/=w.x+w.y+w.z;
    float3 color=0;
    if(w.x>.0001){float2 uv=FaceUV(float2(d.x>0?-d.z:d.z,d.y)/a.x*.5+.5);color+=(d.x>0?SAMPLE_TEXTURE2D(_Right,sampler_Front,uv).rgb:SAMPLE_TEXTURE2D(_Left,sampler_Front,uv).rgb)*w.x;}
    if(w.y>.0001){float2 uv=FaceUV(float2(d.x,d.y>0?-d.z:d.z)/a.y*.5+.5);color+=(d.y>0?SAMPLE_TEXTURE2D(_Up,sampler_Front,uv).rgb:SAMPLE_TEXTURE2D(_Down,sampler_Front,uv).rgb)*w.y;}
    if(w.z>.0001){float2 uv=FaceUV(float2(d.z>0?d.x:-d.x,d.y)/a.z*.5+.5);color+=(d.z>0?SAMPLE_TEXTURE2D(_Front,sampler_Front,uv).rgb:SAMPLE_TEXTURE2D(_Back,sampler_Front,uv).rgb)*w.z;}
    return half4(color*_Exposure,1);
   }
   ENDHLSL
  }
 }
}
