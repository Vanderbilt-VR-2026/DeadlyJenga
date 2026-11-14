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
                // Dense at the feet, patchy toward the waist, with a few bare spots; coverage dusts grains off.
                float density = lerp(1.0, 0.45, saturate(t)) * smoothstep(0.18, 0.55, Noise(p * 4.5 + 3.1));
                float grain = Hash(floor(p * 160));
                clip(density * _Coverage * 1.25 - grain * 0.55 - 0.08);

                // Sugar base with darker cinnamon flecks and a little grainy shading.
                half3 sugar = half3(0.95, 0.86, 0.69), cinnamon = half3(0.56, 0.30, 0.14);
                float fleck = step(0.8, Hash(floor(p * 120) + 7.7));
                half3 albedo = lerp(sugar, cinnamon, fleck * 0.85 + Noise(p * 9) * 0.18) * (0.88 + grain * 0.22);

                float3 n = normalize(input.normalWS);
                Light light = GetMainLight();
                half diffuse = saturate(dot(n, light.direction)) * 0.8 + 0.2;
                half3 color = albedo * (light.color * diffuse + SampleSH(n) * 0.6);
                // Occasional glints, like sugar crystals catching the sun.
                float3 view = normalize(GetWorldSpaceViewDir(p));
                float glint = step(0.985, Hash(floor(p * 220) + 3.3)) * pow(saturate(dot(n, normalize(light.direction + view))), 24);
                color += glint * light.color * 1.6;
                color = MixFog(color, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
