Shader "DeadlyJenga/DuplexSurface"
{
    Properties
    {
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_local_fragment _ _DUPLEX_GLASS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                half4 finish : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 normalOS : TEXCOORD3;
                half4 color : TEXCOORD4;
                half4 finish : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.positionOS = input.positionOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalOS = input.normalOS;
                output.color = input.color;
                output.finish = input.finish;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half Twill(float2 point)
            {
                float2 cell = floor(point);
                float2 grain = frac(point);
                half across = step(2, fmod(cell.x + cell.y + 4096, 4));
                half cord = lerp(sin(grain.x * PI), sin(grain.y * PI), across);
                half contrast = saturate(1 - max(fwidth(point.x), fwidth(point.y)));
                return lerp(1, lerp(.79, 1.25, cord), contrast);
            }

            float Hash(float2 point)
            {
                float3 p = frac(float3(point.xyx) * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half3 CandyColor(float id)
            {
                half3 phase = half3(0, .667, .333);
                return .15 + .85 * saturate(abs(frac(id + phase) * 6 - 3) - 1);
            }

            half4 Splat(float2 position, float scale, half density)
            {
                float2 cell = floor(position * scale + .5);
                float2 grid = frac(position * scale + .5) - .5;
                float seed = Hash(cell);
                float2 shift = float2(Hash(cell + 12.71),Hash(cell - 21.9)) - .5;
                float2 delta = grid - shift * .32;
                float edge = (.18 + .27 * seed) * density;
                float broken = (Hash(floor(position * scale * 17.0)) - .5) * .12;
                half coverage = (1 - smoothstep(edge - .045, edge + .027, length(delta) + broken));
                return half4(CandyColor(Hash(cell + 5.9)), coverage * (.44 + .56 * seed));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 albedo = input.color.rgb;
                half opacity = input.color.a;
                half3 emission = albedo * input.finish.z;

                #if defined(_DUPLEX_GLASS)
                    half fresnel = pow(1 - saturate(abs(dot(normal, view))), 3);
                    opacity = lerp(opacity, .65, fresnel);
                    if (input.finish.w > .5)
                    {
                        float2 radial = float2(input.positionOS.x,
                            input.positionOS.y - (input.positionOS.y > 0 ? .06 : -.06));
                        float2 uv = float2(atan2(radial.y, radial.x) / TWO_PI * .22, input.positionOS.z);
                        half density = .5 + .5 * sin(uv.y * 25 + sin(uv.x * 50));
                        half4 large = Splat(uv, 19, lerp(.55, 1, density));
                        half4 small = Splat(uv + float2(.08, .017), 116, lerp(.34, .9, density));
                        half4 paint = large.a > small.a ? large : small;
                        albedo = lerp(albedo, paint.rgb, paint.a);
                        opacity = lerp(opacity, .9, paint.a);
                        emission += paint.rgb * paint.a * .30;
                    }
                #else
                    if (input.finish.w > .5)
                    {
                        half3 weight = abs(input.normalOS);
                        weight *= weight;
                        weight /= max(.001, dot(weight, half3(1,1,1)));
                        float3 tile = input.positionOS * 340;
                        half weave = dot(weight, half3(Twill(tile.yz), Twill(tile.xz), Twill(tile.xy)));
                        albedo *= weave;
                    }
                #endif

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = normal;
                data.viewDirectionWS = view;
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI = SampleSH(normal);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = input.finish.x;
                surface.smoothness = input.finish.y;
                surface.normalTS = half3(0,0,1);
                surface.emission = emission;
                surface.occlusion = 1;
                surface.alpha = opacity;
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                color.a = opacity;
                return color;
            }
            ENDHLSL
        }
    }
}
