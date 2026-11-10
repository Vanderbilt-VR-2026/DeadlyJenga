Shader "DeadlyJenga/Ninja/Water"
{
    Properties
    {
        _Deep("Deep", Color) = (0.01,0.018,0.035,1)
        _Sky("Reflected sky", Color) = (0.12,0.13,0.24,1)
        _Moon("Moon glints", Color) = (1,0.94,0.8,1)
        _MoonDir("Moon direction", Vector) = (0.4,0.32,0.86,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NinjaCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Deep, _Sky, _Moon; float4 _MoonDir;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p = i.positionWS.xz; float t = _Time.y;
                float2 slope = float2(cos(p.x * 0.31 + t * 0.9) * 0.6 + cos(p.x * 0.73 - p.y * 0.41 + t * 1.7) * 0.3, sin(p.y * 0.27 + t * 0.7) * 0.6 + sin(p.y * 0.81 + p.x * 0.33 - t * 1.3) * 0.3);
                slope += (float2(NinjaNoise(p * 1.7 + t * 0.6), NinjaNoise(p * 1.7 - t * 0.5 + 7.0)) - 0.5) * 0.9;
                float3 n = normalize(float3(slope.x * 0.09, 1.0, slope.y * 0.09));
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float3 r = reflect(-v, n);
                half fresnel = 0.03h + 0.97h * pow(1.0h - saturate(dot(n, v)), 5.0h);
                half3 sky = lerp(_Sky.rgb, _Sky.rgb * 0.35h, saturate(r.y * 2.0h));
                half3 col = lerp(_Deep.rgb, sky, fresnel);
                float3 moon = normalize(_MoonDir.xyz);
                float rm = saturate(dot(r, moon));
                half3 glint = lerp(_Moon.rgb, half3(1, 0.25, 0.15), _NinjaBlood);
                col += glint * (pow(rm, 900.0) * 3.0 + pow(rm, 90.0) * 0.35 + pow(rm, 12.0) * 0.05);
                return half4(NinjaFog(col, i.positionWS), 1);
            }
            ENDHLSL
        }
    }
}
