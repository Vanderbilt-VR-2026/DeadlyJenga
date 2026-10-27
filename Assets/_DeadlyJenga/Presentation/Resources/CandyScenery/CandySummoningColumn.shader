Shader "Candy Tropical/Player Summoning Column"
{
    Properties
    {
        _FlowTime("Flow time",Float)=0
        _Progress("Departure progress",Float)=0
        _Strength("Strength",Float)=0
        _Lift("Player lift",Float)=0
        _Seed("Variation",Float)=0
        _ColumnHeight("Column height",Float)=150
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _FlowTime,_Progress,_Strength,_Lift,_Seed,_ColumnHeight;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 info:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 info:TEXCOORD1;float height:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=i.positionOS.xyz;
                if(i.info.x<.5)
                {
                    float twist=sin(p.y*.11-_FlowTime*1.5+i.info.y)*.045;
                    float s=sin(twist),c=cos(twist);p.xz=float2(p.x*c-p.z*s,p.x*s+p.z*c);
                }
                if(i.info.x>1.5)p.y+=_Lift;
                o.positionCS=TransformObjectToHClip(p);o.uv=i.uv;o.info=i.info;o.height=p.y;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float seed=i.info.y+_Seed,t=_FlowTime;
                float edge=pow(saturate(1-abs(i.uv.x*2-1)),1.7);
                float streak=.16+.84*smoothstep(.12,.65,.5+.5*sin(i.uv.y*1.8-t*16+seed*9));
                float alive=smoothstep(0,.1,_Progress);
                float front=lerp(_ColumnHeight+8,-8,smoothstep(0,.14,_Progress));
                float reach=smoothstep(front-6,front+2,i.height)*(1-smoothstep(_ColumnHeight-min(25,max(.35,_ColumnHeight*.17)),_ColumnHeight,i.height));
                float ring=i.info.x>.5?1:0;
                if(ring>.5){reach=smoothstep(.07,.19,_Progress);streak=.62+.38*sin(t*9+seed+i.uv.y*7);}
                float mixColor=smoothstep(.25,.75,.5+.5*sin(seed*3+i.uv.y*.14-t*.7));
                float3 color=lerp(float3(.17,1,.58),float3(.67,.26,1),mixColor);
                float core=pow(edge,5);color=lerp(color,float3(.96,1,1),core*.5);
                float alpha=edge*streak*reach*alive*_Strength*(ring>.5?.24:.38);
                return half4(color*1.8,alpha);
            }
            ENDHLSL
        }
    }
}
