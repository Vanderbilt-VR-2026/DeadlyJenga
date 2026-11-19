// Sand granules taking over the view as the eyes go under. _Level 0..1 is how far the eyes have sunk.
// One camera-attached quad: grains creep in from the bottom and edges, fall, then pack into solid sand.
Shader "DeadlyJenga/Sand Engulf"
{
    Properties
    {
        _Level("Level", Range(0,1)) = 0
        _SandTex("Sand grains", 2D) = "gray" {}
    }
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

            TEXTURE2D(_SandTex);
            SAMPLER(sampler_SandTex);

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

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 blend = frac(p);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv;
                float time = _Time.y;
                float side = (uv.x - 0.5) * 2;
                float height = lerp(0.20, 0.83, _Level) + 0.28 * smoothstep(0.84, 1, _Level);
                float ridge = Noise(float2(uv.x * 9, time * 0.37));
                float fine = Noise(uv * 27 + float2(2.7, time * 0.9));
                float fill = height - uv.y + side * side * 0.23 + (ridge - 0.5) * 0.16 + (fine - 0.5) * 0.045;

                // Loose grains stream down across the edge before the packed sand blocks the view.
                half3 movingSand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex,
                    uv * 2.7 + float2(0.29 + time * 0.031, 0.16 + time * 0.18)).rgb;
                half3 settledSand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex,
                    uv * 2.15 + float2(0.13, 0.41 + time * 0.025)).rgb;
                half grain = dot(movingSand, half3(0.299, 0.587, 0.114));
                float packed = smoothstep(-0.035, 0.105, fill + (grain - 0.4) * 0.055);
                float loose = smoothstep(-0.155, 0.025, fill) * smoothstep(0.37, 0.68, grain);
                float alpha = lerp(loose * 0.78, 1, packed) * smoothstep(0.03, 0.19, _Level);

                half3 color = lerp(movingSand, settledSand, packed);
                half light = lerp(1, 0.37, smoothstep(0.52, 1, _Level) * lerp(0.58, 1, packed));
                return half4(color * light, alpha);
            }
            ENDHLSL
        }
    }
}
