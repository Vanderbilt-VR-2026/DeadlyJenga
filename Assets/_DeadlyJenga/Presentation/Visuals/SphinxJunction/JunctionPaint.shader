Shader "DeadlyJenga/Sphinx Junction/Painted masonry"
{
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct Input {float4 p:POSITION; float3 n:NORMAL; float4 c:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct Vary {float4 pos:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float3 c:COLOR; float3 local:TEXCOORD2; float3 axis:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO};
 Vary vert(Input i) {Vary o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 o.w=TransformObjectToWorld(i.p.xyz); o.pos=TransformWorldToHClip(o.w); o.n=TransformObjectToWorldNormal(i.n); o.c=i.c.rgb; o.local=i.p.xyz; o.axis=abs(i.n); return o;}
 float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 float detailNoise(float2 p) {
 float2 cell=floor(p),f=frac(p); f=f*f*(3-2*f);
 return lerp(lerp(hash21(cell),hash21(cell+float2(1,0)),f.x),
             lerp(hash21(cell+float2(0,1)),hash21(cell+1),f.x),f.y);
 }
 float4 frag(Vary i):SV_Target {
 Light l=GetMainLight(TransformWorldToShadowCoord(i.w));
 float diffuse=saturate(dot(normalize(i.n),l.direction));
 float2 uv=i.axis.y>max(i.axis.x,i.axis.z)?i.local.xz:(i.axis.x>i.axis.z?i.local.zy:i.local.xy);
 float grain=detailNoise(uv*41);
 float broad=detailNoise(uv*1.2);
 float sandstone=step(.69,i.c.r)*step(.40,i.c.g)*step(i.c.b,i.c.g*.83);
 float timber=step(i.c.g*1.40,i.c.r)*(1-sandstone);
 float2 course=uv*float2(.64,1.28);
 course.x+=.5*fmod(floor(course.y),2.0);
 float2 edge=min(frac(course),1-frac(course));
 float groove=1-smoothstep(.005,max(.026,fwidth(course.y)*1.55),min(edge.x,edge.y));
 float stone=1+.085*(hash21(floor(course))-.5)+.065*(broad-.5)-.21*groove;
 float plank=uv.y*3.4;
 float woodGrain=sin(uv.y*61+4.2*detailNoise(float2(uv.x*.47,uv.y*7)));
 float wood=1+.051*woodGrain-.18*(1-smoothstep(.01,.07,min(frac(plank),1-frac(plank))));
 float3 pigment=pow(i.c,2.0)*lerp(1,stone,sandstone)*lerp(1,wood,timber)*(.965+.07*grain);
 float luminous=step(.97,i.c.r)*step(.84,i.c.g);
 return float4(pigment*(float3(.51,.42,.40)+l.color*diffuse*l.shadowAttenuation*.78+luminous*.6),1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
