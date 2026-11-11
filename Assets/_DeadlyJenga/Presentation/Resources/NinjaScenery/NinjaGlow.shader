Shader "DeadlyJenga/Ninja/Glow"
{
    Properties
    {
        _Intensity("Intensity", Range(0,4)) = 1
        _NearFade("Near fade", Float) = 1.5
        _FarFade("Far fade", Float) = 400
        _FogDim("Fog dimming", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
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
            half _Intensity, _NearFade, _FarFade, _FogDim;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 data : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; half shape : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 center = TransformObjectToWorld(i.positionOS.xyz);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 p = i.data.y > 0.5 ? center + (right * i.uv.x + up * i.uv.y) * i.data.x : center;
                o.positionCS = TransformWorldToHClip(p);
                float dist = distance(center, GetCameraPositionWS());
                float fade = smoothstep(_NearFade, _NearFade * 2.5 + 0.5, dist) * (1.0 - smoothstep(_FarFade * 0.7, _FarFade, dist));
                float far = saturate((dist - _NinjaFogParams.x) / max(1.0, _NinjaFogParams.y - _NinjaFogParams.x));
                float flicker = 1.0 + (0.1 * sin(_Time.y * 9.0 + i.data.z) + 0.06 * sin(_Time.y * 23.0 + i.data.z * 3.0)) * step(0.001, i.data.z);
                o.color = i.color * (fade * flicker * _Intensity * (1.0 - far * _FogDim));
                o.uv = i.uv; o.shape = i.data.w;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half r2 = dot(i.uv, i.uv);
                half radial = saturate(1.0h - r2); radial *= radial;
                half core = exp(-r2 * 14.0h) * 0.6h;
                half beam = (1.0h - abs(i.uv.x)) * (1.0h - abs(i.uv.x)) * saturate(i.uv.y * 0.5h + 0.5h);
                half a = i.shape < 0.5h ? radial * 0.55h + core : i.shape < 1.5h ? radial * radial : beam;
                return half4(i.color.rgb * i.color.a * a, 0);
            }
            ENDHLSL
        }
    }
}
