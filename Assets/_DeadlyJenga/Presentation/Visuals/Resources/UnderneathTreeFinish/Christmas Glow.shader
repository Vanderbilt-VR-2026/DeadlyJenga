Shader "DeadlyJenga/Christmas Glow"
{
    // Soft additive halo for tree lights. Each quad's four corners sit at the light's position;
    // the vertex stage spreads them so the halo always faces the camera.
    Properties
    {
        _Strength("Strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "GlowForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex GlowVertex
            #pragma fragment GlowFragment
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Strength;
            CBUFFER_END

            struct GlowAttributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float2 size : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct GlowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                half4 color : COLOR;
                half core : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            GlowVaryings GlowVertex(GlowAttributes input)
            {
                GlowVaryings output = (GlowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                positionVS.xy += (input.corner - .5) * input.size.x;
                // Step toward the camera so the bulb itself does not hide its halo.
                positionVS.z += 1.2;
                output.positionCS = TransformWViewToHClip(positionVS);
                output.corner = input.corner;
                output.color = input.color;
                output.core = input.size.y;
                return output;
            }

            half4 GlowFragment(GlowVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half reach = saturate(1 - length(input.corner - .5) * 2);
                half glow = pow(reach, 2.5) * .42 + pow(reach, 10) * 1.6 * input.core;
                return half4(input.color.rgb * glow * _Strength, 1);
            }
            ENDHLSL
        }
    }
}
