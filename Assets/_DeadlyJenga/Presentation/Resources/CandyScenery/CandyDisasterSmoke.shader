Shader "Candy Tropical/Disaster Smoke"
{
 Properties { _BaseColor("Tint",Color)=(.25,.28,.3,.7) _Flame("Flame",Float)=0 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _BaseColor;float _Flame;
 struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 p=i.uv*2-1;float n=noise(i.uv*5+float2(_Time.y*.16,-_Time.y*.35)+i.world.xz*.03);float edge=1-smoothstep(.18,1,length(p)+n*.17);float density=(.35+.65*n)*edge;float3 color=_BaseColor.rgb*(.68+n*.65);if(_Flame>.5){float core=pow(saturate(1-abs(p.x)*1.6),3)*(1-i.uv.y);color=lerp(float3(1,.12,.012),float3(1,.88,.28),core);density*=1-smoothstep(.45,1,i.uv.y+n*.25);}return half4(color,density*_BaseColor.a);}
 ENDHLSL }
 }
}
