Shader "Candy Tropical/Storm Funnel"
{
 Properties
 {
  _BaseColor("Cloud",Color)=(.35,.38,.43,.95)
  _DensityTex("Density volume",3D)="white"{}
  _StormInfo("Size height speed",Vector)=(16,65,2,0)
  _MarchBudget("Maximum ray steps",Float)=52
  _StormDissolve("Breakup",Range(0,1))=0
  _StormUseSceneDepth("Use camera depth",Float)=1
 }
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
   float _MarchBudget,_StormDissolve,_StormUseSceneDepth;
   struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i)
   {
    V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.local=i.positionOS.xyz;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;
   }
   float2 RayBox(float3 ro,float3 inv,float3 low,float3 high,float farDepth)
   {
    float3 a=(low-ro)*inv,b=(high-ro)*inv;
    float3 enter=min(a,b),leave=max(a,b);
    float start=max(0,max(enter.x,max(enter.y,enter.z)));
    float end=min(farDepth,min(leave.x,min(leave.y,leave.z)));
    return float2(start,max(start,end));
   }
   float Noise(float3 p,float mip){return SAMPLE_TEXTURE3D_LOD(_DensityTex,sampler_DensityTex,p,mip).r;}
   float2 Turn(float2 p,float angle){float s,c;sincos(angle,s,c);return float2(p.x*c-p.y*s,p.x*s+p.y*c);}

   float Density(float3 p,float time,out float grain,out float cloud,out float edge)
   {
    float size=_StormInfo.x,height=_StormInfo.y,t=saturate(p.y/height);
    float2 lean=float2(sin(t*3+time*.7),cos(t*4-time*.6))*size*.13*t;
    float2 q=p.xz-lean;
    float r=length(q),radius=lerp(size*.155,size*.82,t*(.79+.21*t));
    float twist=t*9-time*_StormInfo.z*2.2;
    float2 flow=Turn(q,twist);
    float rel=r/max(.1,radius);
    float capY=(p.y-height)/size;
    float body=0;cloud=0;grain=.5;edge=saturate(rel);
    float skirtHeight=max(.7,size*.17);
    bool inSkirt=p.y<skirtHeight&&r<size*.88;
    if((rel<1.6||inSkirt)&&p.y>=-1&&capY<.24)
    {
     float3 v=float3(flow.x*.046,p.y*.067-time*.65,flow.y*.046);
     float large=Noise(v,.3);
     float fine=Noise(v*2.63+2.8,_MarchBudget>20?.2:.65);
     float striation=sin(p.y*.44+flow.x*.27+large*5)*.5+.5;
     grain=large*.63+fine*.26+striation*.11;
     body=saturate((1.29-rel+(grain-.5)*1.26)*3.1);
     body*=smoothstep(-1,1.2,p.y)*(1-smoothstep(.02,.24,capY))*(.66+grain*2.05);
     // Reuse the same advected noise for the low, ragged wall of debris.
     if(inSkirt)
     {
      float spread=saturate((.84-r/size+(grain-.5)*.24)*5);
      float lift=smoothstep(-1,.12,p.y)*(1-smoothstep(skirtHeight*.18,skirtHeight,p.y));
      body+=spread*lift*smoothstep(.24,.64,grain)*.78;
     }
    }
    if(capY>-.92)
    {
     float2 rotation=Turn(p.xz,-time*_StormInfo.z*.18);
     float rim=length(rotation)/size;
     float shelf=.25+.21*sin(rotation.x*.16+rotation.y*.11);
     float dist=length(float3(rotation.x/(size*3.32),(capY-shelf)/.99,rotation.y/(size*3.32)));
     if(dist<1.34)
     {
      float3 v=float3(rotation.x*.025,p.y*.049+time*.045,rotation.y*.025);
      float large=Noise(v,0);
      float fine=_MarchBudget>20?Noise(v*2.78+4,.15):large;
      float lobes=large*.72+fine*.28;
      cloud=saturate((1.12-dist+(lobes-.5)*1.40)*3.8)*(1-smoothstep(1.10,1.30,dist));
      grain=lerp(grain,lobes,cloud);
      edge=lerp(edge,saturate(rim/3.4),cloud);
     }
    }
    if(_StormDissolve>.001)
    {
     float liftedBase=smoothstep(_StormDissolve*.9-.12,_StormDissolve*.9+.16,t);
     body*=lerp(1,liftedBase,smoothstep(0,.28,_StormDissolve));
     float breakup=smoothstep(_StormDissolve*.84-.14,_StormDissolve*.84+.2,grain);
     return (body+cloud*(.96+grain*1.50))*lerp(1,breakup,_StormDissolve)*(1-_StormDissolve);
    }
    return body+cloud*(.96+grain*1.50);
   }

   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=normalize(i.local-ro);
    float size=_StormInfo.x,height=_StormInfo.y;
    float3 inv=rcp(rd+float3(.000001,.000001,.000001));
    float farDepth=100000;
    if(_StormUseSceneDepth>.5)
    {
     float depth=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
     #if UNITY_REVERSED_Z
     bool geometry=depth>.00001;
     #else
     bool geometry=depth<.99999;
     #endif
     if(geometry)
     {
      float3 viewRay=TransformWorldToViewDir(TransformObjectToWorldDir(rd));
      farDepth=LinearEyeDepth(depth,_ZBufferParams)/max(.001,-viewRay.z);
     }
    }
    float radius=size*1.38,cloudRadius=size*4.4;
    float2 body=RayBox(ro,inv,float3(-radius,-1,-radius),float3(radius,height+size*.24,radius),farDepth);
    float2 cap=RayBox(ro,inv,float3(-cloudRadius,height-size*.92,-cloudRadius),float3(cloudRadius,height+size*1.8,cloudRadius),farDepth);
    bool bodyFirst=body.x<cap.x;
    float2 a=bodyFirst?body:cap,b=bodyFirst?cap:body;
    if(a.y>a.x)b.x=max(b.x,a.y);
    float first=max(0,a.y-a.x),second=max(0,b.y-b.x),span=first+second;
    clip(span-.001);
    #if defined(SHADER_API_MOBILE)
    const int limit=20;
    #else
    const int limit=52;
    #endif
    int steps=(int)min(min(_MarchBudget,limit),max(7,ceil(span/max(.65,size*.09))));
    // Preserve the narrow funnel when the wide cloud cap consumes most of the ray.
    int firstSteps=0;
    if(first>.001)
    {
     if(second<=.001)firstSteps=steps;
     else
     {
      float firstWeight=first*(bodyFirst?2.7:1),secondWeight=second*(bodyFirst?1:2.7);
      int bodyMinimum=min(7,steps-3);
      firstSteps=clamp((int)round(steps*firstWeight/(firstWeight+secondWeight)),
          bodyFirst?bodyMinimum:3,bodyFirst?steps-3:steps-bodyMinimum);
     }
    }
    float firstStep=first/max(1,firstSteps),secondStep=second/max(1,steps-firstSteps);
    float jitter=.5+(frac(dot(floor(i.positionCS.xy),float2(.75487767,.56984029)))-.5)*.18;
    float crownDistance=length(float3(ro.x/(size*3.32),((ro.y-height)/size-.25)/.99,ro.z/(size*3.32)));
    float insideCrown=1-smoothstep(.78,1.12,crownDistance);
    float3 sum=0;float trans=1;
    [loop]for(int k=0;k<limit;k++)
    {
     if(k>=steps)break;
     bool inFirst=k<firstSteps;
     float stepSize=inFirst?firstStep:secondStep;
     float rayDistance=inFirst?a.x+(k+jitter)*stepSize:b.x+(k-firstSteps+jitter)*stepSize;
     float3 p=ro+rd*rayDistance;
     float grain,cloud,edge;
     float density=Density(p,_Time.y,grain,cloud,edge);
     if(density<.005)continue;
     // The crown keeps its exterior silhouette but leaves nearby geometry readable from inside.
     density*=lerp(1,.11,insideCrown*saturate(cloud*1.4));
     float nearFade=smoothstep(.15,1.10,rayDistance)*lerp(1,smoothstep(2,12,rayDistance),insideCrown);
     float alpha=1-exp(-density*stepSize*.95*nearFade);
     float lightSide=saturate(.52+dot(p.xz,float2(-.39,-.26))/max(size,length(p.xz)));
     float heightLight=saturate((p.y-height*.43)/max(1,height*.57));
     float light=saturate(.045+lightSide*.35+grain*.74+edge*.19+heightLight*.13-density*.25);
     // Charcoal folds with cold lit edges; keep low grit slightly warm.
     float3 color=lerp(float3(.009,.013,.025),float3(.42,.48,.60),light*light);
     color+=float3(.027,.019,.009)*(1-heightLight)*(1-cloud);
     if(_StormInfo.w>.01)
      color+=_StormInfo.w*float3(.20,.32,.78)*exp(-length((p-float3(size,height+size*.15,size*.4))/size))*cloud;
     sum+=trans*alpha*color;trans*=1-alpha;
     if(trans<.018)break;
    }
    return half4(sum,1-trans);
   }
   ENDHLSL
  }
 }
}
