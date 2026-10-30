Shader "Candy Tropical/Neon Outage"
{
 Properties {_BaseColor("Color",Color)=(.5,.2,1,1) _Filled("Filled glow",Float)=0 _Rim("Rim visibility distance",Float)=9 _BoundsCenter("Center",Vector)=(0,0,0,0) _BoundsSize("Size",Vector)=(1,1,1,0)}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _BaseColor,_BoundsCenter,_BoundsSize;float _Filled,_Rim;
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 q:TEXCOORD1;float3 normal:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.q=(i.positionOS.xyz-_BoundsCenter.xyz)/max(.001,_BoundsSize.xyz*.5);o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 q=abs(i.q);float edge=max(min(q.x,q.y),max(min(q.y,q.z),min(q.x,q.z)));float rim=smoothstep(.84,.98,edge);float reach=max(.1,_Rim);float nearby=1-smoothstep(reach*.35,reach,distance(i.world,_WorldSpaceCameraPos));float fresnel=pow(1-saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),3);float glow=_Filled>.5?.65+fresnel*.6:rim*nearby*.55;return half4(_BaseColor.rgb*glow+float3(.012,.003,.025),1);}
 ENDHLSL }
 }
}
