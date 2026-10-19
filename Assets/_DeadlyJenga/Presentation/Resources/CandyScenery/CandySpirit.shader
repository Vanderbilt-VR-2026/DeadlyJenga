Shader "Candy Tropical/Ocean Spirit"
{
 Properties {_BaseColor("Water tint",Color)=(.025,.65,.8,1)}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 float4 _BaseColor;struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 n=normalize(i.normal),v=normalize(_WorldSpaceCameraPos-i.world);float ripple=sin(i.world.x*4+i.world.y*2+_Time.y*2)*sin(i.world.z*3-i.world.y+_Time.y)*.5+.5;n=normalize(n+float3(ripple*.08,0,-ripple*.08));Light l=GetMainLight();float fresnel=pow(1-saturate(dot(n,v)),3);float spec=pow(saturate(dot(n,normalize(v+l.direction))),90);float3 c=_BaseColor.rgb*(.4+.4*saturate(dot(n,l.direction)))+float3(.35,.85,1)*fresnel*.7+spec*l.color+pow(ripple,12)*.12;return half4(c,1);}
 ENDHLSL }
 }
}
