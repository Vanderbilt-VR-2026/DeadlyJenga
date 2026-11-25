Shader "DeadlyJenga/Caldera/Basalt"
{
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "CalderaNoise.cginc"
   struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 kind:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Data { float4 pos:SV_POSITION; float3 stone:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; float kind:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
   Data vert(Input v)
   {
    UNITY_SETUP_INSTANCE_ID(v); Data o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(v.vertex.xyz); o.pos=TransformWorldToHClip(o.world);
    o.stone=v.vertex.xyz*float3(length(unity_ObjectToWorld._m00_m10_m20),length(unity_ObjectToWorld._m01_m11_m21),length(unity_ObjectToWorld._m02_m12_m22));
    o.normal=TransformObjectToWorldNormal(v.normal);o.kind=v.kind.x;return o;
   }
   half4 frag(Data i):SV_Target
   {
    float3 n=normalize(i.normal);
    float2 p=float2(i.stone.x+i.stone.z*.63,i.stone.y+i.stone.z*.84-i.stone.x*.18);
    float grain=strata(p*2.8); float layers=strata(p*float2(.45,1.6));
    float shade=saturate(dot(n,normalize(float3(-.48,.84,-.54)))*.55+.5);
    // Rock kind from the mesh: 0 plum basalt, 1 blue-grey columns, 2 pale tuff,
    // 3 obsidian glass, 4 rust scoria, 5 magma-veined boulder.
    float3 dark=float3(.023,.011,.035), lit=float3(.29,.125,.21); float veins=1, glow=1;
    if(i.kind>.5&&i.kind<1.5){dark=float3(.020,.024,.032);lit=float3(.23,.25,.29);veins=.35;glow=.7;}
    else if(i.kind<2.5&&i.kind>1.5){dark=float3(.10,.085,.07);lit=float3(.56,.49,.40);veins=.12;glow=.45;}
    else if(i.kind<3.5&&i.kind>2.5){dark=float3(.004,.004,.007);lit=float3(.075,.07,.095);veins=.6;glow=.8;}
    else if(i.kind<4.5&&i.kind>3.5){dark=float3(.05,.016,.008);lit=float3(.42,.16,.07);veins=.7;glow=1.1;}
    else if(i.kind>4.5){dark=float3(.015,.008,.006);lit=float3(.17,.07,.05);veins=2.6;glow=1.6;}
    float3 rock=lerp(dark,lit,shade*(.38+layers*.8));
    rock*=.62+grain*.8;
    float fracture=abs(noise2(p*.7+layers*1.2)-.50);
    float vein=(1-smoothstep(.001,.018,fracture))*smoothstep(.47,.68,layers)*veins;
    float heat=(.18+1.3*exp(-max(0,i.world.y)*.17)+.6*saturate(-n.y))*glow;
    rock+=heat*(float3(.24,.036,.004)*(1-shade)+vein*float3(1.65,.25,.012));
    // Obsidian catches a sharp glassy highlight from the sky.
    if(i.kind>2.5&&i.kind<3.5) rock+=pow(saturate(dot(n,normalize(float3(-.3,.9,-.3)))),24)*float3(.35,.3,.4);
    return half4(rock,1);
   }
   ENDHLSL
  }
 }
}
