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
                if(state.y<-.5)
                {
                    float slanted=p.x+p.y*.33;
                    float slot=abs(slanted-clamp(round(slanted/.32),-1,1)*.32);
                    float edge=1-smoothstep(.024,.062,slot);
                    float tips=1-smoothstep(.24,.81,abs(p.y));
                    float heat=exp(-state.z*5);
                    half3 flash=lerp(tint,half3(1,1,1),.55*heat);
                    return half4(MixFog(flash,input.fog),edge*tips*state.x*.8);
                }
                float angle=atan2(p.y,p.x);
                if(state.y>2.5)
                {
                    float outer=.47+.15*sin(angle*5)+.10*sin(angle*11)+.06*cos(angle*19);
                    float splash=1-smoothstep(outer-.02,outer+.02,radius);
                    float2 cell=floor(p*11);
                    float seed=frac(sin(dot(cell,float2(31.282,57.79)))*43758.5453);
                    float drops=(1-smoothstep(.13,.31,length(frac(p*11)-.5)))
                        *step(.75,seed)*(1-smoothstep(.75,.98,radius))*.83;
                    half3 syrup=lerp(tint*.67,saturate(tint*1.07+.10),saturate(.66+p.y*.79));
                    return half4(MixFog(syrup,input.fog),max(splash,drops)*state.x);
                }
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
