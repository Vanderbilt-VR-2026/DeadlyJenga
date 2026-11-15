Shader "DeadlyJenga/Christmas Fabric"
{
    Properties
    {
        [MainTexture] _BaseMap("Fabric Color", 2D) = "white" {}
        [MainColor] _BaseColor("Fabric Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Fabric Normal", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 0.65
        _Sheen("Fiber Sheen", Range(0,1)) = 0.12
        _Smoothness("Pile Smoothness", Range(0,1)) = 0.18
        [HideInInspector] _Cutoff("Cutout Threshold", Float) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _ZWrite("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "FabricForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex FabricVertex
            #pragma fragment FabricFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define _ENVIRONMENTREFLECTIONS_OFF 1
            #define _SPECULARHIGHLIGHTS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Sheen;
                half _Smoothness;
            CBUFFER_END

            struct FabricAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FabricVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half3 vertexLighting : TEXCOORD4;
                half fog : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FabricVaryings FabricVertex(FabricAttributes input)
            {
                FabricVaryings output = (FabricVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs point = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs basis = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = point.positionCS;
                output.positionWS = point.positionWS;
                output.normalWS = basis.normalWS;
                output.tangentWS = half4(basis.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(point);
                output.vertexLighting = VertexLighting(point.positionWS, basis.normalWS);
                output.fog = ComputeFogFactor(point.positionCS.z);
                return output;
            }

            half4 FabricFragment(FabricVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 tangent = normalize(input.tangentWS.xyz);
                half3 bitangent = cross(n, tangent) * input.tangentWS.w;
                half3 pattern = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 normal = NormalizeNormalPerPixel(TransformTangentToWorld(pattern, half3x3(tangent, bitangent, n)));

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                data.normalWS = normal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = input.shadowCoord;
                data.vertexLighting = input.vertexLighting;
                data.bakedGI = SampleSH(normal);
                data.shadowMask = half4(1, 1, 1, 1);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                surface.alpha = 1;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.normalTS = pattern;
                half4 result = UniversalFragmentPBR(data, surface);

                Light key = GetMainLight(data.shadowCoord);
                half grazing = 1.0h - saturate(dot(n, data.viewDirectionWS));
                half forwardScatter = saturate(dot(normal, key.direction) * 0.6h + 0.25h);
                half3 lint = lerp(surface.albedo, half3(0.85h, 0.72h, 0.58h), 0.36h);
                result.rgb += lint * key.color * key.shadowAttenuation * (grazing * grazing * forwardScatter * _Sheen);
                result.rgb = MixFog(result.rgb, input.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
