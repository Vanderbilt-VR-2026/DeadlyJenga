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
                float2 sheetUV : TEXCOORD3;
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
                float lobes = .62 + .21 * sin(angle * 5 + seed + layer * 1.7) +
                    .13 * cos(angle * 9 - seed + layer * 2.7) + .07 * sin(angle * 17 + seed);
                float radius, height, fade;

                if (layer < .5)
                {
                    float spread = 1 - exp2(-age * 3.5);
                    radius = scale * t * (.55 + spread * (2.55 + energy * 1.3)) *
                        (1 + .23 * lean + (lobes - .62) * .22);
                    float rimDistance = (t - .83) * 7;
                    height = .039 + .055 * exp(-rimDistance * rimDistance) * sin(angle * 7 + t * 14 - age * 6);
                    fade = (1 - smoothstep(1.02, 2.35, age)) * smoothstep(0, .095, age);
                }
                else
                {
                    float time = max(0, age - (layer - 1) * .043);
                    float rise = smoothstep(0, .14 + .026 * layer, time);
                    float fall = 1 - smoothstep(.21, .82 + .06 * layer, time);
                    float skirt = 1 - step(1.5, layer);
                    float curl = smoothstep(.59, 1, t);
                    float lift = sin(t * lerp(2.28, 2.85, skirt));
                    float throwOut = (.9 + energy * .54) * (1 - exp2(-time * 4));
                    float reach = lerp(1.6, 2.8, skirt) * throwOut;
                    radius = scale * (.34 + layer * .10 + time * (.9 + .65 * skirt) +
                        t * reach - curl * lerp(.34, .09, skirt) * rise) *
                        (1 + .28 * lean + (lobes - .62) * .67);
                    height = .035 + scale * lerp(1.65 + energy * 1.85, .71 + energy * .55, skirt) *
                        rise * fall * lift * lobes * (1 + .26 * lean);
                    fade = rise * (1 - smoothstep(.34 + .06 * skirt, .94, time));
                }

                float3 world = TransformObjectToWorld(float3(0, 0, 0)) + float3(direction.x * radius, height, direction.y * radius);
                output.world = world;
                output.sheetUV = input.uv;
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
                float alpha;
                half3 color;
                if (input.shape.z < .5)
                {
                    float rim = 1 - smoothstep(.01, .14, abs(t - .88 + (coarse.g - .5) * .10));
                    float edge = 1 - smoothstep(.69, 1, t + (coarse.r - .48) * .33);
                    float cells = coarse.r * .48 + grit.r * .30 + grit.b * .22;
                    float swirl = .11 * sin(t * 27 - age * 6 + coarse.r * 9);
                    float baseFoam = smoothstep(.31, .65, cells + swirl) * edge;
                    float front = rim * smoothstep(.26, .64, grit.r + coarse.r * .25) *
                        (1 - smoothstep(.39, 1.28, age));
                    alpha = max(baseFoam, front * .9) * input.shape.w;
                    color = lerp(half3(.59,.81,.87), half3(.99,1,1), smoothstep(.26,.68,cells + front * .28));
                }
                else
                {
                    float layer = input.shape.z;
                    // Streaks follow the face of the sheet as it folds back over the impact.
                    float2 sheet = input.sheetUV;
                    float warp = (coarse.r - .5) * .22;
                    half4 streak = SAMPLE_TEXTURE2D(_FoamGrain, sampler_FoamGrain,
                        float2(sheet.x * 8 + warp + seed + layer * .37, t * .53 - age * 1.05));
                    float strands = streak.r * .56 + grit.r * .25 + streak.b * .19;
                    float foam = smoothstep(.30 + t * .22, .61 + t * .10, strands);
                    float tear = 1 - smoothstep(.59, 1.01, t + (streak.g - .43) * .53);
                    float body = (1 - smoothstep(.32, .82, t)) * .66;
                    alpha = max(foam * tear, body) * input.shape.w;

                    float3 normal = normalize(cross(ddy(input.world), ddx(input.world)) + float3(0, 1e-5, 0));
                    float3 view = GetWorldSpaceNormalizeViewDir(input.world);
                    float diffuse = abs(dot(normal, normalize(float3(-.45, .83, .33))));
                    float grazing = pow(1 - saturate(abs(dot(normal, view))), 2.7);
                    float light = saturate(.17 + diffuse * .35 + grazing * .26 + foam * .53);
                    color = lerp(half3(.36,.66,.76), half3(1,1,1), light);
                }
                return half4(MixFog(color, input.fog), saturate(alpha));
            }
            ENDHLSL
        }
    }
}
