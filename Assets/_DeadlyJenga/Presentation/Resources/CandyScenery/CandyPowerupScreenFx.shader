Shader "Candy Tropical/Powerup Screen Effects"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Boosts ("Speed and jump strength", Vector) = (0,0,0,0)
        _Phase ("Unscaled time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Boosts;
            float _Phase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screen : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }

            float SoftLine(float distance, float width)
            {
                float q = distance / width;
                return exp2(-1.44 * q * q);
            }

            float4 SpeedChase(float2 uv)
            {
                float x = uv.x - .5;
                float bottom = 1 - smoothstep(.064, .10, uv.y);
                float bed = SoftLine(uv.y - .005, .024) * saturate(1 - abs(x) / .42);
                float core = 0;
                float glow = .17 * bed;
                [unroll] for (int lane = 0; lane < 5; lane++)
                {
                    float phase = frac(_Phase * .8 + lane * .2);
                    float forward = 1 - pow(1 - phase, 2.25);
                    float lateral = x / lerp(.415, .18, forward);
                    float taper = saturate(1 - lateral * lateral);
                    float sway = sin(6.283185 * (_Phase * .8 + lane * .23));
                    float crest = -.019 + .11 * forward + .010 * taper + .0035 * lateral * sway;
                    float energy = taper * taper * smoothstep(0, .13, phase) * pow(1 - phase, 1.35);
                    float distance = uv.y - crest;
                    core += SoftLine(distance, lerp(.0042, .0016, forward)) * energy;
                    glow += SoftLine(distance + .003, lerp(.017, .007, forward)) * energy;
                }

                float alpha = min(.78, (core * .86 + glow * .45) * bottom) * _Boosts.x;
                float3 tint = lerp(float3(.02,.88,.09), float3(.38,1,.34), saturate(core));
                return float4(tint * alpha, alpha);
            }

            float4 JumpAscent(float2 uv)
            {
                float right = step(.5, uv.x);
                float edge = min(uv.x, 1 - uv.x);
                float bend = .017 + .059 * uv.y * uv.y;
                float side = edge - bend;
                float verticalFade = smoothstep(0,.10,uv.y) * (1 - smoothstep(.91,1,uv.y));
                float edgeFade = 1 - smoothstep(.079,.135,edge);
                float core = 0;
                float fringe = 0;
                float trail = 0;
                float filament = 0;
                float cool = 0;
                float warm = 0;
                [unroll] for (int pulse = 0; pulse < 3; pulse++)
                {
                    float phase = frac(_Phase * .65 + pulse / 3.0 + right * .20);
                    float tip = lerp(.09,1.10,phase);
                    float down = tip - uv.y;
                    float follow = saturate(down / .20);
                    float spread = .024 * pow(follow,.86);
                    float profile = smoothstep(-.025,.012,down) * (1 - smoothstep(.05,.20,down));
                    float shimmer = smoothstep(0,.13,phase) * (1 - smoothstep(.78,1,phase));

                    // Two tapered strands meet at the tip of each rising pulse.
                    float wing = abs(side + .007 * follow) - spread;
                    core += SoftLine(wing,.0028) * profile * shimmer;
                    fringe += SoftLine(wing + sign(side) * .006,.0046) * profile * shimmer;
                    float afterglow = smoothstep(-.035,.020,down) * (1 - smoothstep(.03,.34,down));
                    trail += SoftLine(side + .006 * follow,.025) * afterglow * shimmer;
                }

                float opacity = verticalFade * edgeFade * _Boosts.y;
                float alpha = min(.72, .56 * core + .21 * fringe + .21 * trail) * opacity;
                float3 violet = lerp(float3(.30,.07,.70),float3(.74,.65,1),saturate(core));
                float3 prismatic = lerp(float3(.44,.71,1),float3(.98,.25,.80),step(0,side));
                float3 tint = lerp(violet,prismatic,saturate(fringe * .52));
                return float4(tint * alpha, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.screen.xy / input.screen.w;
                float4 speed = 0;
                float4 jump = 0;
                if (_Boosts.x > 0 && uv.y < .10 && abs(uv.x - .5) < .43) speed = SpeedChase(uv);
                if (_Boosts.y > 0 && min(uv.x,1 - uv.x) < .135) jump = JumpAscent(uv);
                return jump + speed * (1 - jump.a);
            }
            ENDHLSL
        }
    }
}
