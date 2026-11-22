Shader "DeadlyJenga/Caldera/AshSky"
{
 SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "CalderaNoise.cginc"
 struct Data { float4 pos:SV_POSITION;float3 dir:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
 Data vert(appdata_base v){UNITY_SETUP_INSTANCE_ID(v);Data o;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
 half4 frag(Data i):SV_Target
 {
  float3 d=normalize(i.dir);float angle=atan2(d.x,d.z), y=d.y;
  float cloud=strata(d.xz*5/(max(y,.025)+.34)+_Time.y*.009);
  float3 sky=lerp(float3(.97,.18,.20),float3(.095,.017,.14),smoothstep(-.04,.36,y));
  sky*=.66+cloud*.55;sky+=float3(.32,.047,.16)*smoothstep(.46,.72,cloud);
  float sector=round(angle/.392699);float x=angle-sector*.392699;
  float h=.052+hash21(float2(sector,9))*.094+(abs(sector)<.1?.06:0);
  float flank=h*exp(-abs(x)*16)+.007*noise2(float2(angle*120,3));
  if(y<flank)
  {
   float ridge=strata(float2(angle*68,y*38));
   float streak=abs(sin(x*281/(1.15-y/max(h,.01)*.72)+ridge*3.1));
   float hot=exp(-streak*11)*smoothstep(.32,.65,ridge);
   sky=lerp(float3(.18,.039,.115),float3(.38,.089,.14),ridge)+hot*float3(1.6,.24,.018);
  }
  float vent=exp(-abs(x)*290-abs(y-h)*215);
  sky+=vent*float3(3,1.28,.12);
  float t=(y-h)/.19;
  float plume=1-smoothstep(.45,1.0,abs(x-.028*t)/(.008+.047*saturate(t))+(cloud-.5)*1.9);
  sky=lerp(sky,float3(.17,.065,.17)+cloud*.08,plume*smoothstep(0,.16,t)*(1-smoothstep(.52,1,t)));
  return half4(sky,1);
 }
 ENDCG
 } }
}
