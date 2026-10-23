Shader "Candy Tropical/Scenic Sky"
{
 Properties { _Zenith("Zenith",Color)=(.03,.37,.92,1) _Horizon("Horizon",Color)=(.48,.86,1,1) _Cloud("Cloud",Color)=(1,1,1,1) _CloudCover("Cloud cover",Float)=.44 _Stars("Stars",Float)=0 _SunDirection("Sun direction",Vector)=(0,1,0,0) _SunColor("Sun",Color)=(1,.94,.83,1) }
 SubShader { Tags {"Queue"="Background" "RenderType"="Background"} Cull Off ZWrite Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Zenith,_Horizon,_Cloud,_SunDirection,_SunColor;float _CloudCover,_Stars;
 struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 positionCS:SV_POSITION;float3 dir:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.dir=i.positionOS.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 d=normalize(i.dir);float3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.4));float2 p=d.xz/max(.15,abs(d.y)+.18)*2.8;float n=noise(p)*.6+noise(p*2.1+5)*.27+noise(p*4.4)*.13;float cloud=smoothstep(1-_CloudCover-.12,1-_CloudCover+.13,n)*smoothstep(.02,.15,d.y)*(1-smoothstep(.75,1,d.y));c=lerp(c,_Cloud.rgb*lerp(.8,1,n),cloud);float star=pow(hash(floor(d.xz/max(.15,d.y)*500)),150)*smoothstep(.08,.4,d.y)*(1-cloud);c+=star*_Stars*.65;float sun=dot(d,normalize(_SunDirection.xyz));c+=_SunColor.rgb*(pow(saturate(sun),800)*1.4+pow(saturate(sun),32)*.1);return half4(c,1);}
 ENDHLSL }
 }
}
