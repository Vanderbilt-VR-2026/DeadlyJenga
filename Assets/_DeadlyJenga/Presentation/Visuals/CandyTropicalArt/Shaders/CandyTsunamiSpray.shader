Shader "Candy Tropical/Tsunami Spray"
{
    Properties { _CloudTexture("Aerated spray",2D)="white"{} _UseCloudTexture("Textured spray",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_CloudTexture); SAMPLER(sampler_CloudTexture); float _UseCloudTexture;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float eye:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(world);o.eye=-TransformWorldToView(world).z;o.uv=i.uv;o.color=i.color;return o;}
            // Soft edge where a sheet meets blocks or ground (no camera depth: depth reads as far, no fade).
            float Soft(V i){
            #if UNITY_REVERSED_Z
                float scene=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)),_ZBufferParams);return saturate((scene-i.eye)/2.5);
            #else
                return 1;   // an unbound depth texture would read as "near" here and hide all spray
            #endif
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(_UseCloudTexture>.5)
                {
                    half4 puff=SAMPLE_TEXTURE2D(_CloudTexture,sampler_CloudTexture,i.uv);
                    float shade=lerp(.72,1,saturate(dot(puff.rgb,float3(.3,.59,.11))*1.8));
                    return half4(i.color.rgb*shade,i.color.a*puff.a*Soft(i));
                }
                float n=noise(i.uv*6+_Time.y*.4)*.7+noise(i.uv*13)*.3;
                float d=length(i.uv-.5)*2+(.5-n)*.35;
                float alpha=(1-smoothstep(.12,1,d))*smoothstep(.18,.6,n);
                return half4(i.color.rgb,i.color.a*alpha*.8*Soft(i));
            }
            ENDHLSL
        }
    }
}
