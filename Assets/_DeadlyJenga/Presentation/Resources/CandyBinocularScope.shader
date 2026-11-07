Shader "DeadlyJenga/BinocularScope"
{
    Properties { _BaseMap ("Scope view", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            ZWrite On ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _OpticRight, _OpticUp, _OpticForward, _ContentTanHalfFov;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 ray=input.positionWS-GetCameraPositionWS();
                float depth=dot(ray,_OpticForward.xyz);
                float2 p=float2(dot(ray,_OpticRight.xyz),dot(ray,_OpticUp.xyz))/max(.00001,depth)/max(_ContentTanHalfFov.xy,.00001);
                if(depth<=0||any(abs(p)>1))return half4(.01,.015,.02,1);
                half4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p*.5+.5);
                float reticle=step(abs(p.x),.003)+step(abs(p.y),.003);
                color.rgb=lerp(color.rgb,half3(.08,.8,.5),saturate(reticle)*.8);
                color.a=1;
                return color;
            }
            ENDHLSL
        }
    }
}
