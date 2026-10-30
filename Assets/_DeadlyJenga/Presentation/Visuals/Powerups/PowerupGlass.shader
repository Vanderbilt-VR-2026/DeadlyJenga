Shader "DeadlyJenga/Powerups/Glass"
{
    Properties { _Tint("Tint",Color)=(.15,.85,1,1) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Tint;
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),v=GetWorldSpaceNormalizeViewDir(i.world);
                float rim=pow(1-saturate(dot(n,v)),3);
                float glint=pow(saturate(dot(n,normalize(float3(-.4,.75,-.5)))),55);
                float band=pow(saturate(1-abs(dot(n,normalize(float3(.7,.35,.5))))),65)*.17;
                float3 color=lerp(_Tint.rgb*.35,float3(.85,.95,1),rim*.85)+glint*2+band;
                return half4(MixFog(color,i.fog),saturate(.1+rim*.65+glint*.75+band));
            }
            ENDHLSL
        }
    }
}
