Shader "DeadlyJenga/Fruit Candy Glass"
{
 Properties {_BaseColor("Fruit color",Color)=(.1,1,.3,.3) _Smoothness("Polish",Range(0,1))=.98 _EnvironmentCube("Baked sky reflections",Cube)=""{}}
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off
 Cull Back
 HLSLPROGRAM
 #pragma target 3.5
  #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
  #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURECUBE(_EnvironmentCube);SAMPLER(sampler_EnvironmentCube);
 float _CandyOutage;
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _Smoothness;
 CBUFFER_END
 struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float3 local:TEXCOORD2;float fog:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.fog=ComputeFogFactor(o.p.z);o.world=TransformObjectToWorld(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.local=a.p.xyz;return o;}
 half4 frag(V i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.world);Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
 float nv=saturate(dot(n,v)),fres=.055+.945*pow(1-nv,4);float3 refl=reflect(-v,n);
 float3 environment=SAMPLE_TEXTURECUBE_LOD(_EnvironmentCube,sampler_EnvironmentCube,refl,(1-_Smoothness)*5).rgb;
 float softbox=pow(saturate(dot(refl,normalize(float3(-.4,.85,-.3)))),28)+pow(saturate(dot(refl,normalize(float3(.7,.3,-.5)))),90)*.5;
 float sun=pow(saturate(dot(n,normalize(v+l.direction))),lerp(70,240,_Smoothness))*1.7*l.shadowAttenuation;
 float3 q=i.local*.75;float3 cell=floor(q);float random=frac(sin(dot(cell,float3(12.98,78.23,34.12)))*43758.54);
 float3 offset=float3(frac(random*13.7),frac(random*37.3),frac(random*7.1))*.6+.2;
 float d=length(frac(q)-offset);float bubble=(1-smoothstep(.025,.055,d))*step(.93,random);
 float nl=saturate(dot(n,l.direction));
 // Saturated body and denser grazing edges suggest thick fruit candy rather than ghostly plastic.
 float3 tint=pow(max(_BaseColor.rgb,.004),lerp(1.45,.75,nv));
 float3 body=tint*(.38+.48*nl*l.shadowAttenuation+.2*max(n.y,0));
 float3 color=body*(1-fres*.65)+environment*(.025+fres*1.1)+softbox*.42+sun*l.color+bubble*.65;
 float alpha=saturate(.88+fres*.10+softbox*.08+bubble*.12);
 return half4(MixFog(color*saturate(1-_CandyOutage*.94),i.fog),alpha);
 }
 ENDHLSL
 }
 Pass {
  Name "ShadowCaster"
  Tags {"LightMode"="ShadowCaster"}
  ZWrite On ZTest LEqual ColorMask 0 Cull Back
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex ShadowPassVertex
  #pragma fragment ShadowPassFragment
  #pragma multi_compile_instancing
  #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseColor;float _Smoothness;
  CBUFFER_END
  #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
  ENDHLSL
 }
 }
}
