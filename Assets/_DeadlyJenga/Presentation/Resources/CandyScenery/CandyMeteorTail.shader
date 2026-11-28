// Meteor v2 entry plume: a flight-aligned billboard playing the Blender plume flipbook (2 x 4 frames, head at u = 1).
// _TailTint lets skins recolour it (icy meteors: white-blue vapour).
Shader "Candy Tropical/Meteor Tail"
{
    Properties { _TailAtlas("Plume flipbook (2x4)",2D)="black"{} _TailTint("Tint",Color)=(1,1,1,1) _Fps("Frames per second",Float)=14 _Glow("Brightness",Float)=1.5 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+7" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One, Zero One   // additive-leaning: the plume glows over the sky without corrupting dest alpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_TailAtlas); SAMPLER(sampler_TailAtlas);
            CBUFFER_START(UnityPerMaterial)
            float4 _TailTint; float _Fps,_Glow;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float seed:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;
                float3 origin=TransformObjectToWorld(float3(0,0,0));o.seed=frac(dot(origin,float3(.131,.271,.193)));
                return o;
            }
            half4 Cell(float2 uv,uint k){k&=7u;float2 cell=float2(k&1u,3u-(k>>1u));return SAMPLE_TEXTURE2D(_TailAtlas,sampler_TailAtlas,(cell+saturate(uv)*.996+.002)/float2(2,4));}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float phase=frac((_Time.y*_Fps)/8.0+i.seed)*8.0;
                uint k0=min((uint)floor(phase),7u),k1=(k0+1u)&7u;
                half4 c=lerp(Cell(i.uv,k0),Cell(i.uv,k1),frac(phase));
                float3 rgb=c.rgb*_TailTint.rgb*_Glow;
                return half4(rgb,c.a*_TailTint.a);
            }
            ENDHLSL
        }
    }
}
