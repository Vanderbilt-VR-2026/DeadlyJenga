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
                UNITY_BRANCH if (_CastlePlasticSkyWeight <= 0)
                    return UniversalFragmentPBR(input, surface);

                half3 dye = surface.albedo;
                half dyeLuminance = dot(dye, half3(0.2126h, 0.7152h, 0.0722h));
                surface.albedo = saturate(lerp(dyeLuminance.xxx, dye, 1.19h)) * 0.78h;

                half3 reflected = reflect(-input.viewDirectionWS, input.normalWS);
                half grazing = 1 - saturate(dot(input.normalWS, input.viewDirectionWS));
                half gloss = saturate((surface.smoothness - 0.48h) * 2.4h);
                // Broaden the bright horizon on vertical faces so the coating reads at eye height.
                half horizon = 1 - smoothstep(0.0h, 0.47h, abs(reflected.y));
                half3 skyDirection = normalize(reflected + half3(0, 0.36h * horizon, 0));
                half mip = lerp(3.4h, 0.35h, gloss);
                half3 sky = SAMPLE_TEXTURECUBE_LOD(_CastlePlasticSky, sampler_CastlePlasticSky, skyDirection, mip).rgb;
                half lightness = dot(sky, half3(0.2126h, 0.7152h, 0.0722h));
                half cloud = smoothstep(0.26h, 0.76h, lightness);
                half sunAlignment = saturate(dot(reflected, GetMainLight().direction));
                half sun = pow(sunAlignment, lerp(14.0h, 38.0h, gloss));

                // Use the existing bevel normals for a soft rim on the molded edges.
                half3 face = abs(TransformWorldToObjectNormal(input.normalWS));
                half bevel = smoothstep(0.06h, 0.42h, face.x + face.y + face.z - max(face.x, max(face.y, face.z)));
                half3 coatTint = lerp(half3(1.0h, 0.95h, 0.91h), sqrt(dye), 0.31h);
                half facing = 0.46h + 0.48h * grazing * grazing;
                half glaze = cloud * facing + sun * 0.52h + bevel * (0.08h + cloud * 0.23h);
                glaze *= (0.25h + gloss * 0.75h) * _CastlePlasticSkyWeight;

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb += coatTint * glaze;
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
