Shader "Candy Tropical/Pickup Hologram"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Glass ("Glass tint", Color) = (.033,.36,.27,.62)
        _Mint ("Mint bevel", Color) = (.29,1,.71,1)
        _Lime ("Lime bevel", Color) = (.77,1,.34,1)
        _Amber ("Swap key", Color) = (1,.67,.16,1)
        _Aspect ("Width / height", Float) = 3.444
        _Shape ("Panel, key, pointer", Float) = 0
        _Swap ("Swap key enabled", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Glass, _Mint, _Lime, _Amber;
                float _Aspect, _Shape, _Swap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float RoundedBox(float2 local, float2 halfSize, float radius)
            {
                float2 corner = abs(local) - halfSize + radius;
                return length(max(corner, 0)) + min(max(corner.x, corner.y), 0) - radius;
            }

            float Streak(float distance, float width)
            {
                float normalized = distance / width;
                return exp2(-normalized * normalized);
            }

            float Glint(float2 local)
            {
                float core = Streak(length(local), .033);
                float rays = Streak(local.x, .011) * Streak(local.y, .083) +
                    Streak(local.y, .011) * Streak(local.x, .11);
                return saturate(core + .46 * rays);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                if (_Shape > 1.5)
                {
                    float edge = max(abs(uv.x - .5) - (uv.y - .07) * .46, uv.y - .91);
                    float alpha = .75 * (1 - smoothstep(-.016, .016, edge));
                    return half4(lerp(_Mint.rgb, _Lime.rgb, uv.y) * alpha, alpha);
                }

                bool key = _Shape > .5;
                float2 local = (uv - .5) * float2(_Aspect, 1);
                float2 halfSize = float2(.5 * _Aspect - .022, .472);
                float radius = key ? .24 : .31;
                float distance = RoundedBox(local, halfSize, radius);
                float aa = max(fwidth(distance), .005);
                float coverage = 1 - smoothstep(-aa, aa, distance);
                float bevel = Streak(distance + .034, .036);
                float rim = Streak(distance + .014, .012);
                float innerRim = Streak(distance + .08, .015);

                float3 rimColor = lerp(_Mint.rgb, _Lime.rgb, smoothstep(.18, .88, uv.y));
                float contrast = Streak(local.x * .42, .56) * Streak(local.y + .02, .30);
                float3 glass = _Glass.rgb * (.68 + .30 * uv.y);
                float opacity = _Glass.a * lerp(.51, 1, contrast);
                float scan = .5 + .5 * sin(uv.y * 226);
                glass += _Mint.rgb * (.035 * scan + .047 * Streak(local.y - .28, .12));

                float gleam = Glint(local - float2(-halfSize.x + .23, .39)) +
                    Glint(local - float2(halfSize.x - .24, -.39));

                if (key)
                {
                    rimColor = lerp(_Lime.rgb, _Amber.rgb, _Swap);
                    glass = rimColor * lerp(.53, .91, uv.y);
                    opacity = .84;
                    gleam *= .67;
                }

                float shine = saturate(.50 * bevel + .38 * rim + .12 * innerRim);
                float3 color = lerp(glass, rimColor, shine);
                color = lerp(color, half3(1, 1, .94), saturate(.43 * rim + .86 * gleam));
                float alpha = coverage * saturate(opacity + .47 * bevel + .28 * rim + .44 * gleam);
                float glow = .18 * Streak(abs(distance), .045) * (1 - coverage);
                return half4(color * alpha + rimColor * glow, saturate(alpha + glow));
            }
            ENDHLSL
        }
    }
}
