Shader "DeadlyJenga/Blackwater Spinosaurus"
{
    Properties { _Stroke("Stroke, surfacing and breach", Vector) = (0,0,0,0) }
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Stroke;
            CBUFFER_END
            struct Vertex { float4 pos:POSITION; half3 normal:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 pos:SV_POSITION; half3 normal:TEXCOORD0; half3 color:COLOR; half fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varying vert(Vertex v)
            {
                Varying o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=v.pos.xyz;
                float tail=saturate((-p.x-1.2)/7.1);
                float wave=_Stroke.x+p.x*.66;
                p.z+=1.45*tail*tail*sin(wave);
                float slope=1.45*(.66*tail*tail*cos(wave)-2*tail/7.1*sin(wave));
                half3 n=normalize(float3(v.normal.x-slope*v.normal.z,v.normal.yz));
                float limb=saturate((-p.y-.75)*1.2)*smoothstep(-3.2,-2.6,p.x)*(1-smoothstep(2.0,2.6,p.x));
                p.x+=limb*(.45*sin(_Stroke.x+sign(p.z)*1.55)-.24*_Stroke.z);
                p.y+=limb*.21*cos(_Stroke.x+sign(p.z)*1.55);
                // Packed jaw weight keeps its teeth and tongue attached during the gape.
                p.y+=v.color.a*max(0,p.x-2.65)*(.36-.29*_Stroke.z-.08*_Stroke.y);
                o.pos=TransformObjectToHClip(p);
                o.normal=TransformObjectToWorldNormal(n);
                o.color=v.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    o.color=SRGBToLinear(o.color);
                #endif
                o.fog=ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 frag(Varying i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                Light sun=GetMainLight();
                half3 n=normalize(i.normal);
                half3 light=max(SampleSH(n),half3(.23,.22,.25))+sun.color*(.18+.82*saturate(dot(n,sun.direction)));
                return half4(MixFog(i.color*light,i.fog),1);
            }
            ENDHLSL
        }
    }
}
