// Sand granules taking over the view as the eyes go under. _Level 0..1 is how far the eyes have sunk.
// One camera-attached quad: grains creep in from the bottom and edges, fall, then pack into solid sand.
Shader "DeadlyJenga/Sand Engulf"
{
    Properties { _Level("Level", Range(0,1)) = 0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent"}
        Pass
        {
            Name "SandEngulf"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Level;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            // Round grain per cell: x = mask, y = random value, z = bump.
            float3 Grain(float2 uv, float cells, float fall, float seed)
            {
                float2 q = uv * cells + float2(0, _Time.y * fall);
                float2 id = floor(q), f = frac(q) - 0.5;
                float2 jit = float2(Hash(id + seed), Hash(id + seed + 4.1)) - 0.5;
                float r = 0.30 + 0.14 * Hash(id + seed + 9.7);
                float2 d = f - jit * 0.4;
                float len = length(d);
                return float3(1 - smoothstep(r - 0.12, r, len), Hash(id + seed + 2.2), 1 - len / r);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv;
                float2 c = uv - 0.5;
                // How buried this pixel is: sand rises from the bottom, and creeps in from the sides.
                float fill = _Level * 1.45 - uv.y * 0.9 + dot(c, c) * 0.9 - 0.12;
                fill += (Hash(floor(uv * 6)) - 0.5) * 0.08;
                float3 far = Grain(uv, 34, 0.15, 1.0), near = Grain(uv, 15, 0.45, 7.0);
                // Thin fill: loose falling grains. Thick fill: packed sand.
                float loose = step(far.y * 0.5, fill * 1.1) * far.x * saturate(fill * 4 + 0.15);
                float chunk = step(near.y * 0.5, fill - 0.05) * near.x * saturate(fill * 3);
                float packed = smoothstep(0.35, 0.6, fill);
                float alpha = saturate(max(max(loose, chunk), packed));
                alpha *= saturate(_Level * 6);

                half3 sugar = half3(0.86, 0.70, 0.46), cinnamon = half3(0.52, 0.28, 0.12), deep = half3(0.30, 0.17, 0.09);
                half3 grains = lerp(sugar, cinnamon, step(0.72, near.y) * 0.8 + far.y * 0.25);
                grains *= 0.72 + 0.35 * saturate(near.z * chunk + far.z * loose);
                half3 color = lerp(grains, deep * (0.85 + far.y * 0.3), packed * saturate(_Level * 1.2));
                color *= lerp(1, 0.45, saturate((_Level - 0.7) * 3));   // dark as the light is lost
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
