Shader "Candy Tropical/Catwalks Coast"
{
    Properties
    {
        [HideInInspector] _CoastStyle("Coast surface",Float)=0
        [HideInInspector] _CoastWater("Water elevation",Float)=0
        [HideInInspector] _CoastMotion("Swim motion",Float)=0
        [NoScaleOffset] _CoastCaustics("Refracted sunlight",2D)="black"{}
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
            #include "CandyCoastCaustics.hlsl"
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
            half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 normal=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                Light sun=GetMainLight();
                float distanceToEye=distance(i.world,_WorldSpaceCameraPos);
                float diffuse=saturate(dot(normal,sun.direction));
                half3 light=SampleSH(normal)*.56+sun.color*(.19+.83*diffuse);
                half3 color=i.color*light;
                float depth=max(0,_CoastWater-i.world.y);
                if(_CoastStyle>.5&&_CoastStyle<1.5)
                {
                    float detail=1-smoothstep(18,64,distanceToEye);
                    float caustic=CandyRefractedLight(i.world.xz,depth)*detail*(1-smoothstep(1.55,4.1,depth));
                    float sandRipple=sin(dot(i.world.xz,float2(5.9,7.6))+sin(i.world.z*1.5));
                    color*=1+sandRipple*.047*saturate(normal.y)*detail;
                    color=lerp(color,color*half3(.65,.92,.94),saturate(depth*.23));
                    color+=half3(1,.85,.47)*caustic*(.30+.44*saturate(normal.y))*sun.color;
                    float3 halfAngle=normalize(sun.direction+GetWorldSpaceNormalizeViewDir(i.world));
                    color+=pow(saturate(dot(normal,halfAngle)),30)*sun.color*.075;
                }
                if(_CoastStyle>1.5)
                {
                    half4 island=SAMPLE_TEXTURE2D(_CoastHorizon,sampler_CoastHorizon,i.uv);
                    clip(island.a-.38);
                    color=lerp(island.rgb,half3(.14,.39,.59),.24);
                }
                float eyeDepth=_CoastWater-_WorldSpaceCameraPos.y;
                if(eyeDepth>.08)
                {
                    float transmittance=exp2(-distanceToEye*.071);
                    half3 filtered=lerp(half3(.007,.18,.29),color*half3(.76,.98,1.03),transmittance);
                    color=lerp(color,filtered,smoothstep(.08,.19,eyeDepth));
                }
                color*=lerp(1,.025,saturate(_CandyOutage));
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Castle reflection"
            Tags { "LightMode"="CandyCoastReflection" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ReflectVert
            #pragma fragment ReflectFrag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:192
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(CastleReflection)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CoastBlockTint)
            UNITY_INSTANCING_BUFFER_END(CastleReflection)
            float4x4 _CoastReflectionVP;
            half3 _CoastLightDirection;
            float _CoastWater;

            struct ReflectionInput
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ReflectionOutput
            {
                float4 position : SV_POSITION;
                float height : TEXCOORD0;
                half3 tint : TEXCOORD1;
            };

            ReflectionOutput ReflectVert(ReflectionInput i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ReflectionOutput o;
                float3 world = TransformObjectToWorld(i.position.xyz);
                half3 n = TransformObjectToWorldNormal(i.normal);
                half light = .34 + .66 * saturate(dot(n, _CoastLightDirection));
                o.position = mul(_CoastReflectionVP, float4(world, 1));
                o.height = world.y - _CoastWater;
                o.tint = UNITY_ACCESS_INSTANCED_PROP(CastleReflection, _CoastBlockTint).rgb * light;
                return o;
            }

            half4 ReflectFrag(ReflectionOutput i) : SV_Target
            {
                clip(i.height);
                return half4(i.tint,1);
            }
            ENDHLSL
        }
    }
}
