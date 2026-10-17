Shader "Candy Tropical/Sky"
{
    Properties
    {
        _Zenith("Blue sky",Color)=(.035,.37,.92,1)
        _Horizon("Horizon haze",Color)=(.48,.86,1,1)
        _Cloud("Cloud white",Color)=(1,.985,.94,1)
        _CloudCover("Cloud cover",Range(0,1))=.44
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Zenith,_Horizon,_Cloud;float _CloudCover;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 dir:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings vert(Attributes i){Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.dir=i.positionOS.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.dir);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.38));
                float2 p=d.xz/max(.12,d.y+.18)*2.8;
                float n=noise(p)*.58+noise(p*2.1+5)*.27+noise(p*4.4)*.15;
                float cloud=smoothstep(1-_CloudCover-.12,1-_CloudCover+.13,n)*smoothstep(.025,.12,d.y)*(1-smoothstep(.72,.98,d.y));
                float3 cloudColor=_Cloud.rgb*lerp(.82,1,saturate(n*1.3));
                color=lerp(color,cloudColor,cloud);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
