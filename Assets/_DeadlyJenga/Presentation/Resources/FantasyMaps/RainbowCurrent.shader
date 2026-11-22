Shader "DeadlyJenga/Rainbow Current"
{
    Properties { _FlowTime ("Current time", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _FlowTime;
            CBUFFER_END
            struct Input { float4 pos:POSITION; float3 normal:NORMAL; float4 color:COLOR; float4 flow:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 pos:SV_POSITION; float4 color:COLOR; float4 flow:TEXCOORD0; float fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pos=v.pos.xyz;
                float4 f=v.flow;
                if(f.z>2.5)
                {
                    pos.y+=f.w*.044*sin(_FlowTime*4.5+f.y);
                    pos.z+=f.w*.035*cos(_FlowTime*3.1+f.y);
                }
                else if(f.z>1.5) // Foam ring vertices are authored around the impact origin.
                {
                    float t=frac(_FlowTime*.74+f.x);
                    pos.xz*=.22+1.4*t;
                    pos.y+=.46*sin(3.14159*t);
                    v.color.rgb*=1-.34*t;
                }
                else if(f.z>.5) // Ballistic spray, staggered across the impact width.
                {
                    float t=frac(_FlowTime*(.79+.19*f.x)+f.x);
                    float2 dir=float2(cos(f.y),sin(f.y));
                    pos.xz+=dir*f.w*t;
                    pos.y+=4*t*(1-t)*f.w*.72;
                }
                else
                {
                    float wave=sin(f.x*6.283185-_FlowTime*3.4+f.y*6);
                    pos+=v.normal*wave*.26;
                }
                o.pos=TransformObjectToHClip(pos); o.flow=f; o.color=v.color;
                o.fog=ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 c=i.color.rgb;
                if(i.flow.z<.5)
                {
                    float along=i.flow.x, across=i.flow.y;
                    float phase=along*6.283185-_FlowTime*3.4;
                    float curl=sin(across*17+along*1.3)*.93+sin(across*39-along*2.1)*.31;
                    float crest=.5+.5*cos(phase+curl);
                    float streak=.5+.5*sin(across*133+sin(phase*.5+across*15)*2);
                    float foam=smoothstep(.78,.99,crest)*(.30+.64*streak);
                    c=lerp(c*(.68+.32*crest),float3(.94,.98,1),foam*.74);
                }
                return half4(MixFog(c,i.fog),1);
            }
            ENDHLSL
        }
    }
}
