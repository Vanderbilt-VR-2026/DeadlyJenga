Shader "Candy Tropical/Catwalks Coast"
{
    Properties
    {
        [HideInInspector] _CoastStyle("Coast surface",Float)=0
        [HideInInspector] _CoastWater("Water elevation",Float)=0
        [HideInInspector] _CoastMotion("Swim motion",Float)=0
        [NoScaleOffset] _CoastHorizon("Painted island chain",2D)="black"{}
        [NoScaleOffset] _CoastGranite("Island granite",2D)="gray"{}
        [NoScaleOffset] _CoastCanopy("Island canopy",2D)="gray"{}
        [NoScaleOffset] _CoastCaustics("Refracted sunlight",2D)="black"{}
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CandyCoastCaustics.hlsl"
            #define CANDY_COAST_MARINE_ONLY
            #include "CandyCoastPanorama.hlsl"
            TEXTURE2D(_CoastHorizon); SAMPLER(sampler_CoastHorizon);
            TEXTURE2D(_CoastGranite); SAMPLER(sampler_CoastGranite);
            TEXTURE2D(_CoastCanopy); SAMPLER(sampler_CoastCanopy);
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
                    if(i.uv.y>.5)
                    {
                        float3 pores=i.world*28;
                        float filter=1-smoothstep(.30,1.2,max(length(ddx(pores)),length(ddy(pores))));
                        float3 cells=abs(frac(pores+sin(pores.yzx*.18)*.32)-.5);
                        float polyps=1-smoothstep(.12,.31,length(cells));
                        color*=1+filter*(polyps*.24-.045);
                    }
                }
                if(_CoastStyle>1.5 && _CoastStyle<2.5)
                {
                    float stratum=sin(i.world.y*.82+sin(i.world.x*.043+i.world.z*.071)*.65);
                    float grain=sin(i.world.x*.76+i.world.z*.42)*sin(i.world.z*.54-i.world.x*.25);
                    float pockets=sin(i.world.x*.49+grain*.67)*sin(i.world.z*.41-i.world.x*.33);
                    float foliage=smoothstep(-.2,.61,pockets+normal.y*.33);
                    float isVegetation=smoothstep(.006,.072,i.color.g-i.color.r);
                    float rockWeight=smoothstep(.024,.16,i.color.r-i.color.g);
                    half3 granite=SAMPLE_TEXTURE2D(_CoastGranite,sampler_CoastGranite,
                        float2(dot(i.world.xz,float2(.82,.57)),i.world.y)*float2(.085,.10)).rgb;
                    half3 leaves=SAMPLE_TEXTURE2D(_CoastCanopy,sampler_CoastCanopy,i.world.xz*.095).rgb;
                    half3 graniteTone=lerp(half3(.71,.77,.98),half3(.91,.88,.84),diffuse);
                    half3 textured=lerp(leaves*half3(.81,1.01,.82),granite*graniteTone,rockWeight);
                    float groundDetail=smoothstep(.38,3.6,i.world.y-_CoastWater);
                    half3 baseColor=lerp(i.color,textured,groundDetail*lerp(.65,.71,rockWeight));
                    half3 skyBounce=half3(.17,.24,.31)*lerp(.61,1,saturate(normal.y));
                    color=baseColor*(skyBounce+sun.color*(.13+.77*diffuse));
                    color*=1+stratum*.19*(1-saturate(normal.y))+grain*.073;
                    color*=lerp(half3(1,1,1),lerp(half3(.69,.78,.78),half3(1.11,1.14,1),foliage),isVegetation*.73);
                }
                if(_CoastStyle>2.5 && _CoastStyle<3.5)
                {
                    half4 painted=SAMPLE_TEXTURE2D(_CoastHorizon,sampler_CoastHorizon,i.uv);
                    clip(painted.a-.57);
                    color=painted.rgb*half3(.86,.90,.97);
                }
                if(_CoastStyle>3.5 && _CoastStyle<4.5)
                {
                    float coastalBand=smoothstep(.025,.45,i.world.y-_CoastWater);
                    float strata=sin(i.world.y*15+sin(i.world.x*2.7+i.world.z*2.2));
                    float fleck=sin(i.world.x*17+i.world.z*8.1)*sin(i.world.z*16-i.world.x*5);
                    float detail=1-smoothstep(32,119,distanceToEye);
                    color*=lerp(half3(.61,.78,.78),half3(1,1,1),coastalBand);
                    color*=1+detail*(strata*.09*(1-saturate(normal.y))+fleck*.055);
                }
                if(_CoastStyle>4.5)
                {
                    float streak=sin(i.world.x*.38+i.world.z*.64+_Time.y*.57);
                    float breakup=sin(i.world.x*2.4-i.world.z*1.27+streak);
                    clip(breakup+streak*.51+.52);
                    color=i.color*(.67+.28*saturate(streak));
                }
                if(_CoastStyle>1.5 && _CoastStyle<3.5)
                {
                    float marineHeight=max(0,i.world.y-_CoastWater);
                    float wetRock=1-smoothstep(.18,2.5,marineHeight);
                    color*=lerp(half3(1,1,1),half3(.64,.80,.93),wetRock*.53);
                    float seaAir=exp2(-marineHeight*.077);
                    float air=1-exp2(-distanceToEye*lerp(.00042,.00072,seaAir));
                    float3 sight=normalize(i.world-_WorldSpaceCameraPos);
                    half3 scattering=CandyCoastMarineAir(sight,sun.direction)*half3(.63,.72,.79);
                    color=lerp(color,scattering,air);
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
