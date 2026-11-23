Shader "DeadlyJenga/Caldera/Lava"
{
 SubShader { Tags { "RenderType"="Opaque" } Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "CalderaNoise.cginc"
 struct Data { float4 pos:SV_POSITION; float3 world:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
 Data vert(appdata_base v)
 {
    UNITY_SETUP_INSTANCE_ID(v); Data o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o;
 }
 half4 frag(Data i):SV_Target
 {
    float2 shore=i.world.xz;
    float2 drift=float2(.031,-.018)*_Time.y;
    float2 p=shore*float2(.14,.24)+drift;
    float bend=noise2(shore*.047+drift*.13);
    p.x+=2.7*bend;
    p.y+=1.4*noise2(shore.yx*.061-drift*.18);
    float2 cell=floor(p), uv=frac(p);
    float nearest=8, second=8;
    [unroll] for(int x=-1;x<=1;x++) [unroll] for(int y=-1;y<=1;y++)
    {
        float2 q=float2(x,y);
        float2 delta=q+.18+.64*float2(hash21(cell+q),hash21(cell+q+71))-uv;
        float d=dot(delta,delta); second=min(second,max(nearest,d)); nearest=min(nearest,d);
    }
    float gap=second-nearest;
    float flow=abs(noise2(float2(shore.x*.034+bend,shore.y*.067)-drift*.36)-.5);
    float current=1-smoothstep(.029,.11,flow);
    float melt=1-smoothstep(.028,.20,gap-current*.15);
    float grain=noise2(p*3.4);
    float3 crust=lerp(float3(.063,.012,.031),float3(.32,.035,.024),grain);
    float3 molten=lerp(float3(.83,.070,.007),float3(1.62,.33,.012),grain*.7+current*.3);
    float vein=(1-smoothstep(.004,.040+fwidth(gap),gap))*(.36+.64*current);
    float3 c=lerp(crust,molten,melt)+vein*float3(1.25,.75,.12);
    float haze=smoothstep(65,480,distance(i.world,_WorldSpaceCameraPos));
    return half4(lerp(c,float3(.56,.11,.13),haze*.92),1);
 }
 ENDCG
 } }
}
