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

            float4 SpeedChase(float along, float depth, float phaseOffset)
            {
                float x = along - .5;
                float edgeFade = 1 - smoothstep(.073, .10, depth);
                float bed = SoftLine(depth - .004, .028) * saturate(1 - abs(x) / .42);
                float core = 0;
                float wake = .35 * bed * bed;
                float echo = 0;
                float clock = _Phase * .8 + phaseOffset;
                [unroll] for (int pulse = 0; pulse < 4; pulse++)
                {
                    float phase = frac(clock + pulse * .25);
                    float forward = 1 - pow(1 - phase, 2);
                    float lateral = x / lerp(.423, .17, forward);
                    float taper = saturate(1 - lateral * lateral);
                    float beat = 6.283185 * (clock + pulse * .23);
                    float crest = -.020 + .114 * forward + .010 * taper +
                        .004 * taper * sin(3.2 * lateral - beat);
                    float energy = taper * taper * smoothstep(0, .13, phase) * pow(1 - phase, .91);
                    float distance = depth - crest;
                    float width = lerp(.0060, .0024, forward);
                    float ripple = .78 + .22 * cos(9 * lateral + beat);
                    core += SoftLine(distance, width) * energy * ripple;
                    wake += SoftLine(distance + .006, width * 3.1) * energy;
                    echo += SoftLine(distance + .012 * (1 - .6 * forward), width * .55) * energy;
                }

                float strength = edgeFade * _Boosts.x;
                float glow = saturate(.52 * wake + .28 * echo);
                float flare = saturate(core * 1.50);
                float alpha = min(.90, glow + flare * .75) * strength;
                float3 color = lerp(float3(.016,.87,.10), float3(.65,1,.48), flare);
                return float4(color * alpha, alpha);
            }

            float4 SpeedPeriphery(float2 uv)
            {
                float horizontalDepth = min(uv.y, 1 - uv.y);
                float verticalDepth = min(uv.x, 1 - uv.x) * 1.5;

                // Map the closest edge to a shared chase, with no stacked glow in the corners.
                bool horizontal = horizontalDepth <= verticalDepth;
                float depth = min(horizontalDepth, verticalDepth);
                float along = horizontal ? uv.x : uv.y;
                float phase = horizontal ? (uv.y < .5 ? 0 : .10) : (uv.x < .5 ? .06 : .17);
                float4 result = 0;
                if (depth < .10 && abs(along - .5) < .43)
                    result = SpeedChase(along, depth, phase);
                return result;
            }

            float4 JumpAscent(float2 uv)
            {
                float right = step(.5, uv.x);
                float edge = min(uv.x, 1 - uv.x);
                float bend = .017 + .059 * uv.y * uv.y;
                float verticalFade = smoothstep(0,.085,uv.y) * (1 - smoothstep(.94,1,uv.y));
                float edgeFade = 1 - smoothstep(.084,.135,edge);
                float core = 0;
                float trail = SoftLine(edge - bend,.021) * .20;
                float violet = 0;
                float cyan = 0;
                float pink = 0;
                [unroll] for (int pulse = 0; pulse < 3; pulse++)
                {
                    float phase = frac(_Phase * .65 + pulse / 3.0 + right * .20);
                    float down = lerp(.06,1.11,phase) - uv.y;
                    float age = saturate(down / .24);
                    float wake = smoothstep(-.012,.035,down) * (1 - smoothstep(.10,.40,down));
                    float shimmer = smoothstep(0,.09,phase) * (1 - smoothstep(.86,1,phase));
                    float twist = sin(20 * down + phase * 6.283185 + pulse * 2.1);
                    float local = edge - bend + (.008 + .009 * twist) * age;
                    float wing = abs(local) - .027 * pow(age,.85);
                    float head = smoothstep(-.008,.012,down) * (1 - smoothstep(.045,.215,down));
                    float width = lerp(.0049,.0024,age);

                    // The arrowhead splits into three strands that trail down the same arc.
                    core += SoftLine(wing,width) * head * shimmer;
                    trail += SoftLine(local,.030) * wake * shimmer;
                    violet += SoftLine(local,.0070) * wake * shimmer;
                    cyan += SoftLine(local - .013 * age,.0047) * wake * shimmer;
                    pink += SoftLine(local + .015 * age,.0045) * wake * shimmer;
                }

                float strength = verticalFade * edgeFade * _Boosts.y;
                float glow = saturate(.40 * trail + .34 * violet);
                float flare = saturate(core * 1.35);
                float alpha = min(.91, glow + .69 * flare + .19 * (cyan + pink)) * strength;
                float3 color = lerp(float3(.43,.065,.93), float3(.89,.80,1), flare);
                color = lerp(color, float3(.30,.92,1), saturate(cyan) * (1 - flare) * .70);
                color = lerp(color, float3(1,.23,.77), saturate(pink) * (1 - flare) * .68);
                return float4(color * alpha, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.screen.xy / input.screen.w;
                float4 speed = 0;
                float4 jump = 0;
                if (_Boosts.x > 0) speed = SpeedPeriphery(uv);
                if (_Boosts.y > 0 && min(uv.x,1 - uv.x) < .135) jump = JumpAscent(uv);
                return jump + speed * (1 - jump.a);
            }
            ENDHLSL
        }
    }
}
