Shader "DeadlyJenga/Rainbow Current"
{
    Properties { _FlowTime ("Current time", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
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
            struct Output { float4 pos:SV_POSITION; float4 color:COLOR; float4 flow:TEXCOORD0; float fog:TEXCOORD1; float3 normalWS:TEXCOORD2; float3 worldPos:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pos=v.pos.xyz;
                float4 f=v.flow;
                float3 n=v.normal;
                if(f.z>2.5)
                {
                    float pulse=sin(f.y*5-_FlowTime*4.1)+.48*sin(f.y*9+_FlowTime*3);
                    float lip=f.x*f.x;
                    pos.y+=f.w*.05*pulse*lip;
                    pos.xz*=1+.095*pulse*lip;
                }
                else if(f.z>.5)
                {
                    float t=frac(_FlowTime*(.9+.27*f.x)+f.x);
                    float2 dir=float2(cos(f.y),sin(f.y));
                    float3 velocity=normalize(float3(dir.x,2.9*(1-2*t),dir.y));
                    float3 side=float3(-dir.y,0,dir.x);
                    float3 forward=cross(side,velocity);
                    float birth=smoothstep(0,.12,t)*(1-smoothstep(.78,1,t));
                    pos=(side*pos.x+velocity*pos.y+forward*pos.z)*birth;
                    n=side*n.x+velocity*n.y+forward*n.z;
                    pos.xz+=dir*f.w*(.48+t);
                    pos.y+=f.w*(.06+2.9*t*(1-t));
                }
                else
                {
                    pos+=n*.24*sin(f.x*6.28-_FlowTime*3.4+f.y*6);
                }
                o.worldPos=TransformObjectToWorld(pos);
                o.normalWS=TransformObjectToWorldNormal(n);
                o.pos=TransformObjectToHClip(pos); o.flow=f; o.color=v.color;
                o.fog=ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normalWS);
                float3 view=GetWorldSpaceNormalizeViewDir(i.worldPos);
                n=faceforward(n,-view,n);
                float3 dye=i.color.rgb;
                float lip=0;
                if(i.flow.z<.5)
                {
                    // Fluted sugar ribbons have long, interrupted reflections.
                    float down=i.flow.x*3.6-_FlowTime*2.15;
                    float across=i.flow.y*37+.55*sin(down+i.flow.y*11);
                    float3 widthAxis=normalize(cross(n,abs(n.y)<.9 ? float3(0,1,0) : float3(1,0,0)));
                    n=normalize(n+widthAxis*(.32*sin(across)+.12*sin(2.7*across-down))+
                        cross(n,widthAxis)*.13*cos(down+.8*sin(across)));
                }
                else if(i.flow.z>2.5)
                {
                    lip=smoothstep(.76,1,i.flow.x);
                    float flute=.09*sin(i.flow.y*22+i.flow.x*4-_FlowTime*3)*i.flow.x;
                    n=normalize(n+float3(flute,0,-flute));
                }
                float3 reflection=reflect(-view,n);
                float facing=saturate(dot(n,view));
                float fresnel=pow(1-facing,3);
                float3 key=normalize(float3(-.52,.78,.34));
                float diffuse=.40+.52*saturate(dot(n,key));
                float transmission=.17*pow(saturate(dot(-n,key)),2);
                float3 body=pow(max(dye,.005),1.7)*(diffuse+transmission);
                // The broad sky reflection and two small highlights describe curvature
                // even on a single bead against the bright cloud bank.
                float sky=smoothstep(.18,.86,reflection.y);
                float glint=pow(saturate(dot(reflection,key)),55);
                float softbox=pow(saturate(dot(reflection,normalize(float3(.73,.32,-.55)))),18);
                float3 c=body+sky*(.036+.17*fresnel)*float3(.69,.86,1)
                    +glint*.85+softbox*.25+lip*.11+fresnel*.10;
                float opacity=i.flow.z<.5 ? .96 : .80+.16*fresnel;
                return half4(MixFog(c,i.fog),opacity);
            }
            ENDHLSL
        }
    }
}
