Shader "DeadlyJenga/Candy Vault Glow"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (.025,.22,1,.95)
        _Brightness("Brightness", Float) = 3.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _Brightness;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float rim = saturate(1 - abs(input.uv.y - .5) * 2);
                float shine = pow(rim, 12);
                float ends = smoothstep(0, .12, input.uv.x) * smoothstep(0, .12, 1 - input.uv.x);
                return half4(lerp(_Tint.rgb, half3(.2,.5,1), shine * .45) * _Brightness,
                    _Tint.a * ends * (rim * rim * .7 + shine * .3));
            }
            ENDHLSL
        }
    }
}
