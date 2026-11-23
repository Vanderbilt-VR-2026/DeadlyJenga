Shader "DeadlyJenga/Cloud Interior"
{
    Properties { _MistTint("Mist", Vector) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZTest Always ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _MistTint;
            CBUFFER_END
            struct A { float4 pos:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = float4(a.pos.xy, UNITY_NEAR_CLIP_VALUE, 1);
                o.uv = a.pos.xy;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Both eyes see the same distant wisps. Turning the head pans through them.
                float4 ray = mul(UNITY_MATRIX_I_P,float4(i.uv,UNITY_NEAR_CLIP_VALUE,1));
                float3 d = normalize(mul((float3x3)UNITY_MATRIX_I_V,ray.xyz));
                float t = _Time.y * .17;
                float w = .5 + .25*sin(dot(d,float3(4,6,3))+t+sin(d.z*5-t)) + .25*sin(d.x*8-d.y*4+t);
                half3 col = lerp(half3(.68,.76,.88),half3(.99,.98,.96),w)*_MistTint.rgb;
                return half4(col,_MistTint.a);
            }
            ENDHLSL
        }
    }
}
