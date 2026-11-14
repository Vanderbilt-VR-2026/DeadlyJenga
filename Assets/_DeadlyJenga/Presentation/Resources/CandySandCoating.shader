// Cinnamon-sugar sand coating drawn over a character after escaping sinking sand.
// World-height band from _CoatBottom (feet) to _CoatTop (about the waist) with a jagged, uneven top,
// thick near the feet and patchy higher up. _Coverage 1..0 dusts the grains off.
Shader "DeadlyJenga/Sand Coating"
{
    Properties
    {
        _CoatBottom("Coat bottom (world Y)", Float) = 0
        _CoatTop("Coat top (world Y)", Float) = 1
        _Coverage("Coverage", Range(0,1)) = 1
        _Seed("Seed", Float) = 0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+20" "RenderType"="TransparentCutout"}
        Pass
        {
            Name "SandCoating"
            Tags {"LightMode"="UniversalForward"}
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CoatBottom, _CoatTop, _Coverage, _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.1031 + _Seed * 0.013);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = lerp(Hash(i), Hash(i + float3(1,0,0)), f.x), b = lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x);
                float c = lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x), d = lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Sit the grains a hair above the body so they never z-fight with it.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * 0.006;
                output.positionWS = positionWS; output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // One rounded grain per 3D cell. Returns x = grain mask (0..1, soft edge), yzw = bump direction.
            float4 Grain(float3 p, float freq, float seedShift)
            {
                float3 q = p * freq + seedShift;
                float3 id = floor(q), f = frac(q) - 0.5;
                float3 jitter = float3(Hash(id + 1.7), Hash(id + 5.3), Hash(id + 9.1)) - 0.5;
                float3 d = f - jitter * 0.45;
                float r = 0.30 + 0.12 * Hash(id + 13.7);
                float len = length(d);
                return float4(1 - smoothstep(r - 0.10, r, len), d / r);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.positionWS;
                float span = max(0.05, _CoatTop - _CoatBottom);
                float t = (p.y - _CoatBottom) / span;
                // Jagged, imperfect top edge instead of a clean cutoff.
                float edge = 1 + (Noise(p * 6) - 0.5) * 0.32 + (Noise(p * 17) - 0.5) * 0.14;
                clip(edge - t);
                clip(t + 0.05);
                // Dense at the feet, patchy toward the waist; coverage dusts grains off.
                // Smooth noise (not a point-sampled grid) decides where the coat is, so edges stay soft.
                float density = lerp(1.0, 0.45, saturate(t)) * smoothstep(0.18, 0.55, Noise(p * 4.5 + 3.1));
                clip(density * _Coverage * 1.25 - Noise(p * 38 + 11.0) * 0.55 - 0.08);

                // Round sugar crystals and cinnamon clumps, each shaded like a tiny bump.
                float dist = distance(_WorldSpaceCameraPos, p);
                float detail = saturate(1 - dist / 7);               // fades to a smooth average from afar (no shimmer)
                float4 sugarG = Grain(p, 70, 0), spiceG = Grain(p, 46, 31.7);
                float clump = smoothstep(0.42, 0.72, Noise(p * 7 + 5.5));
                float spice = saturate(spiceG.x * (0.35 + clump * 0.75));
                half3 sugar = half3(0.97, 0.89, 0.72), cinnamon = half3(0.50, 0.26, 0.11), gap = half3(0.36, 0.20, 0.10);
                half3 albedo = lerp(gap, sugar, lerp(0.8, sugarG.x, detail));
                albedo = lerp(albedo, cinnamon * (0.85 + 0.3 * spiceG.y * 0.5 + 0.15), spice * lerp(0.55, 1, detail));
                albedo *= 0.92 + (Noise(p * 9) - 0.5) * 0.18;

                float3 n = normalize(input.normalWS);
                float3 bump = lerp(sugarG.yzw, spiceG.yzw, spice) * detail * 0.55;
                n = normalize(n + bump);
                Light light = GetMainLight();
                half diffuse = saturate(dot(n, light.direction)) * 0.8 + 0.2;
                half3 color = albedo * (light.color * diffuse + SampleSH(n) * 0.6);
                // Glints on a few crystals, like sugar catching the sun.
                float3 view = normalize(GetWorldSpaceViewDir(p));
                float sparkle = step(0.82, Hash(floor(p * 70) + 3.3)) * sugarG.x * detail;
                color += sparkle * pow(saturate(dot(n, normalize(light.direction + view))), 18) * light.color * 1.8;
                color = MixFog(color, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
