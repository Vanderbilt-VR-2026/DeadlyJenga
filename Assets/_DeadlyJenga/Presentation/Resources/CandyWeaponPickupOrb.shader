Shader "Candy Tropical/Weapon Pickup Orb"
{
    Properties
    {
        _Mint ("Upper glow", Color) = (.29, .96, .80, 1)
        _Lime ("Lower glow", Color) = (.63, 1, .37, 1)
        _Opacity ("Shell strength", Range(0, 1)) = .75
        _Layer ("Shell phase", Float) = 0
        _Flow ("Powerup flow direction and speed", Vector) = (0,0,0,0)
        _Facets ("Powerup taper", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Mint, _Lime;
                float4 _Flow;
                float _Opacity, _Layer, _Facets;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 local = input.positionOS.xyz;
                if (_Facets > 0)
                {
                    float radial = max(length(local.xz), .025);
                    float taper = saturate((.5 - abs(local.y)) / radial);
                    float2 direction = local.xz / radial;
                    float ridge = .83 + .17 * abs(direction.x * direction.x - direction.y * direction.y);
                    local.xz *= lerp(1, taper * ridge, _Facets);
                }
                output.positionWS = TransformObjectToWorld(local);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normal, view));
                half rim = pow(1 - facing, 2.2);
                half innerHalo = pow(1 - facing, 4.5);
                float3 local = input.positionOS;
                half ripple = .5 + .5 * sin(12 * local.y + 8 * local.z + _Time.y * .52 + _Layer * 2);
                half fade = .78 + .22 * ripple;
                half upper = smoothstep(-.28, .29, local.y);
                half3 tint = lerp(_Lime.rgb, _Mint.rgb, upper);
                half3 glintDir = normalize(view + half3(-.48, .64, -.19));
                half glint = pow(saturate(dot(normal, glintDir)), 36) * (1 - _Layer * .48);
                tint = lerp(tint, half3(.94, 1, .92), saturate(.62 * glint + .14 * innerHalo));
                half alpha = _Opacity * (.03 + (.48 * rim + .17 * innerHalo) * fade + .34 * glint);
                if (_Flow.w > 0)
                {
                    float cycle = frac(dot(local, _Flow.xyz) * 1.8 - _Time.y * _Flow.w + _Layer * .14);
                    half wave = 1 - smoothstep(.02, .18, abs(cycle - .5));
                    half charge = wave * (.27 + .73 * rim);
                    tint = lerp(tint, half3(.92, 1, .96), charge * .62);
                    alpha = saturate(alpha + _Opacity * (.045 + .36 * charge));
                }
                return half4(tint, alpha);
            }
            ENDHLSL
        }
    }
}
