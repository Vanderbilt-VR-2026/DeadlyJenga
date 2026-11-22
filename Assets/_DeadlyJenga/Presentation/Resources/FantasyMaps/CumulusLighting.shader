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
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half fog:TEXCOORD2;
                half skyAccess:TEXCOORD3;
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
                o.skyAccess=v.color.r;
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
                Light key=GetMainLight();
                half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half up=smoothstep(-.78,.84,n.y);
                half access=smoothstep(.54,1,i.skyAccess);
                half facing=dot(normal,key.direction);
                half wrap=smoothstep(-.63,.85,facing+.13*(density-.5));
                // Blue skylight fills upward-facing folds. The deep underbank picks up violet.
                half3 skyFill=lerp(half3(.18,.17,.32),half3(.52,.65,.86),up);
                skyFill*=lerp(.68,1.0,access);
                half3 cream=half3(1.02,.95,.85)*lerp(half3(1,1,1),key.color/max(.01,max(key.color.r,max(key.color.g,key.color.b))),.32);
                half3 color=lerp(skyFill,cream,wrap*lerp(.67,1.0,access));
                // Low-angle sunlight scatters peach through exposed crests.
                half grazing=1-saturate(dot(normal,view));
                half forward=saturate(dot(-view,key.direction)*.5+.5);
                half silver=grazing*grazing*access*smoothstep(-.35,.35,facing)*(.28+.72*forward*forward);
                color=color*(.945+.10*density)+half3(.28,.155,.09)*silver;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
