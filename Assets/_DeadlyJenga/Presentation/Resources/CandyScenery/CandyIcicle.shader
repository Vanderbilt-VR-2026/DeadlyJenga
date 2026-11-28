// Icey disaster: instanced icicles (Blender icicle kit). Clear pale ice: cool tint, bright rim, sharp highlight.
Shader "Candy Tropical/Icicle"
{
    Properties { _BaseColor("Ice",Color)=(.78,.92,1,.72) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-40" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float4 _BaseColor;
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float tip:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.world);o.tip=saturate(-i.positionOS.y);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),view=normalize(_WorldSpaceCameraPos-i.world);Light light=GetMainLight();
                float fresnel=pow(1-saturate(dot(n,view)),2.5);
                float spec=pow(saturate(dot(n,normalize(view+light.direction))),60);
                float3 color=_BaseColor.rgb*(.6+.4*saturate(dot(n,light.direction)))+fresnel*float3(.5,.7,.9)+spec*1.5;
                // Root is frostier and whiter, the tip clearer.
                color=lerp(float3(.92,.97,1),color,smoothstep(0,.35,i.tip));
                return half4(color*light.color,saturate(_BaseColor.a*(.55+fresnel*.6)+spec+(1-smoothstep(0,.3,i.tip))*.3));
            }
            ENDHLSL
        }
    }
}
