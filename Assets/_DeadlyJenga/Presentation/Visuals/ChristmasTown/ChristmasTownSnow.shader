Shader "DeadlyJenga/ChristmasTown/Outdoor Snow"
{
    Properties { _RoofMap("Shelter height",2D)="black" {} _Color("Snow",Color)=(.86,.93,1,.7) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_RoofMap);SAMPLER(sampler_RoofMap);
            float4x4 _TownWorldToLocal;float4 _Color;float4 _TownBounds;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 town:TEXCOORD1;float fog:TEXCOORD2;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(world);
                o.town=mul(_TownWorldToLocal,float4(world,1)).xyz;o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.town.xz/_TownBounds.xy+.5;
                clip(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                float roof=SAMPLE_TEXTURE2D(_RoofMap,sampler_RoofMap,uv).r;
                clip(i.town.y-roof-.14);
                float2 p=i.uv-.5;float alpha=smoothstep(.25,.025,dot(p,p));
                alpha*=i.color.a*_Color.a;
                return half4(MixFog(_Color.rgb*i.color.rgb,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
