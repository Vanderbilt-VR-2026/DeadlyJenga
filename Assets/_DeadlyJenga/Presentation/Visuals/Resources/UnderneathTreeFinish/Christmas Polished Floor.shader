Shader "DeadlyJenga/Christmas Polished Floor"
{
    // Varnished floorboards. The room's reflection probe is sampled a few times along the
    // view direction, which smears lamps and windows into long streaks like a real polished floor.
    Properties
    {
        [MainTexture] _BaseMap("Wood Grain", 2D) = "white" {}
        [MainColor] _BaseColor("Wood Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Grain Normal", 2D) = "bump" {}
        _BumpScale("Grain Relief", Range(0,1)) = 0.08
        _Smoothness("Varnish Smoothness", Range(0,1)) = 0.8
        _Mirror("Reflection Strength", Range(0,4)) = 2.6
        _MirrorFacing("Reflection When Looking Down", Range(0,1)) = 0.22
        _Stretch("Streak Length", Range(0,0.4)) = 0.16
        _Blur("Reflection Blur", Range(0,1)) = 0.18
        _Seam("Gap Between Boards", Range(0.01,0.5)) = 0.24
        // Two coloured glows on the floor (lamplight and window light).
        // Lamp = where the light sits (xyz). Wash = centre on the floor (x, z) and its size.
        _GlowALamp("Glow A Lamp", Vector) = (0,40,0,0)
        _GlowAWash("Glow A Wash", Vector) = (0,0,100,0)
        [HDR] _GlowAColor("Glow A Colour", Color) = (0,0,0,1)
        _GlowBLamp("Glow B Lamp", Vector) = (0,40,0,0)
        _GlowBWash("Glow B Wash", Vector) = (0,0,100,0)
        [HDR] _GlowBColor("Glow B Colour", Color) = (0,0,0,1)
        _GlowPower("Glow Power (0 in a power cut)", Range(0,1)) = 1
        _Glint("Glint Strength", Range(0,8)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "FloorForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex FloorVertex
            #pragma fragment FloorFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Smoothness;
                half _Mirror;
                half _MirrorFacing;
                half _Stretch;
                half _Blur;
                half _Seam;
                float4 _GlowALamp;
                float4 _GlowAWash;
                half4 _GlowAColor;
                float4 _GlowBLamp;
                float4 _GlowBWash;
                half4 _GlowBColor;
                half _GlowPower;
                half _Glint;
            CBUFFER_END

            // One coloured glow: a soft wash of light on the wood plus a bright glint
            // that stretches toward whoever is looking, like light on real varnish.
            half3 FloorGlow(float4 lamp, float4 wash, half3 color, float3 position, half3 normal, half3 view,
                half3 wood, half seam)
            {
                float2 away = position.xz - wash.xy;
                half pool = exp(-dot(away, away) / (wash.z * wash.z));
                float3 toLamp = lamp.xyz - position;
                half3 halfway = normalize(normalize(toLamp) + view);
                half facing = saturate(dot(normal, halfway));
                half reach = 1.0h / (1.0h + dot(toLamp, toLamp) / (wash.z * wash.z * 1.5h));
                half glint = (pow(facing, 600.0h) * 5.0h + pow(facing, 110.0h) * 0.8h) * reach;
                // The varnish itself carries some of the colour, so blue light still shows on warm wood.
                return color * (pool * (wood * 2.2h + 0.045h * seam) + glint * _Glint * seam);
            }

            struct FloorAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                // Across, along, width and length of this board in room units.
                float4 board : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FloorVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 board : TEXCOORD3;
                half3 tint : TEXCOORD4;
                half fog : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                half3 vertexLighting : TEXCOORD7;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FloorVaryings FloorVertex(FloorAttributes input)
            {
                FloorVaryings output = (FloorVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.board = input.board;
                output.tint = input.color.rgb;
                output.shadowCoord = GetShadowCoord(position);
                output.vertexLighting = VertexLighting(position.positionWS, output.normalWS);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 FloorFragment(FloorVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 wood = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 grain = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                // The floor is flat, so the grain's sideways tilt maps straight onto the room's x and z.
                half3 normal = normalize(input.normalWS + half3(grain.x, 0, grain.y));

                // Dark, dull line where two boards meet.
                float edge = min(min(input.board.x, input.board.z - input.board.x),
                    min(input.board.y, input.board.w - input.board.y));
                half seam = smoothstep(0.0, _Seam, edge);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.vertexLighting = input.vertexLighting;
                inputData.bakedGI = SampleSH(normal);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = wood * _BaseColor.rgb * input.tint * lerp(0.3h, 1.0h, seam);
                surface.alpha = 1;
                surface.smoothness = _Smoothness * lerp(0.4h, 1.0h, seam) * (0.86h + 0.14h * wood.g);
                surface.occlusion = 1;
                surface.normalTS = half3(0, 0, 1);
                half4 result = UniversalFragmentPBR(inputData, surface);

                half3 view = inputData.viewDirectionWS;
                half3 toward = normalize(half3(view.x, 0.0001h, view.z));
                half3 mirror = 0;
                for (int i = 0; i < 6; i++)
                {
                    half tilt = (i / 5.0h - 0.5h) * 2.0h * _Stretch;
                    half3 tipped = normalize(normal + toward * tilt);
                    half weight = 1.0h - 0.5h * abs(tilt) / max(_Stretch, 0.001h);
                    mirror += GlossyEnvironmentReflection(reflect(-view, tipped), input.positionWS, _Blur, 1.0h,
                        inputData.normalizedScreenSpaceUV) * weight;
                }
                mirror /= 4.2h;
                half grazing = pow(1.0h - saturate(dot(normal, view)), 4.0h);
                half strength = lerp(_MirrorFacing, 1.0h, grazing) * _Mirror * seam;
                // Varnish over dark wood takes a little of the wood's colour.
                half3 warmth = lerp(half3(1, 1, 1), saturate(wood * 1.6h), 0.3h);
                result.rgb += mirror * warmth * strength;
                half3 woodTint = surface.albedo;
                result.rgb += _GlowPower * (
                    FloorGlow(_GlowALamp, _GlowAWash, _GlowAColor.rgb, input.positionWS, normal, view, woodTint, seam) +
                    FloorGlow(_GlowBLamp, _GlowBWash, _GlowBColor.rgb, input.positionWS, normal, view, woodTint, seam));
                result.rgb = MixFog(result.rgb, input.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
