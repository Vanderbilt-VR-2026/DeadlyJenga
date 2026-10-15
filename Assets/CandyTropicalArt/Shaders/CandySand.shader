Shader "Candy Tropical/Sand"
{
    Properties
    {
        _BaseColor("Warm molded sand",Color)=(.86,.67,.4,1)
        _WaterHeight("Water surface",Float)=.08
        _Grain("Sand grain",Range(0,1))=.55
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _WaterHeight,_Grain;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings vert(Attributes i){Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.world.xz;float distanceToEye=distance(i.world,_WorldSpaceCameraPos);
                float2 grainUV=p*75;
                float grainFade=(1-smoothstep(5,22,distanceToEye))*(1-smoothstep(.2,.9,max(length(ddx(grainUV)),length(ddy(grainUV)))));
                float grain=noise(grainUV),grit=noise(p*23);
                float micro=grainFade*_Grain;
                float3 n=normalize(i.normal+float3((noise(grainUV+float2(.3,0))-grain)*micro*.16,0,(noise(grainUV+float2(0,.3))-grain)*micro*.16));
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float3 color=_BaseColor.rgb*lerp(.92,1.06,noise(p*.4));
                color*=1+(grain-.5)*micro*.2+(grit-.5)*micro*.08;
                float submerged=max(0,_WaterHeight-i.world.y);
                color=lerp(color,color*float3(.64,.89,.88),saturate(submerged*.25));
                float3 illumination=SampleSH(n)*.82+light.color*saturate(dot(n,light.direction))*light.shadowAttenuation*.72;
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float broadSheen=pow(saturate(dot(n,normalize(view+light.direction))),14)*.055;
                color=color*illumination+light.color*broadSheen*light.shadowAttenuation;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
