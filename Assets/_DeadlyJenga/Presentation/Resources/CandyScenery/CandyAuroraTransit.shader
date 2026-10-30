Shader "Candy Tropical/Aurora Transit"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        _Progress("Coverage",Float)=0
        _TravelTime("Unscaled flow time",Float)=0
        _Aspect("Aspect",Float)=1.6
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            float4 _Color;
            float _Progress,_TravelTime,_Aspect;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color*_Color;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float p=saturate(_Progress),t=_TravelTime;
                float2 uv=i.uv;
                // A continuous expanding white column, with upward-flowing aurora ribbons.
                float warp=sin(uv.y*5.5-t*1.3)*.026+sin(uv.y*12+t*.7)*.012;
                float x=(uv.x-.5)*max(1,_Aspect);
                float reach=lerp(-.12,max(1,_Aspect)*.64,p);
                float edge=abs(x+warp)-reach;
                float alpha=(1-smoothstep(-.07,.12,edge))*smoothstep(0,.09,p);
                float ribbonX=uv.x+warp;
                float green=pow(saturate(.5+.5*sin(ribbonX*91+sin(uv.y*6-t*.9)*1.3)),15);
                float violet=pow(saturate(.5+.5*sin(ribbonX*67+1.7+sin(uv.y*8-t*1.1))),19);
                float flow=.66+.34*sin(uv.y*17-t*5+ribbonX*4);
                float fringe=exp(-abs(edge)*8);
                float colorStrength=(.19+.37*fringe)*(1-smoothstep(.72,1,p)*.48);
                float3 color=float3(.99,1,1);
                color=lerp(color,float3(.24,1,.66),green*flow*colorStrength);
                color=lerp(color,float3(.69,.34,1),violet*(1.15-flow*.25)*colorStrength);
                color+=fringe*.08;
                float core=exp(-x*x*14)*smoothstep(.15,.85,p);
                color=lerp(color,float3(1,1,1),core*.65);
                return half4(saturate(color)*i.color.rgb,alpha*i.color.a*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a);
            }
            ENDHLSL
        }
    }
}
