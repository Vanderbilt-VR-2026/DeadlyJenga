Shader "Candy Tropical/Catwalks Coast"
{
    Properties
    {
        [HideInInspector] _CoastStyle("Coast surface",Float)=0
        [HideInInspector] _CoastWater("Water elevation",Float)=0
        [HideInInspector] _CoastMotion("Swim motion",Float)=0
        [NoScaleOffset] _CoastHorizon("Horizon artwork",2D)="white"{}
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_CoastHorizon);SAMPLER(sampler_CoastHorizon);
            CBUFFER_START(UnityPerMaterial)
            float _CoastStyle,_CoastWater,_CoastMotion;
            CBUFFER_END
            float _CandyOutage;
            struct A {float4 position:POSITION;float3 normal:NORMAL;half4 color:COLOR;float2 motion:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half3 color:TEXCOORD2;float fog:TEXCOORD3;float2 uv:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                if(_CoastMotion>.5)
                {
                    float beat=sin(_Time.y*4.8+i.position.z*1.7)*i.motion.x;
                    i.position.y+=beat*.15;
                    i.position.x+=beat*.08;
                }
                o.world=TransformObjectToWorld(i.position.xyz);o.position=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);o.color=i.color.rgb;
                o.fog=ComputeFogFactor(o.position.z);o.uv=i.motion;return o;
            }
            float Caustic(float2 p)
            {
                float time=_Time.y;
                p*=1.65;
                float a=sin(dot(p,float2(2.2,1.7))+sin(p.y*1.25+time*.62)*.82-time*.65);
                float b=sin(dot(p,float2(-1.5,2.4))+sin(p.x*1.72-time*.44)*.7+time*.53);
                float c=sin(dot(p,float2(1.7,-2.1))+a*.93+time*.43);
                float stripe=abs(a+b+c*.56);
                return (1-smoothstep(.025,.22,stripe))*(.48+.52*saturate(a*.5+b*.5+.55));
            }
            half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 normal=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                Light sun=GetMainLight();
                float distanceToEye=distance(i.world,_WorldSpaceCameraPos);
                float diffuse=saturate(dot(normal,sun.direction));
                half3 light=SampleSH(normal)*.68+sun.color*(.19+.66*diffuse);
                half3 color=i.color*light;
                float depth=max(0,_CoastWater-i.world.y);
                if(_CoastStyle>.5&&_CoastStyle<1.5)
                {
                    float detail=1-smoothstep(18,64,distanceToEye);
                    float caustic=Caustic(i.world.xz)*detail*(1-smoothstep(1.55,4.1,depth));
                    float sandRipple=sin(dot(i.world.xz,float2(5.9,7.6))+sin(i.world.z*1.5));
                    color*=1+sandRipple*.047*saturate(normal.y)*detail;
                    color=lerp(color,color*half3(.43,.89,.91),saturate(depth*.32));
                    color+=half3(1,.90,.61)*caustic*(.15+.32*saturate(normal.y))*sun.color*.65;
                }
                if(_CoastStyle>1.5)
                {
                    half4 island=SAMPLE_TEXTURE2D(_CoastHorizon,sampler_CoastHorizon,i.uv);
                    clip(island.a-.38);
                    color=lerp(island.rgb,half3(.14,.39,.59),.24);
                }
                color*=lerp(1,.025,saturate(_CandyOutage));
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
