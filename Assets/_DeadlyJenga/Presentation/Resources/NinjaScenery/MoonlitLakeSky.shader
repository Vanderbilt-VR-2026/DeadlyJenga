Shader "DeadlyJenga/Ninja/Moonlit Lake Sky"
{
    Properties
    {
        _Panorama("Painted mountain shoreline",2D)="black"{}
        _Lake("Center XZ, shore radius, waterline",Vector)=(0,0,185,.08)
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MoonlitLake.hlsl"
            TEXTURE2D(_Panorama); SAMPLER(sampler_Panorama);
            CBUFFER_START(UnityPerMaterial)
            float4 _Lake;
            CBUFFER_END
            struct Input { float4 pos:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 pos:SV_POSITION; float3 ray:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input i)
            {
                Output o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=TransformObjectToHClip(i.pos.xyz); o.ray=i.pos.xyz; return o;
            }
            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(LakeSky(GetCameraPositionWS(),normalize(i.ray),_Lake,TEXTURE2D_ARGS(_Panorama,sampler_Panorama)),1);
            }
            ENDHLSL
        }
    }
}
