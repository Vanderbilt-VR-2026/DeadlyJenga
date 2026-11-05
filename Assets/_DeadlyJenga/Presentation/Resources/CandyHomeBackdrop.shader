Shader "DeadlyJenga/Candy Home Backdrop"
{
    Properties
    {
        _Slide("Blurred slide", 2D) = "gray" {}
        _Fade("Slide dissolve", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _Slide;
            float3 _Forward, _Right;
            half _Fade;

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                float depth = max(.35, dot(direction, _Forward));
                // Match the 3 m panel, then mirror the art past its edges.
                float2 uv = .5 + float2(dot(direction, _Right) / 1.28, direction.y / .72) / (2 * depth);
                uv = 1 - abs(frac(uv * .5) * 2 - 1);
                half3 color = tex2Dlod(_Slide, float4(uv, 0, 4)).rgb;
                color = lerp(color, half3(.12, .08, .18), .22);
                return half4(lerp(color, half3(1, 1, 1), _Fade), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
