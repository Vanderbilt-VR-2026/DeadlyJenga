Shader "Hidden/DeadlyJenga/Sprint Camera FX"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Sprint camera"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment SprintFragment
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _SprintBlend, _SprintPhase, _SprintAspect;

            float Random01(float index)
            {
                return frac(sin(index * 127.1 + 43.9) * 43758.5453);
            }

            float Streaks(float2 offset)
            {
                float radius = length(offset);
                float lane = (atan2(offset.y, offset.x) * (1.0 / 6.2831853) + .5) * 90.0;
                float index = floor(lane);
                float seed = Random01(index);
                float distance = abs(frac(lane) - lerp(.23, .77, seed)) * radius;
                float core = 1.0 - smoothstep(.025, .075, distance);
                float glow = 1.0 - smoothstep(.055, .20, distance);

                // Each streak travels outward at its own rate and restarts outside the visible envelope.
                float run = frac(radius * lerp(1.15, 2.45, seed) - _SprintPhase * lerp(.8, 1.85, seed)
                                 + Random01(index + 19.0));
                float head = smoothstep(.46, .83, run) * (1.0 - smoothstep(.84, .98, run));
                float tail = smoothstep(.22, .80, run) * (1.0 - smoothstep(.84, .99, run));
                return (core * head + glow * tail * .36) * lerp(.36, 1.0, seed) * step(.32, seed);
            }

            half4 SprintFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 offset = uv - .5;
                half4 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float edge = smoothstep(.47, 1.0, abs(offset.x) * 2.0);
                if (edge <= .001) return scene;

                float2 smear = offset * (.077 * edge * _SprintBlend);
                half3 blur = scene.rgb * .18;
                [unroll]
                for (int tap = 1; tap <= 4; ++tap)
                {
                    float2 shift = smear * (tap * .25);
                    half weight = tap == 1 ? .16 : tap == 2 ? .125 : tap == 3 ? .08 : .045;
                    blur += (SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - shift, 0).rgb
                           + SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + shift, 0).rgb) * weight;
                }
                scene.rgb = lerp(scene.rgb, blur, edge * _SprintBlend);

                float sides = smoothstep(.56, .94, abs(offset.x) * 2.0);
                float streaks = Streaks(offset * float2(_SprintAspect, 1.0)) * sides * _SprintBlend;
                half3 streakColor = lerp(half3(.30, .70, .91), half3(.60, .90, 1.0), saturate(scene.b));
                scene.rgb += streakColor * (streaks * .48);
                return scene;
            }
            ENDHLSL
        }
    }
}
