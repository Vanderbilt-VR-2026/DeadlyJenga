Shader "DeadlyJenga/Sphinx Junction/Painted masonry"
{
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct Input {float4 p:POSITION; float3 n:NORMAL; half4 c:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct Vary {float4 pos:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; half3 c:COLOR; UNITY_VERTEX_OUTPUT_STEREO};
 Vary vert(Input i) {Vary o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 o.w=TransformObjectToWorld(i.p.xyz); o.pos=TransformWorldToHClip(o.w); o.n=TransformObjectToWorldNormal(i.n); o.c=i.c.rgb; return o;}
 half4 frag(Vary i):SV_Target {
 Light l=GetMainLight(TransformWorldToShadowCoord(i.w));
 float diffuse=saturate(dot(normalize(i.n),l.direction));
 float grit=frac(sin(dot(floor(i.w*21),float3(12.7,17.1,34.2)))*4374.11);
 float band=sin(i.w.y*4.1+sin(i.w.x*.7+i.w.z*.32)*1.7);
 half3 pigment=pow(i.c,2.0);
 return half4(pigment*(float3(.50,.41,.39)+l.color*diffuse*l.shadowAttenuation*.75)*(.97+.048*grit+.024*band),1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
