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
                float3 q = mul(float3x3(.36,.48,-.8, -.8,.6,0, .48,.64,.6), p) * frequency;
                float3 cell = floor(q), local = frac(q);
                float nearest = 10, shade = 0;
                [unroll] for (int z = -1; z <= 1; z++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float3 offset = float3(x,y,z);
                    float3 id = cell + offset;
                    float3 random = float3(Hash(id + 1.1), Hash(id + 7.3), Hash(id + 17.7));
                    float3 d = offset + random - local;
                    float angle = random.z * 6.283185;
                    float sn, cs; sincos(angle, sn, cs);
                    d.xy = float2(cs*d.x - sn*d.y, sn*d.x + cs*d.y);
                    float3 a = abs(d) * lerp(float3(.8,1.2,1), float3(1.25,.85,1.1), random.x);
                    float facet = max(max(a.x, a.y), a.z);
                    facet = max(facet, (a.x + a.y + a.z) * .52);
                    facet /= lerp(.29, .49, random.z);
                    if (facet < nearest) { nearest = facet; shade = random.y; }
                }
                float footprint = max(length(ddx(q)), length(ddy(q)));
                float resolved = 1 - smoothstep(.45, 1.5, footprint);
                float edge = max(.08, footprint * .7);
                float mask = 1 - smoothstep(.86 - edge, 1 + edge, nearest);
                float height = max(0, 1 - nearest) * mask;
                return float4(lerp(.60, mask, resolved), height * resolved, shade, resolved);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.rest.xyz + _Seed * float3(.137, .271, .193);
                float t = input.rest.w;
                float edge = 1 + (Noise(p * 15) - .5) * .3;
                float patches = Noise(p * float3(27, 6, 27) + 3.1) * .65 + Noise(p * 43) * .35;
                float density = saturate((edge - t) * 4) * smoothstep(.16, .67, patches);
                float erosion = (1 - _Coverage) * .66;
                float boundary = density - erosion - .17 - Noise(p * 145) * .23 + (Noise(p * 410) - .5) * .17;
                float aa = max(fwidth(boundary), .012);
                float coverage = smoothstep(-aa, aa, boundary) * smoothstep(-.07, -.03, t);
                clip(coverage - .015);
                float4 crystal = Crystal(p, 380);
                float grains = crystal.x;
                float height = crystal.y * .0009;
                float3 n = normalize(input.normalWS);
                float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
                float3 rx = cross(dy, n), ry = cross(n, dx);
                float determinant = dot(dx, rx);
                float3 gradient = (ddx(height) * rx + ddy(height) * ry) / (abs(determinant) + 1e-10) * sign(determinant);
                n = normalize(n - gradient * .65);
                half3 amber = half3(.48, .245, .075);
                half3 ivory = half3(.95, .79, .48);
                half3 albedo = lerp(amber, ivory, saturate(grains * .70 + crystal.z * .15));
                Light light = GetMainLight();
                float ndl = saturate(dot(n, light.direction));
                half3 color = albedo * (SampleSH(n) + light.color * (.18 + ndl * .82));
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float nh = saturate(dot(n, normalize(view + light.direction)));
                float glint = pow(nh, 72) * grains * crystal.w;
                float glaze = pow(nh, 28) * .2;
                color += light.color * (glint * .65 + glaze);
                float alpha = coverage * .985;
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
