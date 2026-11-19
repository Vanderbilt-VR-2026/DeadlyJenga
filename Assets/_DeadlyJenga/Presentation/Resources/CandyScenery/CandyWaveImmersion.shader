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

            float Current(float2 p)
            {
                float2 cell = floor(p);
                float2 blend = frac(p);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
            }

            float Bubbles(float2 uv, float time)
            {
                float2 flow = uv - float2(time * .31, time * 1.6);
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
                return (rim * .24 + shine * .48) * smoothstep(.87, .96, seed);
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
                float2 flow = uv * float2(5.3, 4.1) + float2(t * .19, -t * .27);
                float current = Current(flow);
                float ripples = Current(flow * 1.93 + float2(-t * .13, t * .11) + 17.3);
                float2 distortion = float2(current, ripples) * 2 - 1;
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
                half3 water = lerp(half3(.006,.027,.036), half3(.013,.098,.106),
                    saturate(.71 + p.y * .2 - edge * .48 + (current - .5) * .12));
                half luminance = dot(scene.rgb, half3(.25,.62,.13));
                half3 filtered = lerp(scene.rgb * half3(.31,.73,.74),
                    luminance * half3(.24,.62,.65), .40);
                half3 submerged = water + filtered * visibility;
                float2 bubbleUV = uv + distortion * .012;
                float bubbles = Bubbles(bubbleUV * float2(17, 11), t) +
                                .4 * Bubbles(bubbleUV * float2(29, 18) + .37, t * 1.36);
                submerged += bubbles * half3(.076,.19,.20);

                float froth = smoothstep(.34, .82, current * (.52 + .48 * ripples));
                float whitewater = saturate(foam * (.16 + .56 * froth + .42 * bubbles));
                half3 color = lerp(scene.rgb, submerged, wet);
                return half4(lerp(color, half3(.56,.78,.77), whitewater), scene.a);
            }
            ENDHLSL
        }
    }
}
