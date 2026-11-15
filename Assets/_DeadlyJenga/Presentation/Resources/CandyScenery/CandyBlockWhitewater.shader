Shader "Candy Tropical/Block Whitewater"
{
    Properties
    {
        _FoamGrain("Whitewater grain", 2D) = "gray" {}
        _ImpactShape("Radius, energy, seed, age", Vector) = (1,1,1,0)
        _ImpactMotion("Horizontal direction", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+9" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FoamGrain); SAMPLER(sampler_FoamGrain);
            CBUFFER_START(UnityPerMaterial)
                float4 _ImpactShape, _ImpactMotion;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 layer : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 shape : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float scale = _ImpactShape.x, energy = _ImpactShape.y, seed = _ImpactShape.z, age = _ImpactShape.w;
                float angle = input.uv.x * TWO_PI;
                float t = input.uv.y;
                float layer = input.layer.x;
                float2 direction = float2(cos(angle), sin(angle));
                float lean = dot(direction, _ImpactMotion.xy);
                float lobes = .60 + .19 * sin(angle * 5 + seed + layer) +
                    .14 * cos(angle * 9 - seed + layer * 2.7) + .07 * sin(angle * 17 + seed);
                float radius, height, fade;

                if (layer < .5)
                {
                    float spread = 1 - exp2(-age * 2.2);
                    radius = scale * t * (.6 + spread * (2.25 + energy * 1.12)) * (1 + .25 * lean + (lobes - .6) * .28);
                    height = .034 + .032 * sin(angle * 7 + t * 11 - age * 5) * t;
                    fade = (1 - smoothstep(.9, 2.35, age)) * smoothstep(0, .14, age);
                }
                else
                {
                    float time = max(0, age - (layer - 1) * .07);
                    float rise = smoothstep(.015, .18, time);
                    float fall = 1 - smoothstep(.24, .84, time);
                    float flare = lerp(.4, 1.45, rise) + time * 1.5;
                    radius = scale * ((.34 + layer * .11) + t * flare + time * 1.0) *
                        (1 + .32 * lean + (lobes - .6) * .58);
                    float arc = pow(saturate(sin(t * 1.83)), 1.25);
                    height = .025 + scale * (.48 + energy * .98) * rise * fall * arc * lobes * (1 + .45 * lean);
                    fade = rise * (1 - smoothstep(.38, .93, time));
                }

                float3 world = TransformObjectToWorld(float3(0, 0, 0)) + float3(direction.x * radius, height, direction.y * radius);
                output.world = world;
                output.positionCS = TransformWorldToHClip(world);
                output.shape = float4(t, height / max(scale, .05), layer, fade);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float age = _ImpactShape.w, seed = _ImpactShape.z;
                float2 p = input.world.xz / _ImpactShape.x;
                float t = input.shape.x;
                half4 coarse = SAMPLE_TEXTURE2D(_FoamGrain, sampler_FoamGrain, p * .38 + float2(seed, -age * .15));
                half4 grit = SAMPLE_TEXTURE2D(_FoamGrain, sampler_FoamGrain,
                    p * float2(.96, .82) + float2(age * .26, input.shape.y * .48));
                float froth = coarse.r * .58 + grit.r * .28 + grit.b * .14;
                float alpha;
                float brightness;
                if (input.shape.z < .5)
                {
                    float edge = 1 - smoothstep(.65, 1, t + (coarse.r - .48) * .38);
                    float swirl = .10 * sin(t * 29 - age * 5 + coarse.r * 9);
                    alpha = smoothstep(.35, .67, froth + swirl) * edge * input.shape.w;
                    brightness = froth;
                }
                else
                {
                    // Break up the upper lip while keeping a dense core near the water.
                    float lip = 1 - smoothstep(.59, 1, t + (coarse.r - .46) * .5);
                    float fingers = smoothstep(.29 + t * .19, .61 + t * .17, froth);
                    alpha = lip * fingers * input.shape.w * lerp(1.08, .88, t);
                    brightness = saturate(froth + .22 * t);
                }
                half3 color = lerp(half3(.50,.78,.83), half3(.99,1,1), smoothstep(.20,.75,brightness));
                return half4(MixFog(color, input.fog), saturate(alpha));
            }
            ENDHLSL
        }
    }
}
