Shader "DeadlyJenga/Ninja/Unlit"
{
    Properties
    {
        _BaseColor("Base", Color) = (1,1,1,1)
        _UseFog("Use fog", Range(0,1)) = 1
        _Flicker("Flicker", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NinjaCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; half _UseFog, _Flicker;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 color : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
                float phase = o.positionWS.x * 1.7 + o.positionWS.z * 2.3;
                o.color = i.color * _BaseColor * (1.0 + _Flicker * (0.12 * sin(_Time.y * 8.0 + phase) + 0.07 * sin(_Time.y * 21.0 + phase * 2.0)));
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 c = i.color.rgb;
                return half4(lerp(c, NinjaFog(c, i.positionWS), _UseFog), 1);
            }
            ENDHLSL
        }
    }
}
