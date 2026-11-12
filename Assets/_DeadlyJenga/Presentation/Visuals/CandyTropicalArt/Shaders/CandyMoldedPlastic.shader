Shader "Candy Tropical/Molded Plastic"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.85
        _MetallicGlossMap("Surface Finish", 2D) = "white" {}
        [HideInInspector] _Metallic("Metallic", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 250

        Pass
        {
            Name "CandyForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURECUBE(_CastlePlasticSky);
            SAMPLER(sampler_CastlePlasticSky);
            half _CastlePlasticSkyWeight;

            half4 LitWithMoldedFinish(InputData input, SurfaceData surface)
            {
                half4 color = UniversalFragmentPBR(input, surface);
                UNITY_BRANCH if (_CastlePlasticSkyWeight > 0)
                {
                    half3 reflected = reflect(-input.viewDirectionWS, input.normalWS);
                    half grazing = 1 - saturate(dot(input.normalWS, input.viewDirectionWS));
                    half gloss = saturate((surface.smoothness - 0.55h) * 2.6h);
                    half mip = lerp(4.4h, 0.4h, gloss);
                    half3 sky = SAMPLE_TEXTURECUBE_LOD(_CastlePlasticSky, sampler_CastlePlasticSky, reflected, mip).rgb;
                    half luminance = dot(sky, half3(0.2126h, 0.7152h, 0.0722h));
                    // The plastic keeps its dyed body color. Only the brighter sky features read as sheen.
                    half cloud = smoothstep(0.095h, 0.58h, luminance);
                    half sunAlignment = saturate(dot(reflected, GetMainLight().direction));
                    half sunlight = pow(sunAlignment, lerp(12.0h, 40.0h, gloss));
                    half highlight = cloud * 0.78h + sunlight * 0.36h;
                    half fresnel = 0.32h + 0.62h * grazing * grazing;
                    half3 tint = lerp(half3(1.0h, 0.94h, 0.88h), sqrt(saturate(surface.albedo)), 0.37h);
                    color.rgb += tint * highlight * fresnel * gloss * _CastlePlasticSkyWeight;
                }
                return color;
            }

            #define UniversalFragmentPBR LitWithMoldedFinish
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }

    FallBack "Universal Render Pipeline/Lit"
}
