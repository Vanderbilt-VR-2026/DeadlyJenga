// Campus palette: one opaque material for the whole campus.
// Each vertex stores its material ID in vertex color R (index / 255).
// The vertex shader looks that ID up in a 32x1 palette strip (pixel i = color of ID i, alpha = gloss boost).
// UV0 is kept for a future texture bake; UV1 is the lightmap UV.
Shader "DeadlyJenga/Campus Palette"
{
    Properties
    {
        [NoScaleOffset] _Palette("Palette strip (32x1)", 2D) = "gray" {}
        _Gloss("Candy gloss", Range(0,1)) = 0.35
        _Shininess("Gloss tightness", Range(4,128)) = 40
        _Ambient("Ambient strength", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_Palette); SAMPLER(sampler_Palette);
        float _CandyOutage; // set globally by the power-outage disaster visuals
        CBUFFER_START(UnityPerMaterial)
        half _Gloss, _Shininess, _Ambient;
        CBUFFER_END

        // Palette color for one vertex. Sampling the texel center returns that texel exactly.
        half4 PaletteColor(half4 vertexColor)
        {
            float index = clamp(round(vertexColor.r * 255.0), 0.0, 31.0);
            float2 uv = float2((index + 0.5) / 32.0, 0.5);
            return SAMPLE_TEXTURE2D_LOD(_Palette, sampler_Palette, uv, 0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 palette : TEXCOORD2;   // rgb = color, a = gloss boost
                float2 lightmapUV : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.palette = PaletteColor(v.color);
                #if defined(LIGHTMAP_ON)
                o.lightmapUV = v.staticLightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normalWS);
                half3 albedo = i.palette.rgb;

                // Sun: Lambert with realtime shadows.
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half ndl = saturate(dot(n, light.direction));
                half3 sun = light.color * (ndl * light.shadowAttenuation * light.distanceAttenuation);

                // Sky light: baked lightmap when there is one, otherwise the ambient probe.
                #if defined(LIGHTMAP_ON)
                half3 ambient = SampleLightmap(i.lightmapUV, n);
                #else
                half3 ambient = SampleSH(n);
                #endif

                half3 color = albedo * (ambient * _Ambient + sun);

                // Candy gloss: a cheap Blinn-Phong highlight. Palette alpha makes glass and water shinier.
                UNITY_BRANCH if (_Gloss > 0)
                {
                    half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                    half3 h = normalize(light.direction + viewDir);
                    half spec = pow(saturate(dot(n, h)), _Shininess) * _Gloss * i.palette.a;
                    color += sun * spec * 4.0h;
                }

                color *= saturate(1.0 - _CandyOutage * 0.94);
                return half4(MixFog(color, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
                #else
                float3 l = _LightDirection;
                #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, l));
                #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }

        // Only used when baking lightmaps: tells the baker each surface's palette color.
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };
            struct Varyings { float4 positionCS : SV_POSITION; half3 albedo : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = UnityMetaVertexPosition(v.positionOS.xyz, v.uv1, v.uv2);
                o.albedo = PaletteColor(v.color).rgb;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UnityMetaInput meta = (UnityMetaInput)0;
                meta.Albedo = i.albedo;
                meta.Emission = 0;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
