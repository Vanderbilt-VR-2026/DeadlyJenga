// The tree-skirt carpet under the Christmas tree. It is the deadly floor, so it should look
// cozy but hungry: deep yarn loops up close, a knitted Christmas border, and slow swirls
// where the pile sinks inward with a faint warm glow at the bottom.
// Cost: one floor draw with about six texture reads. Swirls are a short loop of math.
Shader "DeadlyJenga/Christmas Carpet"
{
    Properties
    {
        [MainTexture] _BaseMap("Carpet Color (far)", 2D) = "white" {}
        [MainColor] _BaseColor("Carpet Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _DetailMap("Yarn Loops (near)", 2D) = "white" {}
        [NoScaleOffset][Normal] _BumpMap("Yarn Normal", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 0.9
        _DetailSize("Yarn Tile Size (m)", Float) = 4.5
        _DetailFade("Yarn Fade Distance (m)", Float) = 30
        _Depth("Pile Depth (m)", Range(0,0.4)) = 0.12
        _Sheen("Fiber Sheen", Range(0,1)) = 0.3
        _Smoothness("Pile Smoothness", Range(0,1)) = 0.15
        _RugCircle("Rug Centre XZ, Radius", Vector) = (0,0,100,0)
        _Ivory("Knit Ivory", Color) = (0.95,0.86,0.72,1)
        _Pine("Knit Green", Color) = (0.08,0.42,0.2,1)
        [HDR] _Ember("Pit Glow", Color) = (1.6,0.45,0.12,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "CarpetForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex CarpetVertex
            #pragma fragment CarpetFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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

            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                float _DetailSize;
                float _DetailFade;
                half _Depth;
                half _Sheen;
                half _Smoothness;
                float4 _RugCircle;
                half4 _Ivory;
                half4 _Pine;
                half4 _Ember;
            CBUFFER_END

            // Set every frame by the art script: x, z, radius, strength (0..1).
            #define MAX_SWIRLS 8
            float4 _CarpetSwirls[MAX_SWIRLS];
            float _CarpetSwirlCount;

            struct CarpetAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct CarpetVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half3 vertexLighting : TEXCOORD3;
                half fog : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CarpetVaryings CarpetVertex(CarpetAttributes input)
            {
                CarpetVaryings output = (CarpetVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(p);
                output.vertexLighting = VertexLighting(p.positionWS, output.normalWS);
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            float2 Rotate(float2 v, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(c * v.x - s * v.y, s * v.x + c * v.y);
            }

            // How much the swirls pull at this spot (0..1) and how far the yarn is turned.
            void Swirls(float2 xz, out half pull, out float2 centre, out float twist, out float spin)
            {
                pull = 0; centre = xz; twist = 0; spin = 0;
                [loop] for (int i = 0; i < MAX_SWIRLS; i++)
                {
                    if (i >= (int)_CarpetSwirlCount)
                        break;
                    float4 s = _CarpetSwirls[i];
                    float k = saturate(1.0 - length(xz - s.xy) / max(s.z, 0.01));
                    k = k * k * (3.0 - 2.0 * k) * s.w;
                    if (k > pull)
                    {
                        pull = (half)k;
                        centre = s.xy;
                        twist = k * k * 4.0;
                        spin = k * k * 0.35;
                    }
                }
            }

            half4 CarpetFragment(CarpetVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 toEye = _WorldSpaceCameraPos - input.positionWS;
                float distance = length(toEye);
                half3 view = (half3)(toEye / max(distance, 1e-4));
                half near = saturate(1.0 - distance / _DetailFade);
                float2 xz = input.positionWS.xz;

                half pull; float2 centre; float twist; float spin;
                Swirls(xz, pull, centre, twist, spin);

                // Fake depth: shift the yarn picture with the view so loops look raised.
                float2 tileUV = xz / _DetailSize;
                half height = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, tileUV).r;
                height = saturate((height - 0.03h) * 3.5h);
                half depth = _Depth * (1.0h + 2.5h * pull) * near;
                float2 shift = view.xz / (view.y + 0.42h) * (height - 0.5h) * depth;
                float2 surface = xz + shift;

                // Inside a swirl the yarn is twisted around the centre and drifts round slowly.
                // Two phases blend so the pattern never smears out over time.
                half3 yarn; half3 bump;
                if (pull > 0.002h)
                {
                    float2 offset = surface - centre;
                    float phaseA = frac(_Time.y * 0.25);
                    float phaseB = frac(_Time.y * 0.25 + 0.5);
                    half weightA = 1.0h - abs(2.0h * phaseA - 1.0h);
                    float angleA = twist + spin * phaseA * 6.2831853;
                    float angleB = twist + spin * phaseB * 6.2831853 + 1.7;
                    float2 uvA = (centre + Rotate(offset, angleA)) / _DetailSize;
                    float2 uvB = (centre + Rotate(offset, angleB)) / _DetailSize + 0.37;
                    half3 yarnA = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uvA).rgb;
                    half3 yarnB = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uvB).rgb;
                    half3 bumpA = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvA), _BumpScale);
                    half3 bumpB = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvB), _BumpScale);
                    bumpA.xy = (half2)Rotate(bumpA.xy, -angleA);
                    bumpB.xy = (half2)Rotate(bumpB.xy, -angleB);
                    yarn = lerp(yarnB, yarnA, weightA);
                    bump = normalize(lerp(bumpB, bumpA, weightA));
                }
                else
                {
                    float2 uv = surface / _DetailSize;
                    yarn = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uv).rgb;
                    bump = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                }
                half loopTop = saturate((yarn.r - 0.03h) * 3.5h);

                // Far away the big painted weave; up close the small yarn loops.
                half3 far = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 albedo = lerp(far, yarn, near * 0.75h) * _BaseColor.rgb;
                half shade = clamp(dot(lerp(far, yarn, near), half3(0.6h, 0.3h, 0.1h)) / 0.07h, 0.35h, 1.6h);

                // Knitted tree-skirt border: ivory rings, a green band with an ivory zigzag,
                // and a ring of ivory snowflake dots.
                float2 fromCentre = xz - _RugCircle.xy;
                float ring = length(fromCentre) / _RugCircle.z;
                float around = atan2(fromCentre.y, fromCentre.x) / 6.2831853 + 0.5;
                half ivory = (half)(step(abs(ring - 0.885), 0.008) + step(abs(ring - 0.975), 0.007));
                half green = (half)step(abs(ring - 0.93), 0.034);
                float zig = abs(frac(around * 72.0) - 0.5) * 2.0;
                ivory += green * (half)step(abs(ring - (0.905 + 0.05 * zig)), 0.0065);
                float dotAngle = frac(around * 48.0) - 0.5;
                float2 dotSpot = float2(dotAngle * 6.2831853 * ring * _RugCircle.z / 48.0, (ring - 0.83) * _RugCircle.z);
                half snow = (half)step(length(dotSpot), 0.55) + (half)step(min(abs(dotSpot.x), abs(dotSpot.y)), 0.18) * (half)step(length(dotSpot), 1.5);
                ivory = saturate(ivory + snow * (half)step(abs(ring - 0.83), 0.03));
                albedo = lerp(albedo, _Pine.rgb * shade, green * (1.0h - ivory));
                albedo = lerp(albedo, _Ivory.rgb * shade, ivory);

                // Gaps between loops are dark.
                half gaps = lerp(1.0h, lerp(0.55h, 1.3h, loopTop), near);
                albedo *= gaps;

                // Swirl look, readable from far away: a raised lighter rim, spiral grooves
                // that crawl inward, and a wide funnel that goes almost black at the bottom.
                // The funnel darkens the final colour so no lighting term can lift it.
                half funnel = 1.0h;
                if (pull > 0.002h)
                {
                    float2 d = xz - centre;
                    float r = length(d);
                    float a = atan2(d.y, d.x);
                    half arms = (half)sin(a * 3.0 + sqrt(r) * 5.0 + _Time.y * 1.2);
                    half ridge = smoothstep(-0.3h, 0.7h, arms);
                    half pit = saturate(pull * 1.15h);
                    half rim = saturate(pull * 9.0h) * (1.0h - saturate((pull - 0.14h) * 7.0h));
                    half armMask = saturate(pull * 4.0h) * (1.0h - pit * 0.6h);
                    albedo *= 1.0h + 0.55h * rim;
                    albedo *= lerp(1.0h, lerp(0.35h, 1.2h, ridge), armMask);
                    funnel = lerp(1.0h, 0.03h, pit * pit * (3.0h - 2.0h * pit));
                }

                half3 n = normalize(input.normalWS);
                half3 detail = half3(bump.x, bump.z, bump.y);
                half3 normal = normalize(lerp(n, normalize(n + half3(detail.x, 0, detail.z)), near));

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                data.normalWS = normal;
                data.viewDirectionWS = view;
                data.shadowCoord = input.shadowCoord;
                data.vertexLighting = input.vertexLighting;
                data.bakedGI = SampleSH(normal);
                data.shadowMask = half4(1, 1, 1, 1);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.alpha = 1;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.normalTS = half3(0, 0, 1);
                // Only a faint warm hint in the grooves near the bottom; the very centre stays black.
                half core = saturate((pull - 0.6h) * 3.0h) * (1.0h - saturate((pull - 0.8h) * 5.0h));
                s.emission = _Ember.rgb * (core * 0.06h) * (1.0h - loopTop);
                half4 result = UniversalFragmentPBR(data, s);

                // Fuzz: loop tops catch light at grazing angles.
                Light key = GetMainLight(data.shadowCoord);
                half grazing = 1.0h - saturate(dot(n, view));
                half scatter = saturate(dot(normal, key.direction) * 0.6h + 0.3h);
                half3 lint = albedo * 1.8h + half3(0.04h, 0.0h, 0.01h);
                result.rgb += lint * key.color * key.shadowAttenuation * (grazing * grazing * scatter * _Sheen * (0.5h + loopTop));
                result.rgb *= funnel;
                result.rgb = MixFog(result.rgb, input.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
