Shader "Candy Tropical/Scaffold Oil Glaze"
{
 Properties {_BaseColor("Glazed metal",Color)=(.055,.08,.1,1)}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 float4 _BaseColor;struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 n=normalize(i.normal),v=normalize(GetWorldSpaceViewDir(i.world));Light sun=GetMainLight();float f=pow(1-saturate(dot(n,v)),2);float phase=dot(n,float3(2,3,1))+dot(i.world,float3(.04,.025,.03))+f*2;float3 film=.5+.5*cos(6.28318*(phase+float3(0,.33,.67)));float3 base=lerp(_BaseColor.rgb,film*.55,.3+f*.65);float spec=pow(saturate(dot(n,normalize(v+sun.direction))),100);float light=.25+.75*saturate(dot(n,sun.direction));return half4(base*light+sun.color*spec*1.6+film*f*.15,1);}
 ENDHLSL }
 }
}
