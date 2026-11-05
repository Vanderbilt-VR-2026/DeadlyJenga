Shader "Candy Tropical/Wet Splash Coating"
{
    Properties
    {
        _BaseColor("Clear water absorption", Color)=(.025,.075,.078,.045)
        _Wetness("Wetness", Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float4 _BaseColor;
            UNITY_INSTANCING_BUFFER_START(Water)
                UNITY_DEFINE_INSTANCED_PROP(float,_Wetness)
            UNITY_INSTANCING_BUFFER_END(Water)
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 world:TEXCOORD0;
                float3 normal:TEXCOORD1;
                float3 local:TEXCOORD2;
                half fog:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.local=input.positionOS.xyz;
                output.normal=TransformObjectToWorldNormal(input.normalOS);
                output.positionCS=TransformWorldToHClip(output.world);
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float wet=UNITY_ACCESS_INSTANCED_PROP(Water,_Wetness);
                float3 p=input.local*13;
                float film=.78+.22*sin(p.x*1.7+sin(p.z*1.1))*sin(p.y*1.3+p.z);
                float beads=pow(saturate(sin(p.x*2.9+p.z)*sin(p.y*3.1-p.z)),18);
                beads*=1-smoothstep(.15,.75,length(fwidth(p)));
                float2 flow=float2(cos(p.z+_Time.y*.65),sin(p.x-_Time.y*.55));
                half3 normal=normalize(input.normal+float3(flow.x,.18*beads,flow.y)*(.009*wet));
                half3 view=GetWorldSpaceNormalizeViewDir(input.world);
                Light light=GetMainLight(TransformWorldToShadowCoord(input.world));
                float grazing=pow(1-saturate(dot(normal,view)),5);
                float fresnel=.02+.48*grazing;
                half3 halfVector=SafeNormalize(view+light.direction);
                float alignment=saturate(dot(normal,halfVector));
                float power=160/(1+max(.001,fwidth(alignment))*220);
                float specular=pow(alignment,power)*(power/160);
                half3 reflection=GlossyEnvironmentReflection(reflect(-view,normal),input.world,.075h,1.0h);
                float coverage=wet*film;
                float reflectionWeight=fresnel*.60;
                float absorption=_BaseColor.a*coverage;
                float opacity=saturate(absorption+reflectionWeight*coverage);
                half3 direct=light.color*light.shadowAttenuation*light.distanceAttenuation*saturate(dot(normal,light.direction));
                half3 sheen=reflection*reflectionWeight+direct*specular*1.6+
                    direct*beads*pow(alignment,65)*.20;
                // Premultiplied clear film keeps the candy color beneath it.
                half3 color=_BaseColor.rgb*absorption+sheen*coverage;
                color=MixFogColor(color,unity_FogColor.rgb*opacity,input.fog);
                return half4(color,opacity);
            }
            ENDHLSL
        }
    }
}
