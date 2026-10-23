Shader "DeadlyJenga/ChristmasTown/Winter Sky"
{
    Properties
    {
        _Zenith("Night blue",Color)=(.018,.035,.12,1)
        _Horizon("Winter haze",Color)=(.12,.22,.36,1)
        _Aurora("Aurora",Color)=(.1,.65,.46,1)
        _AuroraStrength("Aurora strength",Range(0,1))=.18
        _MoonDirection("Moon direction",Vector)=(-.6,.35,-.35,0)
        _Stars("Stars",Range(0,1))=.65
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Zenith,_Horizon,_Aurora,_MoonDirection;float _Stars,_AuroraStrength;
            struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.direction=i.positionOS.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction);float elevation=saturate(d.y);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(elevation,.45));
                float2 p=d.xz/max(.2,d.y+.2)*190;float2 cell=floor(p),spot=frac(p)-.5;
                float star=smoothstep(.065,0,length(spot))*step(.965,hash(cell))*smoothstep(.05,.3,d.y);
                color+=star*_Stars*lerp(.5,1.25,hash(cell+19));
                float moon=dot(d,normalize(_MoonDirection.xyz));
                color+=float3(.67,.8,1)*(smoothstep(.9993,.99965,moon)*1.15+pow(saturate(moon),100)*.065);
                float ribbon=d.y-(.31+.07*sin(d.x*5+d.z*3)+.025*sin(d.z*17));
                float aurora=exp(-ribbon*ribbon*430)*(.5+.5*sin(d.x*33+d.z*23));
                color+=_Aurora.rgb*aurora*_AuroraStrength*smoothstep(.1,.4,d.y);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
