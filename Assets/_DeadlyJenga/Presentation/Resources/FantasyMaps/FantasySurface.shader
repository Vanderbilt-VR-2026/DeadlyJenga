Shader "DeadlyJenga/Fantasy Surface"
{
 Properties {
  _BaseColor("Color",Color)=(1,.7,.3,1)
  _Accent("Accent",Color)=(1,.9,.6,1)
  _Pattern("Pattern",Float)=0
  _Smoothness("Gloss",Range(0,1))=.65
  _Scale("Texture scale",Float)=1
  _Metallic("Metal",Range(0,1))=0
  _Stripes("Candy ribbon",Range(0,1))=.35
  _Glow("Soft luminous trim",Range(0,1))=0
  _DetailTex("Surface grain",2D)="white"{}
  _DetailStrength("Grain strength",Range(0,1))=0
  _EnvironmentCube("Baked sky reflections",Cube)=""{}
  _CloudRadius("Cloud ground outline",Vector)=(0,0,0,0)
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {
  Tags {"LightMode"="UniversalForward"}
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
  TEXTURE2D(_DetailTex);SAMPLER(sampler_DetailTex);
  float _CandyOutage;
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseColor,_Accent,_CloudRadius;float _Pattern,_Smoothness,_Scale,_Metallic,_Stripes,_Glow,_DetailStrength;
  CBUFFER_END
  struct A {float4 p:POSITION;float3 n:NORMAL;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
  struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float3 local:TEXCOORD2;float fog:TEXCOORD4;float3 localNormal:TEXCOORD3;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
  V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.fog=ComputeFogFactor(o.p.z);o.world=TransformObjectToWorld(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.local=a.p.xyz;o.localNormal=a.n;o.color=a.color;return o;}
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
  float cotton(float2 p){return noise(p)*.60+noise(p*2.13+7)*.27+noise(p*4.31+19)*.13;}
  float hex(float2 q){float2 grid=float2(1.7320508,3);float2 a=q-grid*floor(q/grid+.5);float2 b=q-float2(.8660254,1.5);b-=grid*floor(b/grid+.5);float2 h=dot(a,a)<dot(b,b)?a:b;return max(abs(h.x),dot(abs(h),float2(.5,.8660254)));}
  half4 frag(V i):SV_Target {
   UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
   float3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.world),p=i.local*_Scale;
   float top=smoothstep(.5,.92,normalize(i.localNormal).y);
   float2 uv=abs(i.localNormal.y)>.6?p.xz:abs(i.localNormal.x)>.6?p.zy:p.xy;
   float3 detail=SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,uv*float2(.12,.075)).rgb;
   float3 color=_BaseColor.rgb;float gloss=_Smoothness;float relief=0;
   if(_Pattern<.5){
    float d=hex(uv*.62);float aa=max(fwidth(d),.015);float seam=smoothstep(.76-aa,.83+aa,d);
    float grain=cotton(uv*1.7);float wax=sin(uv.x*.37+sin(uv.y*.21))*.5+.5;
    color=lerp(color,_Accent.rgb,seam*.12*(1-top));color*=.96+.04*grain;
    color=lerp(color,float3(1,.91,.64),top*.90);color*=lerp(1,.8+detail*.25,_DetailStrength);gloss*=.84+.16*wax;relief=seam*.012*(1-top)+detail.r*.006*_DetailStrength;
    if(_Scale<.4){color=lerp(_BaseColor.rgb*.78,_Accent.rgb,cotton(uv*.14)*.8);color=lerp(color,_Accent.rgb,seam*.025);gloss=.87;}
   } else if(_Pattern<1.5){
    float warp=noise(float2(uv.x*1.3,uv.y*.045))*2.2;
    float grain=sin(uv.x*8+warp+sin(uv.y*.2))*.5+.5;
    float pores=pow(saturate(sin(uv.x*49+warp*5)),12)*(1-noise(uv*.6));
    color=lerp(_BaseColor.rgb*.7,_Accent.rgb,grain*.12+.10);color=lerp(color,detail*.80,_DetailStrength);
    gloss*=.88+.12*grain;relief=dot(detail,float3(.3,.6,.1))*.028*_DetailStrength;
   } else if(_Pattern<2.5){
    float q=(uv.x+uv.y)*.20;float aa=max(fwidth(q),.005);float stripe=smoothstep(.32-aa,.32+aa,frac(q))*(1-smoothstep(.66-aa,.66+aa,frac(q)));
    color=lerp(color,_Accent.rgb,stripe*_Stripes*(1-top));
    color=lerp(color,_Accent.rgb,top*.16);color*=.975+.025*noise(uv*3);
   } else if(_Pattern<3.5){
    float2 q=uv*.20;float softness=cotton(q);
    if(_CloudRadius.x>0)clip(.96-length(i.local.xz/_CloudRadius.xy)+(softness-.5)*.045);
    float2 grad=float2(cotton(q+float2(.035,0))-softness,cotton(q+float2(0,.035))-softness)/.035;
    if(abs(i.localNormal.y)>.6)n=normalize(n+float3(-grad.x,0,-grad.y)*1.1);
    color=lerp(_Accent.rgb*.88,_BaseColor.rgb,smoothstep(.08,.88,softness));gloss=.05;
   } else color=_BaseColor.rgb;
   if(i.color.a>.01)color*=i.color.rgb;
   // Derivative relief follows the moving body and needs no normal-map texture or extra draw.
   if(relief>0){float3 dp1=ddx(i.world),dp2=ddy(i.world);float3 r1=cross(dp2,n),r2=cross(n,dp1);float det=dot(dp1,r1);n=normalize(n-sign(det)*(ddx(relief)*r1+ddy(relief)*r2)/max(abs(det),.0001));}
   Light light=GetMainLight(TransformWorldToShadowCoord(i.world));float3 h=normalize(light.direction+v);
   float nl=saturate(dot(n,light.direction)),nv=saturate(dot(n,v)),nh=saturate(dot(n,h));
   float rough=max(.065,1-gloss),a2=pow(rough,4);float den=nh*nh*(a2-1)+1;
   float D=a2/(3.14159*den*den+.00001);float k=(rough+1)*(rough+1)*.125;
   float G=nv/(nv*(1-k)+k)*nl/(nl*(1-k)+k);
   float3 f0=lerp(float3(.045,.045,.045),color,_Metallic);
   float3 F=f0+(1-f0)*pow(1-saturate(dot(v,h)),5);
   float3 directSpec=min(D*G*F/max(4*nv*nl,.001),8);
   float3 ambient=max(SampleSH(n)*.53,float3(.10,.10,.12));
   float3 lit=color*(1-_Metallic*.85)*(ambient+nl*light.color*light.shadowAttenuation*.72);
   lit+=directSpec*nl*light.color*light.shadowAttenuation*.7;
   float3 reflected=SAMPLE_TEXTURECUBE_LOD(_EnvironmentCube,sampler_EnvironmentCube,reflect(-v,n),rough*6).rgb;
   float3 envF=f0+(max(gloss,f0)-f0)*pow(1-nv,5);
   lit+=reflected*envF*(.6+gloss*1.5);
   // A wide sky lobe keeps the rounded candy edge readable at map scale.
   float sheen=pow(saturate(dot(reflect(-v,n),normalize(float3(-.4,.8,-.3)))),lerp(10,60,gloss));
   lit+=float3(1,.97,.9)*sheen*gloss*.35;
   if(_Pattern>2.5&&_Pattern<3.5)lit=lerp(lit,color,.58);
   lit+=color*_Glow;
   float outage=saturate(1-_CandyOutage*.94);
   return half4(MixFog(lit*outage,i.fog),1);
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
  float4 _BaseColor,_Accent,_CloudRadius;float _Pattern,_Smoothness,_Scale,_Metallic,_Stripes,_Glow,_DetailStrength;
  CBUFFER_END
  #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
  ENDHLSL
 }
 }
}

