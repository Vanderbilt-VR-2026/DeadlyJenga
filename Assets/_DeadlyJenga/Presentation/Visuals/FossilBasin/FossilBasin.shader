Shader "DeadlyJenga/Fossil Basin"
{
    Properties { _Surface("Surface", Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct In { float4 pos:POSITION; float3 normal:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Out { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half3 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _Surface;
            CBUFFER_END
            Out vert(In v)
            {
                Out o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.pos.xyz);
                o.pos=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normal);o.color=v.color.rgb;return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 frag(Out i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 ray=normalize(i.world-GetCameraPositionWS());
                float3 sun=normalize(float3(-.47,.14,.91));
                if(_Surface>1.5)
                {
                    float h=max(0,ray.y);
                    float3 sky=lerp(float3(.96,.30,.19),float3(.135,.095,.37),smoothstep(0,.46,h));
                    float2 uv=ray.xz/(h+.12);
                    float cloud=noise(uv*float2(1.7,3.7))*.68+noise(uv*5.6)*.32;
                    sky=lerp(sky,float3(.98,.31,.33),smoothstep(.49,.71,cloud)*smoothstep(.005,.12,h)*.74);
                    float ridge=.024+.022*noise(ray.xz*48)+.016*noise(ray.xz*100);
                    sky=lerp(sky,float3(.34,.16,.30),1-smoothstep(ridge-.003,ridge,h));
                    float glow=saturate(dot(ray,sun));
                    return half4(sky+float3(1,.52,.18)*pow(glow,120)+float3(1,.92,.65)*smoothstep(.99956,.99975,glow),1);
                }
                if(_Surface>.5)
                {
                    float2 p=i.world.xz;
                    float wave=noise(p*float2(.86,3.3)+_Time.y*.10)*.65+noise(p*float2(1.7,8)-_Time.y*.17)*.35;
                    float shine=smoothstep(.64,.83,wave);
                    float reflected=pow(saturate(dot(normalize(ray.xz),normalize(sun.xz))),32);
                    return half4(float3(.023,.017,.033)+wave*float3(.016,.012,.024)+shine*(float3(.09,.055,.085)+reflected*float3(.73,.28,.07)),1);
                }
                float3 n=normalize(i.normal);
                float3 light=normalize(float3(-.49,.76,-.42));
                float diff=saturate(dot(n,light));
                float warm=pow(saturate(dot(n,normalize(light-ray))),28);
                float patina=noise(i.world.xz*9+i.world.y*.9);
                float3 pigment=i.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                pigment=SRGBToLinear(pigment);
                #endif
                return half4(pigment*(float3(.48,.42,.45)+diff*float3(.85,.77,.61))*(.94+.09*patina)+warm*.16,1);
            }
            ENDHLSL
        }
    }
}
