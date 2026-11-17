Shader "DeadlyJenga/Christmas Foil Paper"
{
    Properties
    {
        [MainTexture] _BaseMap("Fabric Color", 2D) = "white" {}
        [MainColor] _BaseColor("Fabric Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Fabric Normal", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 0.65
        _Sheen("Fiber Sheen", Range(0,1)) = 0.0
        _FoilColor("Gold Snowflake Foil", Color) = (0.95,0.64,0.20,1)
        _Smoothness("Pile Smoothness", Range(0,1)) = 0.18
        _Metallic("Paper Metallic", Range(0,1)) = 0
        _Coat("Lacquer Coat", Range(0,1)) = 0
        _CoatSmoothness("Lacquer Smoothness", Range(0,1)) = 0.9
        _FoilGlow("Foil Self Glow", Range(0,2)) = 0
        // Warm room light the lacquer picks up when you look across a face.
        [HDR] _GlossColor("Room Light On Lacquer", Color) = (0,0,0,1)
        _Sparkle("Glitter Strength", Range(0,4)) = 0
        _SparkleSize("Glitter Fleck Size", Range(0.02,1)) = 0.14
        _EdgeLight("Light Caught On Edges", Range(0,3)) = 0
        _TopLight("Extra Light On Top Faces", Range(0,1)) = 0
        // Paper colour that shows even in shadow; the power-cut code turns it off.
        [HDR] _EmissionColor("Colour Kept In Shadow", Color) = (0,0,0,1)
        [HideInInspector] _Cutoff("Cutout Threshold", Float) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _ZWrite("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "FabricForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex FabricVertex
            #pragma fragment FabricFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.5
            // The glossy lacquer layer over the paper.
            #define _CLEARCOAT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _FoilColor;
                half _BumpScale;
                half _Sheen;
                half _Smoothness;
                half _Metallic;
                half _Coat;
                half _CoatSmoothness;
                half _FoilGlow;
                half4 _EmissionColor;
                half4 _GlossColor;
                half _Sparkle;
                half _SparkleSize;
                half _EdgeLight;
                half _TopLight;
            CBUFFER_END

            struct FabricAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FabricVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float2 paper : TEXCOORD7;
                half3 vertexLighting : TEXCOORD4;
                half fog : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FabricVaryings FabricVertex(FabricAttributes input)
            {
                FabricVaryings output = (FabricVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertexPosition = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs basis = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = vertexPosition.positionCS;
                output.positionWS = vertexPosition.positionWS;
                output.normalWS = basis.normalWS;
                output.tangentWS = half4(basis.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                // Keep foil motifs the same size on broad lids and thin side panels.
                float4x4 model = GetObjectToWorldMatrix();
                float3 scale = float3(length(model._m00_m10_m20), length(model._m01_m11_m21), length(model._m02_m12_m22));
                float3 paperPosition = input.positionOS.xyz * scale;
                float3 face = abs(input.normalOS);
                float2 paperUV = face.y >= face.x && face.y >= face.z ? paperPosition.xz :
                    (face.x >= face.z ? paperPosition.zy : paperPosition.xy);
                output.uv = paperUV * (_BaseMap_ST.xy / 64.0) + _BaseMap_ST.zw;
                output.paper = paperUV;
                output.shadowCoord = GetShadowCoord(vertexPosition);
                output.vertexLighting = VertexLighting(vertexPosition.positionWS, basis.normalWS);
                output.fog = ComputeFogFactor(vertexPosition.positionCS.z);
                return output;
            }

            half4 FabricFragment(FabricVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 tangent = normalize(input.tangentWS.xyz);
                half3 bitangent = cross(n, tangent) * input.tangentWS.w;
                // The paper's fine texture repeats much smaller than the snowflake print.
                half3 pattern = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.paper * 0.11), _BumpScale);
                half3 normal = NormalizeNormalPerPixel(TransformTangentToWorld(pattern, half3x3(tangent, bitangent, n)));

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
                half3 paper = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half foil = smoothstep(0.035h, 0.25h, paper.r - paper.b);
                surface.albedo = lerp(_BaseColor.rgb * (0.90h + 0.10h * paper.g), _FoilColor.rgb, foil);
                surface.metallic = lerp(_Metallic, 0.8h, foil);
                surface.alpha = 1;
                surface.smoothness = lerp(_Smoothness, 0.84h, foil);
                surface.clearCoatMask = _Coat;
                surface.clearCoatSmoothness = _CoatSmoothness;
                surface.emission = surface.albedo * lerp(_EmissionColor.rgb, _FoilGlow * step(0.0001h, _EmissionColor.g), foil);
                surface.occlusion = 1;
                surface.normalTS = pattern;
                half4 result = UniversalFragmentPBR(inputData, surface);

                Light key = GetMainLight(inputData.shadowCoord);
                half grazing = 1.0h - saturate(dot(n, inputData.viewDirectionWS));
                half forwardScatter = saturate(dot(normal, key.direction) * 0.6h + 0.25h);
                half3 lint = lerp(surface.albedo, half3(0.85h, 0.72h, 0.58h), 0.36h);
                result.rgb += lint * key.color * key.shadowAttenuation * (grazing * grazing * forwardScatter * _Sheen);
                // Brighter tops, deeper undersides: gives each box a clear lit side.
                result.rgb *= 1.0h + _TopLight * (n.y * 0.5h - 0.12h);

                // Folded edges catch the light (the model's edges are slightly rounded).
                half3 bend = fwidth(n);
                half edge = saturate((bend.x + bend.y + bend.z) * 2.5h);
                result.rgb += surface.albedo * key.color * (edge * _EdgeLight) + half3(1.0h, 0.82h, 0.55h) * (edge * _EdgeLight * 0.12h);

                // Glitter: tiny flecks that flash on and off as you move your head.
                float2 fleck = input.paper / _SparkleSize;
                float2 cell = floor(fleck);
                half pick = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
                half turn = frac(pick * 91.7h + dot(inputData.viewDirectionWS, half3(pick * 7.0h + 3.0h, 5.0h - pick * 4.0h, 6.0h)) * 2.2h);
                float2 inCell = frac(fleck) - 0.5;
                half dotShape = saturate(1.0h - dot(inCell, inCell) * 9.0h);
                // Fade the flecks out before they get smaller than a pixel, so they never shimmer.
                half fine = saturate(1.6h - max(fwidth(fleck.x), fwidth(fleck.y)) * 1.6h);
                half flash = smoothstep(0.90h, 1.0h, turn) * step(0.72h, pick) * dotShape * fine;
                result.rgb += lerp(half3(1.0h, 0.9h, 0.7h), _FoilColor.rgb, 0.4h) * (flash * _Sparkle) * step(0.0001h, _EmissionColor.g) *
                    (0.35h + key.shadowAttenuation * 0.65h);

                // Lamps and tree lights sit low around the room, so the lacquer shines most
                // where its reflection points just above the horizon.
                half3 bounce = reflect(-inputData.viewDirectionWS, normal);
                half band = smoothstep(-0.12h, 0.22h, bounce.y) * (1.0h - smoothstep(0.4h, 0.95h, bounce.y));
                half across = 0.06h + 0.94h * grazing * grazing * grazing;
                // Mostly tinted by the paper, so the shine keeps the colour rich instead of milky.
                half3 shine = _GlossColor.rgb * lerp(half3(1, 1, 1), saturate(surface.albedo * 2.5h), 0.65h);
                result.rgb += shine * (band * across * _Coat) * step(0.0001h, _EmissionColor.g);
                result.rgb = MixFog(result.rgb, input.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
