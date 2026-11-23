Shader "DeadlyJenga/Kingdom Sugar Glass"
{
    Properties { _Tint ("Amber glass", Color) = (1,.46,.09,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 positionCS:SV_POSITION; float3 local:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            CBUFFER_END
            Output Vert(Input i)
            {
                Output o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.local=i.positionOS.xyz; return o;
            }
            half4 Frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float ripple=.5+.5*sin(11*i.local.x+3*sin(7*i.local.y)+5*i.local.z);
                return half4(lerp(_Tint.rgb,float3(1,.78,.27),.25+.58*ripple)*2.1,1);
            }
            ENDHLSL
        }
    }
}
