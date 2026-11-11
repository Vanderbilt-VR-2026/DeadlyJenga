Shader "DeadlyJenga/Ninja/Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.012,0.016,0.045,1)
        _Mid("Mid sky", Color) = (0.035,0.045,0.11,1)
        _Horizon("Horizon", Color) = (0.16,0.15,0.26,1)
        _Glow("Moon glow", Color) = (0.55,0.62,0.85,1)
        _MoonColor("Moon", Color) = (1,0.96,0.86,1)
        _BloodColor("Blood moon", Color) = (1,0.22,0.12,1)
        _MoonDir("Moon direction", Vector) = (0.4,0.32,0.86,0)
        _MoonSize("Moon radius", Float) = 0.085
        _Clouds("Clouds", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NinjaCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Zenith, _Mid, _Horizon, _Glow, _MoonColor, _BloodColor;
            float4 _MoonDir; float _MoonSize; half _Clouds;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.dir = i.positionOS.xyz; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dir);
                float3 moon = normalize(_MoonDir.xyz);
                float md = dot(d, moon), y = d.y, blood = _NinjaBlood;
                half3 glow = lerp(_Glow.rgb, _BloodColor.rgb * 0.8, blood);
                half3 horizon = lerp(_Horizon.rgb, half3(0.3, 0.07, 0.08), blood * 0.7);
                half3 col = lerp(horizon, _Mid.rgb, smoothstep(0.0, 0.28, y));
                col = lerp(col, _Zenith.rgb, smoothstep(0.22, 0.9, y));
                float2 flatD = normalize(d.xz + 1e-4), flatM = normalize(moon.xz);
                float side = saturate(dot(flatD, flatM) * 0.5 + 0.5);
                col += glow * pow(side, 5.0) * exp(-abs(y - 0.02) * 7.0) * 0.45;
                float3 cell = floor(d * 260.0); float h = NinjaHash(cell);
                float star = step(0.987, h) * saturate(1.0 - length(frac(d * 260.0) - 0.5) * 3.2);
                star *= (0.55 + 0.45 * sin(_Time.y * (1.5 + h * 6.0) + h * 50.0)) * smoothstep(0.03, 0.3, y) * (1.0 - smoothstep(0.8, 0.98, md));
                col += star * half3(0.75, 0.82, 1.0);
                float halo = exp(-max(0.0, 1.0 - md) * 140.0) * 0.55 + exp(-max(0.0, 1.0 - md) * 18.0) * 0.22;
                col += glow * halo;
                float edge = 1.0 - cos(_MoonSize);
                float k = saturate((1.0 - md) / edge);
                float disk = 1.0 - smoothstep(0.88, 1.0, k);
                float3 up = normalize(cross(moon, float3(0, 1, 0)));
                float2 local = float2(dot(d, up), dot(d, cross(up, moon))) / _MoonSize;
                float mare = NinjaNoise(local * 2.3 + 4.0) * 0.6 + NinjaNoise(local * 5.1 + 9.0) * 0.4;
                half3 moonColor = lerp(_MoonColor.rgb, _BloodColor.rgb, blood) * (0.82 + 0.18 * sqrt(saturate(1.0 - k))) * (1.0 - smoothstep(0.45, 0.8, mare) * 0.22);
                col = lerp(col, moonColor, disk);
                float2 cuv = d.xz / (y + 0.18);
                cuv.x += _Time.y * 0.006;
                float c = NinjaNoise(cuv * float2(1.1, 3.2)) * 0.65 + NinjaNoise(cuv * float2(2.7, 7.5)) * 0.35;
                c = smoothstep(0.52, 0.78, c) * smoothstep(-0.02, 0.1, y) * (1.0 - smoothstep(0.3, 0.6, y)) * _Clouds;
                half3 cloud = lerp(_Zenith.rgb * 0.7, glow * 0.85, saturate(pow(saturate(md), 30.0) + 0.08));
                col = lerp(col, cloud, c * 0.88);
                col = lerp(col, horizon * 0.75, saturate(-y * 10.0));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
