Shader "DeadlyJenga/Caldera/AshSky"
{
 SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "CalderaNoise.cginc"
 struct Data { float4 pos:SV_POSITION; float3 dir:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
 Data vert(appdata_base v)
 {
    UNITY_SETUP_INSTANCE_ID(v); Data o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos=UnityObjectToClipPos(v.vertex); o.dir=v.vertex.xyz; return o;
 }
 half4 frag(Data i):SV_Target
 {
    float3 d=normalize(i.dir);
    float altitude=max(0,d.y);
    float2 wind=float2(.006,-.002)*_Time.y;
    float2 p=d.xz/(altitude+.22);
    float broad=strata(p*float2(1.7,3.1)+wind);
    float folds=noise2(p*float2(6,13)+broad*2.6+wind);
    float banks=smoothstep(.37,.67,broad+.10*(folds-.5));
    float3 dusk=lerp(float3(.89,.17,.16),float3(.105,.024,.19),smoothstep(0,.43,altitude));
    float ember=exp(-abs(altitude-.065)*15)*(.4+.6*saturate(d.z));
    dusk+=float3(.44,.10,.032)*ember;
    float3 ash=lerp(float3(.30,.052,.17),float3(.062,.018,.112),smoothstep(.01,.45,altitude));
    float3 sky=lerp(dusk,ash,banks*(.43+.54*smoothstep(.008,.12,altitude)));
    sky+=float3(.29,.045,.085)*(1-abs(banks*2-1))*ember;
    return half4(sky,1);
 }
 ENDCG
 } }
}
