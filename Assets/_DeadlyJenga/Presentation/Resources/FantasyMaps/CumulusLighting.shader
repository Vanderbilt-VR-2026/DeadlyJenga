Shader "DeadlyJenga/Island Cumulus"
{
    Properties { _Billows ("Billow relief", 2D) = "gray" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Billows);
            SAMPLER(sampler_Billows);
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half fog:TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 p=i.positionWS*.015;
                half3 n=normalize(i.normalWS);
                half3 w=abs(n)*abs(n);
                half3 x=SAMPLE_TEXTURE2D(_Billows,sampler_Billows,p.zy).rgb;
                half3 y=SAMPLE_TEXTURE2D(_Billows,sampler_Billows,p.xz+.37).rgb;
                half3 z=SAMPLE_TEXTURE2D(_Billows,sampler_Billows,p.xy+.71).rgb;
                half3 slope=float3(y.r*w.y+z.r*w.z,x.g*w.x+z.g*w.z,x.r*w.x+y.g*w.y)-(1-w)*.5;
                half3 normal=normalize(n-.66*(slope-n*dot(n,slope)));
                half density=dot(half3(x.b,y.b,z.b),w);
                half3 sun=GetMainLight().direction;
                half wrap=smoothstep(-.74,.89,dot(normal,sun)+.09*(density-.5));
                half up=smoothstep(-.75,.85,n.y);
                half3 shade=lerp(half3(.24,.28,.49),half3(.50,.58,.76),up);
                half3 lit=lerp(half3(.78,.72,.81),half3(1.0,.94,.87),up);
                half3 color=lerp(shade,lit,wrap)*(.93+.12*density);
                half rim=1-saturate(dot(normal,GetWorldSpaceNormalizeViewDir(i.positionWS)));
                color+=half3(.085,.075,.079)*rim*rim*(.28+.72*wrap);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
