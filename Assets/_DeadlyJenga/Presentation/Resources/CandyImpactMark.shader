Shader "Candy/Temporary Weapon Impact"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off Offset -1,-1
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(Marks)
                UNITY_DEFINE_INSTANCED_PROP(float4,_MarkTint)
                UNITY_DEFINE_INSTANCED_PROP(float4,_MarkState)
            UNITY_INSTANCING_BUFFER_END(Marks)
            struct Attributes
            {
                float4 positionOS:POSITION;float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;output.fog=ComputeFogFactor(output.positionCS.z);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 state=UNITY_ACCESS_INSTANCED_PROP(Marks,_MarkState);
                half3 tint=UNITY_ACCESS_INSTANCED_PROP(Marks,_MarkTint).rgb;
                float2 p=(input.uv-.5)*2;float radius=length(p);
                float angle=atan2(p.y,p.x);
                float jagged=radius*(1+.12*sin(angle*13)+.045*sin(angle*27));
                float rim=exp(-abs(jagged-.48)*22);
                float center=1-smoothstep(.05,.68,jagged);
                float electric=state.y>1.5?pow(saturate(.5+.5*cos(angle*7+radius*13)),10):0;
                float heat=exp(-state.z*4);
                half3 color=lerp(half3(.055,.042,.055),tint,(rim*.42+electric*.5+heat*.42));
                float alpha=saturate(center*.65+rim*.65+electric*.26)*(1-smoothstep(.72,1,radius))*state.x;
                color=MixFog(color,input.fog);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
