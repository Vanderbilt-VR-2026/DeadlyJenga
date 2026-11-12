Shader "Candy Tropical/Scenic Sky"
{
 Properties { _Zenith("Zenith",Color)=(.03,.37,.92,1) _Horizon("Horizon",Color)=(.48,.86,1,1) _Cloud("Cloud",Color)=(1,1,1,1) _CloudCover("Cloud cover",Float)=.44 _Stars("Stars",Float)=0 _SunDirection("Sun direction",Vector)=(0,1,0,0) _SunColor("Sun",Color)=(1,.94,.83,1) [HideInInspector] _CoastDay("Warm coastal clouds",Float)=0 [HideInInspector] _CoastWaterLevel("Water elevation",Float)=0 [NoScaleOffset] _CoastPanorama("Island cloud panorama",2D)="black"{} [HideInInspector] _CoastHasPanorama("Panorama present",Float)=0 }
 SubShader { Tags {"Queue"="Background" "RenderType"="Background"} Cull Off ZWrite Off
 Pass { HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Zenith,_Horizon,_Cloud,_SunDirection,_SunColor;float _CloudCover,_Stars,_CoastDay,_CoastWaterLevel,_CoastHasPanorama;
 TEXTURE2D(_CoastPanorama); SAMPLER(sampler_CoastPanorama);
 #include "CandyCoastPanorama.hlsl"
 struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 positionCS:SV_POSITION;float3 dir:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.dir=i.positionOS.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
 float CloudMass(float2 p)
 {
     p+=float2(noise(p*.66+9),noise(p*.67-11))*.93;
     return noise(p)*.57+noise(p*2.05+5)*.27+noise(p*4.27+12)*.115+noise(p*9.1-8)*.045;
 }
 float3 CoastSky(float3 d)
 {
     float3 sun=normalize(_SunDirection.xyz);
     float horizon=saturate(d.y);
     float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(horizon,.39));
     float2 p=d.xz/max(.17,d.y+.16)*3.1+float2(8.7,-3.2);
     float n=CloudMass(p);
     float shade=CloudMass(p+normalize(sun.xz)*.29);
     float coverage=smoothstep(.47,.65,n)*smoothstep(.004,.041,d.y);
     float light=saturate(.13+(n-shade)*2.7+n*.90);
     float3 cloud=lerp(float3(.46,.55,.80),float3(1.38,1.16,.93),smoothstep(.38,.86,light));
     sky=lerp(sky,cloud,coverage);
     UNITY_BRANCH if(_CoastHasPanorama>.5) sky=CandyCoastClouds(d);
     sky+=_SunColor.rgb*(pow(saturate(dot(d,sun)),700)*1.32+pow(saturate(dot(d,sun)),23)*.10);
     float depth=_CoastWaterLevel-_WorldSpaceCameraPos.y;
     float marineLine=d.y-depth*_ProjectionParams.w;
     float airBlend=1-smoothstep(.012,.084,abs(marineLine));
     float3 marineAir=CandyCoastMarineAir(d,sun);
     sky=lerp(sky,marineAir,airBlend);
     if(depth<.08 && marineLine>-.008 && marineLine<.065)
     {
         float2 bearing=normalize(d.xz+float2(.0001,0));
         float group=smoothstep(.30,.68,noise(bearing*5.8+17.1));
         float ridge=noise(bearing*29+8.3)*.70+noise(bearing*68+3.1)*.30;
         float back=group*(.009+ridge*.029);
         float front=smoothstep(.46,.79,noise(bearing*8.1-10.2))*(.004+ridge*.022);
         float foot=smoothstep(-.004,.012,marineLine);
         float horizonDetail=max(.0007,fwidth(marineLine));
         float mountains=foot*(1-smoothstep(back-horizonDetail,back+horizonDetail,marineLine))*group;
         float headlands=foot*(1-smoothstep(front-horizonDetail,front+horizonDetail,marineLine));
         sky=lerp(sky,marineAir*float3(.68,.82,.90),mountains*.64);
         sky=lerp(sky,marineAir*float3(.51,.74,.85),headlands*.56);
     }
     if(depth>.08)
     {
         // Distant rays remain in the water even where the ocean mesh ends.
         float altitude=saturate(d.y);
         float rays=pow(saturate(dot(d,normalize(float3(sun.x,.26,sun.z)))),9);
         float streak=smoothstep(.39,.86,noise(d.xz/max(.07,altitude+.12)*20+_Time.y*.04));
         float3 underwater=lerp(float3(.006,.11,.22),float3(.008,.35,.47),smoothstep(-.22,.66,d.y));
         underwater+=float3(.044,.24,.29)*rays*streak;
         sky=lerp(sky,underwater,smoothstep(.08,.19,depth));
     }
     return sky;
 }
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 d=normalize(i.dir);if(_CoastDay>.5)return half4(CoastSky(d),1);float3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.4));float2 p=d.xz/max(.15,abs(d.y)+.18)*2.8;float n=noise(p)*.6+noise(p*2.1+5)*.27+noise(p*4.4)*.13;float cloud=smoothstep(1-_CloudCover-.12,1-_CloudCover+.13,n)*smoothstep(.02,.15,d.y)*(1-smoothstep(.75,1,d.y));float3 cloudColor=_Cloud.rgb*lerp(.8,1,n);c=lerp(c,cloudColor,cloud);float star=pow(hash(floor(d.xz/max(.15,d.y)*500)),150)*smoothstep(.08,.4,d.y)*(1-cloud);c+=star*_Stars*.65;float sun=dot(d,normalize(_SunDirection.xyz));c+=_SunColor.rgb*(pow(saturate(sun),800)*1.4+pow(saturate(sun),32)*.1);return half4(c,1);}
 ENDHLSL }
 }
}
