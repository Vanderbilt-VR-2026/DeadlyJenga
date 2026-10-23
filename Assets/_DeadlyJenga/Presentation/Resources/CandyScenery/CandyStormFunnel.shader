Shader "Candy Tropical/Storm Funnel"
{
 Properties { _BaseColor("Cloud",Color)=(.24,.28,.31,.84) }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"} ZWrite On Cull Back
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _BaseColor;
 struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 p=float2(i.uv.x*18+i.uv.y*8-_Time.y*1.8,i.uv.y*12-_Time.y*.65);float n=noise(p)*.6+noise(p*2.1)*.28+noise(p*4.3)*.12;float strand=.5+.5*sin(i.uv.y*72+i.uv.x*19-_Time.y*8+n*5);float light=.55+.45*saturate(dot(normalize(i.normal),normalize(float3(-.4,.6,-.5))));float edge=pow(saturate(1-abs(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world))))),.3);float alpha=(.91+n*.09)*_BaseColor.a*(1-edge*.12)*smoothstep(0,.045,i.uv.y);return half4(_BaseColor.rgb*light*(.5+n*.8+strand*.08),1);}
 ENDHLSL }
 }
}


