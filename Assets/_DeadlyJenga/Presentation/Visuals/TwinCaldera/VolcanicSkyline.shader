Shader "DeadlyJenga/Caldera/VolcanicSkyline"
{
 SubShader { Tags { "RenderType"="Opaque" } Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "CalderaNoise.cginc"
 struct Data {float4 pos:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float3 normal:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO};
 Data vert(appdata_base v)
 {
    UNITY_SETUP_INSTANCE_ID(v); Data o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
    o.normal=UnityObjectToWorldNormal(v.normal); o.uv=v.texcoord.xy; return o;
 }
 half4 frag(Data i):SV_Target
 {
    float h=i.uv.y;
    float grain=noise2(i.world.xz*.19+i.world.y*.12);
    float light=saturate(dot(normalize(i.normal),normalize(float3(-.55,.5,-.6))));
    float3 c;
    if(h>1.5)
    {
        float embers=saturate(1-(h-2)*2.8);
        c=lerp(float3(.14,.045,.12),float3(.35,.11,.16),light*grain)
            +float3(.68,.12,.013)*embers*(.4+.6*grain);
    }
    else
    {
        float a=i.uv.x*6.283185;
        float river=abs(sin(5*a+.49*sin(3*a+h*10)+.24*sin(7*a-h*15)+grain*.19));
        float run=(1-smoothstep(.022,.10+fwidth(river),river))*smoothstep(.04,.48,h+.15*sin(3*a));
        float fire=max(run,smoothstep(.93,1,h));
        c=lerp(float3(.10,.025,.085),float3(.31,.083,.14),light*(.68+.32*grain));
        c+=fire*float3(2.1,.44,.025)*(.81+.19*sin(h*29+_Time.y*.47));
    }
    float haze=smoothstep(80,480,distance(i.world,_WorldSpaceCameraPos));
    return half4(lerp(c,float3(.52,.115,.165),haze*.58),1);
 }
 ENDCG
 } }
}
