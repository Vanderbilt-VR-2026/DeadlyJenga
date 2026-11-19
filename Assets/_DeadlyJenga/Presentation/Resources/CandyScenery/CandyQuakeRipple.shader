Shader "Candy Tropical/Earthquake Ripple"
{
    Properties
    {
        _BaseColor("Shore foam", Color)=(.69,.93,.87,.62)
        _RippleOpacity("Ripple opacity", Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+110" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Quake)
                UNITY_DEFINE_INSTANCED_PROP(float, _RippleOpacity)
            UNITY_INSTANCING_BUFFER_END(Quake)

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
                float fog : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.uv*2-1;
                float radius=length(p);
                float shoreSide=smoothstep(.02,.32,p.y);
                float breakUp=.74+.26*sin(p.x*29+p.y*17);
                float wave=1-smoothstep(.022,.055,abs(radius-.84));
                float trailing=1-smoothstep(.015,.046,abs(radius-.61));
                float alpha=(wave+trailing*.48)*shoreSide*breakUp;
                alpha*=UNITY_ACCESS_INSTANCED_PROP(Quake,_RippleOpacity)*_BaseColor.a;
                return half4(MixFog(_BaseColor.rgb,input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
