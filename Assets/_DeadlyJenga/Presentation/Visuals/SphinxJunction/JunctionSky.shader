Shader "DeadlyJenga/Sphinx Junction/Canyon sunset" {
 Properties { _MainTex ("Panorama",2D)="white"{} }
 SubShader { Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 struct A {float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 p:SV_POSITION;float3 d:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o; UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.d=i.p.xyz;return o;}
 half4 frag(V i):SV_Target{float3 d=normalize(i.d);return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(.408+atan2(d.x,d.z)*.47746483,.5+atan2(3*d.y,length(d.xz))*.31830989));}
 ENDHLSL
 }}}
