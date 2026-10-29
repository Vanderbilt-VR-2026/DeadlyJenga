Shader "Deadly Jenga/Prototypes/Practical Funnel"
{
    Properties
    {
        _Detail("Packed 2D detail",2D)="gray"{}
        _Dark("Shadow",Color)=(.10,.17,.23,1)
        _Light("Sunlit cloud",Color)=(.65,.76,.81,1)
        _Phase("Preview time",Float)=0
        _Ribbon("Ribbon variant",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Detail); SAMPLER(sampler_Detail);
            CBUFFER_START(UnityPerMaterial)
            float4 _Dark,_Light;
            float _Phase,_Ribbon;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t=_Time.y+_Phase;
                float2 spiral=float2(i.uv.x*3+i.uv.y*5-t*.21,i.uv.y*3-t*.055);
                float d=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,spiral).r;
                float fine=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,spiral*3.1).g;
                float band=sin((i.uv.x*3+i.uv.y*7-t*.32)*6.283)*.5+.5;
                float3 n=normalize(i.normal);Light light=GetMainLight();
                float diffuse=abs(dot(n,light.direction));
                float rim=pow(1-abs(dot(n,normalize(GetWorldSpaceViewDir(i.world)))),2);
                float shade=saturate(.14+diffuse*.37+d*.34+band*.18+fine*.09);
                float3 color=lerp(_Dark.rgb,_Light.rgb,shade)+rim*.09;
                color*=lerp(1,light.color,.3);return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
