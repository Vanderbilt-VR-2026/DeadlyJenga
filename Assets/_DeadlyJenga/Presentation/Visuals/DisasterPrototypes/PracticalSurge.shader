Shader "Deadly Jenga/Prototypes/Practical Surge"
{
    Properties
    {
        _Detail("Packed 2D foam",2D)="gray"{}
        _Deep("Deep water",Color)=(.014,.16,.36,1)
        _Shallow("Crest water",Color)=(.04,.72,.69,1)
        _Foam("Foam",Color)=(.88,.99,1,1)
        _Whitewater("Whitewater",Range(0,1))=0
        _Phase("Preview time",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Detail);SAMPLER(sampler_Detail);
            CBUFFER_START(UnityPerMaterial)
            float4 _Deep,_Shallow,_Foam;
            float _Whitewater,_Phase;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a)
            {
                V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t=_Time.y+_Phase;
                float2 flow=float2(i.uv.x*8,i.uv.y*4-t*.12);
                float4 d=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,flow);
                float f=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,float2(i.uv.x*25,i.uv.y*1.7-t*.18)).b;
                float nearCrest=1-smoothstep(.015,lerp(.10,.21,_Whitewater),abs(i.uv.y-.72));
                float streak=pow(saturate(f*1.35-.30),3)*smoothstep(.27,.64,i.uv.y)*(1-smoothstep(.74,.98,i.uv.y));
                float foam=saturate(nearCrest*smoothstep(.23,.65,d.r)+streak*lerp(.65,1.5,_Whitewater));
                float3 n=normalize(i.normal+float3((d.g-.5)*.15,0,(d.b-.5)*.15));
                float fresnel=pow(1-saturate(abs(dot(n,normalize(GetWorldSpaceViewDir(i.world))))),3);
                Light light=GetMainLight();
                float spec=pow(saturate(abs(dot(n,normalize(light.direction+normalize(GetWorldSpaceViewDir(i.world)))))),75);
                float heightTint=smoothstep(0,.72,i.uv.y)*(1-smoothstep(.75,1,i.uv.y));
                float3 color=lerp(_Deep.rgb,_Shallow.rgb,heightTint*.85+d.g*.1);
                color=lerp(color,float3(.38,.72,.89),fresnel*.3)+spec*.6*light.color;
                color=lerp(color,_Foam.rgb,foam*.95);return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
