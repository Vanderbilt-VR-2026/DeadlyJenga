Shader "Candy Tropical/Scenic Surface"
{
 Properties { _BaseColor("Base",Color)=(.7,.03,.01,1) _Accent("Accent",Color)=(1,.4,.01,1) _Style("Style",Float)=1 }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _BaseColor,_Accent;float _Style;
 struct A{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 p=i.world.xz*.22;float n=noise(p+_Time.y*.025)+.35*noise(p*2.7-_Time.y*.04);float3 c=lerp(_BaseColor.rgb,_Accent.rgb,smoothstep(.3,.95,n));float a=1;if(_Style<1.5){float crust=smoothstep(.67,.74,n);c=lerp(c,float3(.055,.025,.04),crust);c+=_Accent.rgb*pow(saturate(1-abs(n-.65)*8),3)*.6;}else if(_Style<2.5){float ripples=sin(p.x*3+noise(p)*4)*.5+.5;c*=lerp(.85,1.1,ripples);}else{c=float3(.83,.92,1);a=.035+.025*noise(p*.1);}return half4(c,a);}
 ENDHLSL }
 }
}
