Shader "DeadlyJenga/Gleaming Plasma Claws"
{
 Properties { _BaseColor("Plasma tint",Color)=(.1,.9,1,1) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
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
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 spark:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float3 axisWS:TEXCOORD3;float sparkAlpha:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 position = i.positionOS.xyz;
    o.sparkAlpha = -1;
    if (i.spark.w > 0)
    {
        float flight = frac(i.spark.x + _Time.y*i.spark.w*1.65);
        position += float3(i.spark.yz,.068) * flight;
        o.sparkAlpha = smoothstep(.015,.12,flight)*(1-smoothstep(.42,.90,flight));
    }
    o.positionWS=TransformObjectToWorld(position);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.axisWS=TransformObjectToWorldDir(float3(0,0,1));o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 avatarColor = max(_BaseColor.rgb, .012);
    float strongestChannel = max(avatarColor.r,max(avatarColor.g,avatarColor.b));
    float3 hue = avatarColor / max(.15,strongestChannel);
    if (i.sparkAlpha >= 0)
        return half4(lerp(hue,1,.24)*2.8,i.sparkAlpha);
    float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS),axis=normalize(i.axisWS);
    float3 across=view-axis*dot(view,axis);float facing=saturate(dot(normalize(i.normalWS),normalize(across+float3(.00001,0,0))));
    float flow=.92+.08*sin(i.uv.y*70-_Time.y*19+sin(i.uv.x*18+i.uv.y*23));
    float core=smoothstep(.87,.96,facing),arc=pow(saturate(sin(i.uv.y*145-_Time.y*30+i.uv.x*8)),28)*.16;
    float3 face = lerp(avatarColor,hue,.64)*(.68+.45*facing);
    float3 hot = lerp(hue,1,.56)*2.1;
    float3 color = lerp(face,hot,core)*flow+arc*core*hue;
    return half4(color,.97);
   }
   ENDHLSL
  }
 }
}
