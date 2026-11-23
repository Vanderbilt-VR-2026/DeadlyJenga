Shader "DeadlyJenga/Cloud Resident"
{
    Properties { _Tint("Cloud tint", Vector) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 position:POSITION; half3 normal:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 position:SV_POSITION; half3 normal:TEXCOORD0; half4 color:COLOR; float3 world:TEXCOORD1; half fog:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = TransformObjectToWorld(input.position.xyz);
                o.position = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normal);
                o.color = input.color;
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normal);
                Light light = GetMainLight();
                half body = smoothstep(.35,.87,min(i.color.r,min(i.color.g,i.color.b)));
                half3 pigment = i.color.rgb * lerp(half3(1,1,1),_Tint.rgb,body);
                half wrap = saturate((dot(n,light.direction)+.38)/1.38);
                half3 shade = pigment * (.28 + .23 * max(0,SampleSH(n)) + .60 * light.color * wrap);
                half3 h = SafeNormalize(light.direction + GetWorldSpaceNormalizeViewDir(i.world));
                half eye = 1-smoothstep(.035,.13,max(i.color.r,max(i.color.g,i.color.b)));
                shade += eye * .70 * pow(saturate(dot(n,h)),36);
                return half4(MixFog(shade,i.fog),1);
            }
            ENDHLSL
        }
    }
}
