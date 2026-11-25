Shader "DeadlyJenga/Caldera/AshPlume"
{
    // A soft, rolling ash column. Each plume is a vertical strip that turns to face the
    // camera: uv.x runs across (-1..1), uv.y up the column (0..1), uv2 = (half width, glow).
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CalderaNoise.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float2 shape:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Data { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 shape:TEXCOORD1; float fog:TEXCOORD2; float seed:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Data vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v); Data o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 axis=TransformObjectToWorld(v.vertex.xyz);
                float3 toCam=_WorldSpaceCameraPos-axis; toCam.y=0;
                float3 side=normalize(cross(float3(0,1,0),normalize(toCam+float3(.001,0,0))));
                float3 world=axis+side*v.uv.x*v.shape.x;
                float3 d=world-_WorldSpaceCameraPos; float len=length(d);
                float pulled=len>400?400+(len-400)*.45:len;
                world=_WorldSpaceCameraPos+d*(pulled/max(len,.001));
                o.pos=TransformWorldToHClip(world);
                o.uv=v.uv; o.shape=v.shape; o.fog=ComputeFogFactor(o.pos.z);
                o.seed=axis.x*.013+axis.z*.007;
                return o;
            }

            half4 frag(Data i):SV_Target
            {
                float t=i.uv.y, x=i.uv.x;
                float rise=_Time.y*.035;
                float2 p=float2(x*1.6+i.seed*40,t*5-rise*5);
                float billow=strata(p*1.3+strata(p*.7+7)*1.6);
                float fine=strata(p*4.2-rise*3);
                // Ragged edges: the column's width is eaten by the billows.
                float edge=1-smoothstep(.35+.45*billow,1.05,abs(x));
                float body=edge*smoothstep(0,.06,t)*(1-smoothstep(.72,1,t));
                float density=saturate(body*(.55+billow*.9)*(.8+fine*.35)-.12);
                float lit=saturate(.35+billow*.7+t*.25-(x+1)*.12);
                float3 smoke=lerp(float3(.035,.03,.03),float3(.33,.27,.25),lit);
                // Crater fire lights the base of the column from below.
                smoke+=i.shape.y*float3(1.6,.34,.05)*exp(-t*7)*(.6+.6*billow);
                smoke=lerp(smoke,float3(.42,.13,.11),.25);
                return half4(MixFog(smoke,i.fog),density*.92);
            }
            ENDHLSL
        }
    }
}
