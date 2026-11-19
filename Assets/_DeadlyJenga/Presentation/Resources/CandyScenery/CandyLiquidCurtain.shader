Shader "Candy Tropical/Liquid Curtain"
{
 Properties {[PerRendererData] _MainTex("Sprite Texture",2D)="white" {} _Color("Color",Color)=(1,.2,.4,1) _Progress("Fill",Float)=0 _Sand("Sand effect",Float)=0}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest Always
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float4 _Color;float _Progress;float _CandyHeat;float _Sand;
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color*_Color;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float t=_Time.y;
 if(_Sand>.5)
 {
  float2 uv=i.uv;
  float edge=_Progress+sin(uv.x*19+t*2.9)*.021+sin(uv.x*43-t*3.6)*.013;
  float depth=edge-uv.y;
  float fill=smoothstep(-.023,.045,depth);
  float2 cells=uv*float2(60,34)+float2(t*.9,-t*1.9);
  float grain=frac(sin(dot(floor(cells),float2(127.1,311.7)))*43758.5453);
  float fleck=(1-smoothstep(.10,.25,length(frac(cells)-.5)))*step(.67,grain);
  float rim=(1-smoothstep(.009,.043,abs(depth)))*(.65+.35*sin(uv.x*29+t*2));
  float eddy=.5+.5*sin(uv.x*33+depth*39-t*6);
  float3 sand=i.color.rgb*(.78+.18*eddy)+float3(.36,.24,.095)*(rim+fleck*.45);
  return half4(sand,saturate(fill*.75+rim*.38+fleck*smoothstep(-.06,.025,depth)*.20)*i.color.a);
 }
 float edge=_Progress+sin(i.uv.x*11+t*3)*.025+sin(i.uv.x*23-t*2)*.012;float a=1-smoothstep(edge-.012,edge+.012,i.uv.y);float2 cell=frac(i.uv*float2(12,8)+float2(0,-t*.12))-.5;float bubble=1-smoothstep(.11,.15,length(cell));float rim=1-smoothstep(.01,.055,abs(i.uv.y-edge));float3 c=i.color.rgb+rim*.24+bubble*.15*a;float edgeHeat=max(abs(i.uv.x-.5),abs(i.uv.y-.5))*2;float heatAlpha=saturate(_CandyHeat)*(.018+.045*edgeHeat)*(.7+.3*sin(i.uv.y*46+sin(i.uv.x*17+t)*2-t*2));c=lerp(c,float3(1,.61,.3),heatAlpha/max(.001,a+heatAlpha));return half4(c,max(a*i.color.a,heatAlpha)*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a);}
 ENDHLSL }
 }
}
