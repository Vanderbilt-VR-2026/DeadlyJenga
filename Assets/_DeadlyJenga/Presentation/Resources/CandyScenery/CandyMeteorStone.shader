Shader "Candy Tropical/Meteor Stone"
{
 Properties {_BaseColor("Stone",Color)=(.13,.085,.065,1) _Heat("Heat",Range(0,1))=1 _GlowColor("Crack glow",Color)=(2.3,.38,.025,1) _Frost("Frost (icy skin)",Range(0,1))=0}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _BaseColor,_GlowColor;float _Heat,_Frost;
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
 // 3D cellular noise: distance to the nearest cell border (F2 - F1), for the v2 magma crack network.
 float3 hash3(float3 p){p=float3(dot(p,float3(127.1,311.7,74.7)),dot(p,float3(269.5,183.3,246.1)),dot(p,float3(113.5,271.9,124.6)));return frac(sin(p)*43758.5453);}
 float Cracks(float3 p){float3 c=floor(p),f=frac(p);float f1=8,f2=8;
  [unroll]for(int z=-1;z<=1;z++)[unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++){float3 o=float3(x,y,z);float3 d=o+hash3(c+o)*.9-f;float dd=dot(d,d);if(dd<f1){f2=f1;f1=dd;}else if(dd<f2)f2=dd;}
  return sqrt(f2)-sqrt(f1);}
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.p=i.positionOS.xyz;o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float n=noise(i.p*18)+noise(i.p*47)*.25;
 // v2 look: crust plates split by a network of glowing cracks (big cracks + finer ones), plus the old seams.
 float3 q=i.p*5.5+noise(i.p*6)*.35;float big=Cracks(q),fine=Cracks(q*2.6+3.1);
 float fissure=max(1-smoothstep(.02,.09,big),(1-smoothstep(.015,.05,fine))*.6);
 fissure=max(fissure,(1-smoothstep(.025,.075,abs(n-.58)))*.5);float light=.2+.8*saturate(dot(normalize(i.normal),normalize(float3(-.3,.8,-.5))));float3 stone=_BaseColor.rgb*light*(.65+n*.75);
 // Icy skin: frosted white patches and a cold sparkle over the glassy ice.
 float frost=_Frost*smoothstep(.25,.95,n+noise(i.p*90)*.25)*.85;stone=lerp(stone,float3(.88,.94,1)*(.55+light*.6),frost);
 stone+=_Frost*pow(noise(i.p*140),12)*float3(1.2,1.4,1.6)*light;
 // Heat bleeds out of the cracks into the crust around them.
 stone+=(1-smoothstep(.02,.28,big))*_Heat*_GlowColor.rgb*.1;
 return half4(stone+fissure*_Heat*_GlowColor.rgb,1);}
 ENDHLSL }
 }
}
