Shader "DeadlyJenga/Ninja/Lacquer"
{
    Properties
    {
        _BaseColor("Base", Color) = (1,1,1,1)
        _Gloss("Gloss", Range(0,1)) = 0.5
        _Spec("Specular", Range(0,2)) = 0.6
        _Rim("Moon rim", Range(0,2)) = 0.6
        _Warm("Lantern bounce", Range(0,2)) = 1
        _Wind("Wind", Range(0,3)) = 0
        _AlphaEmission("Emission from vertex alpha", Range(0,4)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "NinjaCommon.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _Gloss, _Spec, _Rim, _Warm, _Wind, _AlphaEmission, _Cull;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        float3 Displaced(Attributes i)
        {
            float3 p = TransformObjectToWorld(i.positionOS.xyz);
            if (_Wind > 0) p += NinjaWind(p, i.color.a * i.color.a, _Wind);
            return p;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_instancing
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = Displaced(i);
                o.positionWS = p; o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS); o.color = i.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normalWS);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                half3 albedo = _BaseColor.rgb * i.color.rgb;
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(n, light.direction);
                half shadow = lerp(0.3h, 1.0h, light.shadowAttenuation);
                half3 direct = light.color * saturate((ndl + 0.3h) / 1.3h) * shadow;
                half3 hemi = lerp(_NinjaGroundAmb.rgb, _NinjaSkyAmb.rgb, n.y * 0.5h + 0.5h);
                half warm = exp(-max(0.0, i.positionWS.y - _NinjaFogParams.w) / max(0.5, _NinjaWarm.w)) * _Warm * (0.55h - n.y * 0.45h);
                half3 lit = albedo * (direct + hemi + _NinjaWarm.rgb * max(0.0h, warm));
                half3 h = normalize(light.direction + v);
                lit += light.color * pow(saturate(dot(n, h)), lerp(10.0h, 110.0h, _Gloss)) * _Spec * shadow * saturate(ndl * 4.0h);
                lit += light.color * pow(1.0h - saturate(dot(n, v)), 3.0h) * _Rim * 0.55h * saturate(ndl + 0.6h);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0; li < count; li++)
                {
                    Light a = GetAdditionalLight(li, i.positionWS);
                    half3 c = a.color * a.distanceAttenuation;
                    lit += albedo * c * saturate(dot(n, a.direction) * 0.75h + 0.25h);
                    lit += c * pow(saturate(dot(n, normalize(a.direction + v))), 48.0h) * _Spec * 0.6h;
                }
                #endif
                lit += i.color.rgb * _BaseColor.rgb * (1.0h - i.color.a) * _AlphaEmission;
                return half4(NinjaFog(lit, i.positionWS), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            float4 vert(Attributes i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = Displaced(i), n = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
                #else
                float3 l = _LightDirection;
                #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, l));
                #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
