// Low-poly sand cave-in drawn over the ground around a sinking block.
// Flat-shaded facets with vertex colors (dark cavity, lighter rim) fading out at the edge.
Shader "DeadlyJenga/Sand Cave-in"
{
    Properties { _Fade("Fade", Range(0,1)) = 1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent"}
        Pass
        {
            Name "CaveIn"
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float4 color : COLOR; half fog : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                half3 lit = light.color * saturate(dot(n, light.direction)) * 0.85 + SampleSH(n) * 0.75;
                half3 color = MixFog(i.color.rgb * lit, i.fog);
                return half4(color, i.color.a * _Fade);
            }
            ENDHLSL
        }
    }
}
