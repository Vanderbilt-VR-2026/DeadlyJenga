Shader "DeadlyJenga/Christmas Flame"
{
    // Soft additive flame tongue for fire particles. The shape is drawn from the quad's UVs
    // (wide and round at the bottom, a thin licking tip at the top), so no texture is needed.
    // Particle colour sets the hue: yellow-white when young, orange, then deep red as it fades.
    Properties
    {
        _Strength("Strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlameForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex FlameVertex
            #pragma fragment FlameFragment
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Strength;
            CBUFFER_END

            struct FlameAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FlameVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FlameVaryings FlameVertex(FlameAttributes input)
            {
                FlameVaryings output = (FlameVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 FlameFragment(FlameVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half y = input.uv.y;
                half x = (input.uv.x - .5) * 2.0;
                // Teardrop: full width low down, narrowing to a fine tip.
                half width = lerp(.95, .06, pow(saturate(y), .8));
                half across = saturate(1.0 - abs(x) / max(width, .001));
                across = across * across * (3.0 - 2.0 * across);
                half along = smoothstep(0.0, .18, y) * (1.0 - smoothstep(.45, 1.0, y));
                half body = across * along;
                half core = pow(body, 3.0);
                half3 color = input.color.rgb * (body * .55 + core * 1.4) * input.color.a * _Strength;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
