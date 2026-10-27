Shader "Candy Tropical/Storm Funnel"
{
 Properties { _BaseColor("Cloud",Color)=(.35,.38,.43,.95) _DensityTex("Density volume",3D)="white"{} _StormInfo("Size height speed",Vector)=(16,65,2,0) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent"}
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass
  {
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE3D(_DensityTex);SAMPLER(sampler_DensityTex);
   float4 _BaseColor,_StormInfo;
   struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.local=i.positionOS.xyz;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
   float Density(float3 p,out float grain,out float cloud)
   {
    float size=_StormInfo.x,height=_StormInfo.y,t=saturate(p.y/height);
    float2 lean=float2(sin(t*3+_Time.y*.7),cos(t*4-_Time.y*.6))*size*.13*t;
    float2 q=p.xz-lean;float radius=lerp(size*.12,size*.75,pow(t,1.2));
    float a=atan2(q.y,q.x)+t*9-_Time.y*_StormInfo.z*2.7;
    float r=length(q);radius*=1+.15*sin(a*2+p.y*.12)+.06*sin(a*5-p.y*.4);
    float capAngle=atan2(p.z,p.x)-_Time.y*_StormInfo.z*.2;
    float shelf=height+size*.35+sin(r*.12-capAngle*2)*size*.35;
    float3 dome=float3(p.x/(size*3.8),(p.y-shelf)/(size*1.2),p.z/(size*3.8));
    grain=0;cloud=0;
    if((r>radius*1.5||p.y<0||p.y>height+size*.3)&&length(dome)>1.35)return 0;
    float3 flow=float3(cos(a)*r,p.y-_Time.y*8,sin(a)*r);
    float n=SAMPLE_TEXTURE3D_LOD(_DensityTex,sampler_DensityTex,flow*.044,.5).r;
    n=n*.65+SAMPLE_TEXTURE3D_LOD(_DensityTex,sampler_DensityTex,flow*.09+3.7,.8).r*.35;
    float rib=.75+.25*sin(a*3+p.y*.16+n*2);
    float core=(1-smoothstep(.35,1.2,r/max(.1,radius)+(.5-n)*.8))*smoothstep(-1,1,p.y)*(1-smoothstep(height,height+size*.3,p.y));
    float cnoise=SAMPLE_TEXTURE3D_LOD(_DensityTex,sampler_DensityTex,float3(cos(capAngle)*r*.025,p.y*.05,sin(capAngle)*r*.025)+_Time.y*float3(.015,0,.02),0).r;
    float capDetail=SAMPLE_TEXTURE3D_LOD(_DensityTex,sampler_DensityTex,float3(cos(capAngle)*r*.07,p.y*.09,sin(capAngle)*r*.07),.5).r;
    cloud=saturate((1.04-length(dome)+(cnoise-.5)*1.6+(capDetail-.5)*.7)*3.1);
    grain=lerp(n,cnoise,cloud);return core*(.5+n*1.6)*rib+cloud*(.5+cnoise)*.9;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=normalize(i.local-ro);
    float size=_StormInfo.x,height=_StormInfo.y;
    float3 low=float3(-size*5.2,-1,-size*5.2),high=float3(size*5.2,height+size*2.5,size*5.2);
    float3 inv=rcp(rd+float3(.000001,.000001,.000001));
    float3 b0=(low-ro)*inv,b1=(high-ro)*inv;
    float3 nearB=min(b0,b1),farB=max(b0,b1);
    float start=max(0,max(nearB.x,max(nearB.y,nearB.z))),end=min(farB.x,min(farB.y,farB.z));
    float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
    float depth=SampleSceneDepth(uv);
    #if UNITY_REVERSED_Z
    bool geometry=depth>.00001;
    #else
    bool geometry=depth<.99999;depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif
    if(geometry){float3 scene=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);end=min(end,distance(_WorldSpaceCameraPos,scene));}
    clip(end-start);
    float stepSize=(end-start)/96;
    float jitter=.5+(frac(sin(dot(floor(i.positionCS.xy),float2(12.9898,78.233)))*43758.5453)-.5)*.15;
    float3 sum=0;float trans=1;
    [loop]for(int k=0;k<96;k++)
    {
     float3 p=ro+rd*(start+(k+jitter)*stepSize);float grain,cloud;
     float density=Density(p,grain,cloud);
     float alpha=1-exp(-density*stepSize*.8);
     float rim=saturate(length(p.xz)/max(size*.2,lerp(size*.085,size*.7,saturate(p.y/height))));
     float shadowGrain,shadowCloud;
     float shadow=Density(p+float3(-2,3,-1),shadowGrain,shadowCloud);
     float light=saturate(.1+exp(-shadow*1.65)*.85+(grain-.5)*1.4);
     light*=light;
     float3 color=lerp(float3(.005,.008,.024),float3(.38,.48,.58),light);
     color+=float3(.012,.004,.03)*(1-light)*.35;
     color+=float3(.08,.15,.23)*pow(saturate(rim-.55),3)*light*(1-cloud)*.18;
     float flashGlow=exp(-length((p-float3(size,height+size*.15,size*.4))/size)*1.2)*cloud;
     color+=_StormInfo.w*float3(.25,.36,.8)*flashGlow*1.6;
     sum+=trans*alpha*color;trans*=1-alpha;
     if(trans<.015)break;
    }
    return half4(sum,1-trans);
   }
   ENDHLSL
  }
 }
}
