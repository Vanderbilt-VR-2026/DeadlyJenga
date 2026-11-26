// Meteor impact blast: an upright billboard that plays the Blender explosion flipbook (4x4) ONCE.
// The impact's age (0..1) is carried in the length of the object's Z axis, so many impacts stay instanced.
Shader "Candy Tropical/Impact Billboard"
{
    Properties { _BlastAtlas("Explosion flipbook (4x4)",2D)="black"{} _Glow("Fire brightness",Float)=1.4 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BlastAtlas); SAMPLER(sampler_BlastAtlas);
            float _Glow;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float age:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;
                o.age=saturate(length(float3(UNITY_MATRIX_M._m02,UNITY_MATRIX_M._m12,UNITY_MATRIX_M._m22)));
                return o;
            }
            half4 Cell(float2 uv,float k){k=clamp(k,0,15);float2 cell=float2(fmod(k,4),3-floor(k/4));return SAMPLE_TEXTURE2D(_BlastAtlas,sampler_BlastAtlas,(cell+saturate(uv)*.996+.002)/4);}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float frame=i.age*15.0,blend=frac(frame);
                half4 c=lerp(Cell(i.uv,floor(frame)),Cell(i.uv,floor(frame)+1),blend);
                float hot=saturate(max(c.r,c.g)*1.2-c.b);
                float fade=1-smoothstep(.8,1,i.age);
                return half4(c.rgb*lerp(1,_Glow,hot),c.a*fade);
            }
            ENDHLSL
        }
    }
}
