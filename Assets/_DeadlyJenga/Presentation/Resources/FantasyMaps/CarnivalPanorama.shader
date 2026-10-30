Shader "DeadlyJenga/Carnival Panorama"
{
 Properties { _MainTex("360 degree panorama",2D)="white" {} _Exposure("Exposure",Range(.25,2))=1 _Rotation("Rotation",Range(0,360))=0 }
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
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _Exposure,_Rotation;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.direction=i.positionOS.xyz;return o;}
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 d=normalize(i.direction);
    float2 uv=float2(frac(atan2(d.x,d.z)/6.28318530718+.5+_Rotation/360),acos(clamp(d.y,-1,1))/3.14159265359);
    float2 texUV=float2(uv.x,1-uv.y);
    float3 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,texUV).rgb;
    float edge=min(uv.x,1-uv.x);
    float blend=(1-smoothstep(0,.018,edge))*.5;
    color=lerp(color,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(1-uv.x,texUV.y)).rgb,blend);
    float cap=1-smoothstep(0,.018,min(uv.y,1-uv.y));
    color=lerp(color,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(.5,texUV.y)).rgb,cap);
    return half4(color*_Exposure,1);
   }
   ENDHLSL
  }
 }
}
