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
        _HasRest("Rest surface coordinates", Float) = 0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="TransparentCutout"}
        Pass
        {
            Name "SandCoating"
            Tags {"LightMode"="UniversalForward"}
            Cull Back ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CoatBottom, _CoatTop, _Coverage, _Seed, _HasRest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 rest : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half fog : TEXCOORD2; float4 rest : TEXCOORD3;
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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * 0.00065;
                output.rest = lerp(float4(input.positionOS.xyz, (positionWS.y - _CoatBottom) / max(.05, _CoatTop - _CoatBottom)), input.rest, _HasRest);
                output.positionWS = positionWS; output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Irregular faceted crystals. Derivatives average detail smaller than a pixel.
            float4 Crystal(float3 p, float frequency)
            {
                float3 q = p * frequency;
                float3 cell = floor(q);
                float3 random = float3(Hash(cell + 1.1), Hash(cell + 7.3), Hash(cell + 17.7));
                float3 d = frac(q) - .5 - (random - .5) * .3;
                float3 a = abs(d);
                float facet = max(max(a.x, a.y * 1.15), a.z * .87);
                facet = max(facet, (a.x + a.y + a.z) * .55);
                float radius = lerp(.22, .48, random.x);
                float footprint = max(length(ddx(q)), length(ddy(q)));
                float edge = max(.025, footprint * .65);
                float mask = 1 - smoothstep(radius - edge, radius + edge, facet);
                float resolved = 1 - smoothstep(.35, 1.4, footprint);
                float height = max(0, radius - facet) * mask;
                return float4(lerp(.55, mask, resolved), height * resolved, random.y, resolved);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.rest.xyz + _Seed * float3(.137, .271, .193);
                float t = input.rest.w;
                float edge = 1 + (Noise(p * 15) - .5) * .3;
                float patches = Noise(p * 9 + 3.1) * .65 + Noise(p * 23) * .35;
                float density = saturate((edge - t) * 3.5) * smoothstep(.2, .53, patches);
                float erosion = (1 - _Coverage) * .92;
                float boundary = density - erosion - .06;
                float aa = max(fwidth(boundary), .012);
                float coverage = smoothstep(-aa, aa, boundary) * smoothstep(-.07, -.03, t);
                clip(coverage - .015);
                float4 crystal = Crystal(p, 420);
                float4 cluster = Crystal(p + 1.3, 190);
                float grains = saturate(crystal.x * .78 + cluster.x * .32);
                float height = crystal.y * .0017 + cluster.y * .0012;
                float3 n = normalize(input.normalWS);
                float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
                float3 rx = cross(dy, n), ry = cross(n, dx);
                float determinant = dot(dx, rx);
                float3 gradient = (ddx(height) * rx + ddy(height) * ry) / (abs(determinant) + 1e-10) * sign(determinant);
                n = normalize(n - gradient * .65);
                half3 amber = half3(.66, .36, .115);
                half3 ivory = half3(.98, .87, .64);
                half3 albedo = lerp(amber, ivory, saturate(grains * .65 + crystal.z * .22));
                Light light = GetMainLight();
                float ndl = saturate(dot(n, light.direction));
                half3 color = albedo * (SampleSH(n) + light.color * (.18 + ndl * .82));
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float nh = saturate(dot(n, normalize(view + light.direction)));
                float glint = pow(nh, 72) * grains * crystal.w;
                float glaze = pow(nh, 28) * .2;
                color += light.color * (glint * .65 + glaze);
                float alpha = coverage * lerp(.40, .96, grains);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
