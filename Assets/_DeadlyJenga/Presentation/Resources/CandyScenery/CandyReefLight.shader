Shader "Candy Tropical/Reef Light"
{
    Properties
    {
        [NoScaleOffset] _CoastCaustics("Refracted light",2D)="black"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Reef underwater light"
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_CoastCaustics); SAMPLER(sampler_CoastCaustics);
            float3 _ReefSun;
            float _ReefWater;

            struct Attributes { uint vertexID : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            float LightColumn(float3 position, float3 direction)
            {
                float depth=max(0,_ReefWater-position.y);
                float2 surface=position.xz+direction.xz*depth;
                float2 uv=float2(dot(surface,float2(.83,.55)),dot(surface,float2(-.55,.83)));
                float2 flow=uv*float2(.064,.142)+float2(.011,-.008)*_Time.y;
                half4 intensity=SAMPLE_TEXTURE2D_LOD(_CoastCaustics,sampler_CoastCaustics,flow,.9);
                float packet=sin(uv.x*.36+sin(uv.y*.18-_Time.y*.23)*1.05);
                float ribbon=smoothstep(.38,.88,packet);
                float broken=smoothstep(.23,.72,max(intensity.r,intensity.g*.72));
                return ribbon*(.27+.73*broken)*exp2(-depth*.60);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.positionCS.xy/_ScaledScreenParams.xy;
                float sceneDepth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    sceneDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,sceneDepth);
                #endif
                // A fixed clip depth gives a stable ray even for a sky pixel.
                float3 origin=GetCameraPositionWS();
                float3 ahead=ComputeWorldSpacePosition(uv,.5,UNITY_MATRIX_I_VP);
                float3 ray=normalize(ahead-origin);
                float3 hit=ComputeWorldSpacePosition(uv,clamp(sceneDepth,.00002,.99998),UNITY_MATRIX_I_VP);
                float distanceToHit=max(0,dot(hit-origin,ray));
                float eyeDepth=max(0,_ReefWater-origin.y);
                float exitDistance=ray.y>.003?eyeDepth/ray.y:48;
                float lengthInWater=min(33,min(distanceToHit,exitDistance));
                float3 light=normalize(float3(_ReefSun.x*.58,1,_ReefSun.z*.58));
                float beam=0;
                UNITY_UNROLL for(int index=0;index<4;index++)
                {
                    float distance=(index+.5)*.25*lengthInWater;
                    beam+=LightColumn(origin+ray*distance,light)*smoothstep(1.4,6,distance);
                }
                float facing=pow(saturate(dot(ray,light)),4);
                beam*=min(lengthInWater*.044,.50)*(.14+facing*.82);
                beam*=1-smoothstep(.10,.43,-ray.y);
                float haze=(1-exp2(-lengthInWater*.039))*.18;
                float fade=smoothstep(.12,.33,eyeDepth);
                float3 color=float3(.015,.22,.31)*haze+float3(.15,.68,.72)*beam;
                return half4(color*fade,haze*fade);
            }
            ENDHLSL
        }
    }
}
