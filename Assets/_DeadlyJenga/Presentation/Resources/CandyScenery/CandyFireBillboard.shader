// Fire v2 engulfing fire mass: an upright billboard playing the Blender fire-simulation flipbook (flames + black
// smoke, 4x4 frames). Each billboard starts at its own frame (from its position) and frames crossfade.
Shader "Candy Tropical/Fire Billboard"
{
    Properties { _FireAtlas("Fire mass flipbook (4x4)",2D)="black"{} _Fps("Frames per second",Float)=12 _Glow("Flame brightness",Float)=1.35 _Fade("Fade",Range(0,1))=1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_FireAtlas); SAMPLER(sampler_FireAtlas);
            float _Fps,_Glow,_Fade;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float seed:TEXCOORD1;float eye:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world);o.uv=i.uv;o.eye=-TransformWorldToView(world).z;
                float3 origin=TransformObjectToWorld(float3(0,0,0));o.seed=frac(dot(origin.xz,float2(.173,.311)));
                return o;
            }
            half4 Cell(float2 uv,float frame){float k=fmod(frame,16);float2 cell=float2(fmod(k,4),3-floor(k/4));return SAMPLE_TEXTURE2D(_FireAtlas,sampler_FireAtlas,(cell+saturate(uv)*.996+.002)/4);}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float frame=_Time.y*_Fps+i.seed*16,blend=frac(frame);
                half4 fire=lerp(Cell(i.uv,floor(frame)),Cell(i.uv,floor(frame)+1),blend);
                // Soft edge where the fire mass meets blocks or ground (reversed-Z only; otherwise no fade).
                float soft=1;
                #if UNITY_REVERSED_Z
                soft=saturate((LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)),_ZBufferParams)-i.eye)/1.2);
                #endif
                float hot=saturate(max(fire.r,fire.g)*1.2-fire.b);   // bright flame vs dark smoke
                return half4(fire.rgb*lerp(1,_Glow,hot),fire.a*_Fade*soft);
            }
            ENDHLSL
        }
    }
}
