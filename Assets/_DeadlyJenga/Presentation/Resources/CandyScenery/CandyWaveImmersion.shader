Shader "Candy Tropical/Wave Immersion"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Tsunami underwater"
            Cull Off ZTest Always ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _WaterWash; // submersion, surface turbulence, time, warp strength
            float _WaveHasDepth;

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * .1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Bubbles(float2 uv, float time)
            {
                float2 flow = uv - float2(time * .47, time * 2.7);
                float2 id = floor(flow);
                float seed = Hash(id);
                float2 center = float2(.26 + .48 * seed, .21 + .55 * Hash(id + 14));
                center.x += sin(time * 3 + seed * 12) * .11;
                float2 p = frac(flow) - center;
                p.x *= lerp(.78, 1.16, seed);
                float radius = lerp(.045, .16, seed);
                float d = length(p);
                float rim = smoothstep(radius - .06, radius - .02, d) *
                            (1 - smoothstep(radius, radius + .035, d));
                float shine = rim * saturate(.45 - p.x * 2 + p.y * 3);
                return (rim * .32 + shine * .6) * smoothstep(.70, .88, seed);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float t = _WaterWash.z;
                float wet = _WaterWash.x;
                float foam = _WaterWash.y;
                float2 p = uv * 2 - 1;
                float edge = saturate(dot(p,p) * .58);
                float2 distortion = sin(float2(uv.y * 26 + uv.x * 7 - t * 5,
                                               uv.x * 22 - uv.y * 9 + t * 4));
                distortion += .38 * sin(float2(uv.y * 49 + t * 8, uv.x * 43 - t * 7));
                float2 distortedUV = saturate(uv + distortion * _WaterWash.w *
                    (wet + foam * .38) * (1 - edge * .45));
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV);

                float visibility = .25;
                UNITY_BRANCH if (_WaveHasDepth > .5)
                {
                    float distance = LinearEyeDepth(SampleSceneDepth(distortedUV), _ZBufferParams);
                    visibility = lerp(.045, .75, exp2(-distance * .32));
                }
                visibility *= 1 - .35 * edge;
                float current = sin(uv.y * 21 + uv.x * 7 - t * 4 + distortion.x * .8);
                float ripples = .5 + .5 * sin(uv.y * 39 - uv.x * 16 - t * 6 + distortion.y);
                half3 water = lerp(half3(.006,.027,.036), half3(.013,.098,.106),
                    saturate(.71 + p.y * .2 - edge * .48 + current * .10 + ripples * .06));
                half luminance = dot(scene.rgb, half3(.25,.62,.13));
                half3 filtered = lerp(scene.rgb * half3(.31,.73,.74),
                    luminance * half3(.24,.62,.65), .40);
                half3 submerged = water + filtered * visibility;
                float bubbles = Bubbles(uv * float2(23, 14), t) +
                                .5 * Bubbles(uv * float2(39, 24) + .37, t * 1.36);
                submerged += bubbles * half3(.076,.19,.20);

                float froth = smoothstep(.18, .95, (.5 + .5 * current) * (.52 + .48 * ripples));
                float whitewater = saturate(foam * (.16 + .56 * froth + .42 * bubbles));
                half3 color = lerp(scene.rgb, submerged, wet);
                return half4(lerp(color, half3(.56,.78,.77), whitewater), scene.a);
            }
            ENDHLSL
        }
    }
}
