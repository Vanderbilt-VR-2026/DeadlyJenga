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
        _CrystalMap("Fine crystal slopes and coverage", 2D) = "white" {}
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CoatBottom, _CoatTop, _Coverage, _Seed, _HasRest;
            CBUFFER_END

            TEXTURE2D(_CrystalMap); SAMPLER(sampler_CrystalMap);

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 rest : TEXCOORD3; float2 raised : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half fog : TEXCOORD2; float4 rest : TEXCOORD3; float2 raised : TEXCOORD4;
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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * 0.00018;
                output.raised = input.raised;
                output.rest = lerp(float4(input.positionOS.xyz, (positionWS.y - _CoatBottom) / max(.05, _CoatTop - _CoatBottom)), input.rest, _HasRest);
                output.positionWS = positionWS; output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
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
                // A physical 32 mm tile, blended in rest space so grains follow the skin.
                float3 restNormal = normalize(cross(ddx(input.rest.xyz), ddy(input.rest.xyz)));
                float3 weights = pow(abs(restNormal), 4);
                weights /= max(.0001, weights.x + weights.y + weights.z);
                float3 uv = p * 31.25;
                float4 cx = SAMPLE_TEXTURE2D(_CrystalMap, sampler_CrystalMap, uv.yz);
                float4 cy = SAMPLE_TEXTURE2D(_CrystalMap, sampler_CrystalMap, uv.zx);
                float4 cz = SAMPLE_TEXTURE2D(_CrystalMap, sampler_CrystalMap, uv.xy);
                float4 crystal = cx * weights.x + cy * weights.y + cz * weights.z;
                float3 slope = float3(0,cx.r*2-1,cx.g*2-1)*weights.x
                    + float3(cy.g*2-1,0,cy.r*2-1)*weights.y
                    + float3(cz.r*2-1,cz.g*2-1,0)*weights.z;
                float3 n = normalize(input.normalWS);
                float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
                float3 rx = cross(dy,n), ry = cross(n,dx);
                float determinant = dot(dx,rx);
                float dhx = dot(slope,ddx(input.rest.xyz)), dhy = dot(slope,ddy(input.rest.xyz));
                float3 gradient = (dhx*rx + dhy*ry) / (abs(determinant)+1e-10)*sign(determinant);
                n = normalize(n + gradient * 1.65 * (1-input.raised.x));
                float grains = lerp(crystal.a,1,input.raised.x);
                half3 albedo = lerp(half3(.64,.51,.34),half3(.96,.91,.78),crystal.b);
                albedo = lerp(albedo,half3(.88,.81,.65),input.raised.x);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndl = saturate(dot(n,light.direction));
                float occlusion = lerp(.63,1,grains);
                half3 color = albedo * (SampleSH(n)*occlusion + light.color * ndl * light.shadowAttenuation);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float nh = saturate(dot(n,normalize(view+light.direction)));
                float footprint = max(length(ddx(uv)),length(ddy(uv)));
                float resolved = 1-smoothstep(.02,.12,footprint);
                // Broaden tiny highlights as grains become smaller than a pixel.
                color += light.color * pow(nh,lerp(12,48,resolved)) * lerp(.10,.34,resolved);
                color += albedo * light.color * pow(saturate(dot(-view,light.direction)),3) * .06;
                float alpha = coverage * lerp(.22,.97,grains);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
