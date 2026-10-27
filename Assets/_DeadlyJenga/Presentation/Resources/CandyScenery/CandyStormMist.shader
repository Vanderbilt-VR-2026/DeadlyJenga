Shader "Candy Tropical/Storm Mist"
{
    Properties { _BaseColor("Condensation",Color)=(.64,.72,.78,.48) _CloudTexture("Cloud detail",2D)="white"{} _UseCloudTexture("Textured cloud",Float)=0 _Streamed("Streaming ribbon",Float)=0 _Lightning("Lightning filament",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _BaseColor; float _UseCloudTexture,_Streamed,_Lightning;
            TEXTURE2D(_CloudTexture); SAMPLER(sampler_CloudTexture);
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);}
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(_Lightning>.5)
                {
                    float x=abs(i.uv.x-.5)*2,core=exp(-x*x*70),halo=exp(-x*x*8);
                    float edge=smoothstep(0,.06,i.uv.y)*(1-smoothstep(.94,1,i.uv.y));
                    return half4(lerp(_BaseColor.rgb*.45,float3(3,3,3),core),(core+halo*.32)*edge*_BaseColor.a);
                }
                if(_UseCloudTexture>.5)
                {
                    float2 uv=i.uv;
                    if(_Streamed>.5)uv=float2(.18+uv.x*.64+sin(uv.y*23+_Time.y*2)*.035,frac(uv.y*5-_Time.y*1.3));
                    half4 cloud=SAMPLE_TEXTURE2D(_CloudTexture,sampler_CloudTexture,uv);
                    if(_Streamed>.5)cloud.a*=smoothstep(0,.2,i.uv.x)*(1-smoothstep(.8,1,i.uv.x))*smoothstep(0,.04,i.uv.y)*(1-smoothstep(.92,1,i.uv.y));
                    float nearFade=smoothstep(.25,1.5,distance(i.world,_WorldSpaceCameraPos));
                    return half4(MixFog(cloud.rgb*_BaseColor.rgb*1.5,i.fog),cloud.a*_BaseColor.a*nearFade);
                }
                float2 p=i.uv*2-1;
                float2 flow=i.uv*float2(2.3,2.3)+i.world.xz*.012+float2(_Time.y*.20,-_Time.y*.32);
                float n=noise(flow)*.55+noise(flow*2.1)*.30+noise(flow*4.1)*.15;
                float edge=1-smoothstep(.12,.97,length(p)+(.5-n)*.36);
                float density=(.28+.72*smoothstep(.24,.66,n))*edge;
                float3 color=_BaseColor.rgb*(.62+n*.62+i.uv.y*.08);
                float nearFade=smoothstep(.25,1.5,distance(i.world,_WorldSpaceCameraPos));
                return half4(MixFog(color,i.fog),density*_BaseColor.a*nearFade);
            }
            ENDHLSL
        }
    }
}
