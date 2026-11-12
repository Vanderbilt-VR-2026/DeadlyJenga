Shader "Candy Tropical/Sand"
{
    Properties
    {
        _BaseColor("Warm molded sand",Color)=(.86,.67,.4,1)
        _WaterHeight("Water surface",Float)=.08
        _Grain("Sand grain",Range(0,1))=.55
        _ScenicStyle("Scenic style",Float)=0
        [HideInInspector] _CoastShadow("Castle shore shadow",2D)="black"{}
        [NoScaleOffset] _CoastSandGrain("Golden sand grains",2D)="white"{}
        [NoScaleOffset] _CoastCaustics("Refracted sunlight",2D)="black"{}
        [HideInInspector] _CatwalksCoast("Catwalks beach",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../../../Resources/CandyScenery/CandyCoastCaustics.hlsl"
            TEXTURE2D(_CoastSandGrain);SAMPLER(sampler_CoastSandGrain);
            TEXTURE2D(_CoastShadow);SAMPLER(sampler_CoastShadow);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _WaterHeight,_Grain,_ScenicStyle,_CatwalksCoast,_CoastShadowReady,_CoastDayWater,_CoastHasGrain;
            float4x4 _CoastShadowVP;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings vert(Attributes i){Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            float Ripple(float phase,float2 direction,float strength,inout float2 slope)
            {
                float s,c;
                sincos(phase,s,c);
                float coverage=1-smoothstep(.45,2,fwidth(phase));
                slope+=direction*c*strength*coverage;
                return s*coverage;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.world.xz;
                float distanceToEye=distance(i.world,_WorldSpaceCameraPos);
                float patch=noise(p*.34);
                float shoreHeight=i.world.y-_WaterHeight;
                float wet=1-smoothstep(-.03,.36+patch*.28,shoreHeight);

                // Moulded ridges shade against the sun and fall away on steep edges.
                float2 slope=0;
                float2 duneDirection=float2(.7,.9);
                float dunePhase=dot(p,duneDirection)+patch*2.8;
                float dune=Ripple(dunePhase,duneDirection,.14,slope);
                float2 rippleDirection=float2(5.7,-3.6);
                float ridgePhase=dot(p,rippleDirection)+sin(dunePhase)*1.55+patch*2;
                float ridge=Ripple(ridgePhase,rippleDirection*.15,.24,slope);
                float fine=Ripple(dot(p,float2(-15.3,12.6))+sin(ridgePhase)*.68,float2(-.77,.63),.09,slope);
                float facing=saturate(i.normal.y);
                slope*=facing*_Grain*lerp(1.2,.78,wet);
                float3 n=normalize(i.normal-float3(slope.x,0,slope.y));

                float2 grains=p*35;
                float grainFade=(1-smoothstep(4,19,distanceToEye))*
                    (1-smoothstep(.26,1.05,max(length(ddx(grains)),length(ddy(grains)))));
                float grain=(noise(grains)-.5)*grainFade*_Grain;
                if(_CatwalksCoast>.5 && grainFade>.01)
                {
                    float2 grit=float2(noise(grains+float2(.34,0)),noise(grains+float2(0,.34)))-
                        noise(grains);
                    n=normalize(n-float3(grit.x,0,grit.y)*(.64*grainFade*facing));
                }
                float3 color=_BaseColor.rgb*lerp(float3(.78,.64,.46),float3(1.11,1.06,.94),patch);
                color*=1+facing*(dune*.09+ridge*.075+fine*.025)+grain*(_CatwalksCoast>.5?.67:.26);
                if(_CatwalksCoast>.5 && _CoastDayWater>.5 && _CoastHasGrain>.5 && distanceToEye<27)
                {
                    float2 sandUV=float2(dot(p,float2(.94,.34)),dot(p,float2(-.34,.94)))*1.43;
                    float3 grainsColor=SAMPLE_TEXTURE2D(_CoastSandGrain,sampler_CoastSandGrain,sandUV).rgb;
                    float3 variation=clamp(grainsColor/float3(.716,.342,.109),.48,1.59);
                    color*=lerp(float3(1,1,1),variation,.72*(1-smoothstep(8,27,distanceToEye)));
                }
                color*=lerp(float3(1,1,1),float3(.76,.71,.64),wet);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                if(_ScenicStyle>2.5&&_ScenicStyle<3.5)color*=lerp(.95,1.12,noise(p*.7));
                if(_ScenicStyle>.5&&_ScenicStyle<1.5)color*=lerp(.55,1.2,noise(p*1.7));
                if(_ScenicStyle>3.5)color=lerp(color,float3(.88,.98,1),pow(saturate(noise(p*.9)),3)*.5);
                float submerged=max(0,_WaterHeight-i.world.y);
                color=lerp(color,color*float3(.64,.89,.88),saturate(submerged*.25));
                if(_CatwalksCoast>.5 && _CoastShadowReady>.5 && distanceToEye>21)
                {
                    float2 projected=mul(_CoastShadowVP,float4(i.world,1)).xy;
                    #if UNITY_UV_STARTS_AT_TOP
                        projected.y=-projected.y;
                    #endif
                    float2 uv=projected*.5+.5;
                    float inside=1-smoothstep(.44,.50,max(abs(uv.x-.5),abs(uv.y-.5)));
                    float cast=SAMPLE_TEXTURE2D_LOD(_CoastShadow,sampler_CoastShadow,uv,1.15).a;
                    light.shadowAttenuation=min(light.shadowAttenuation,1-cast*inside*.78*smoothstep(21,42,distanceToEye));
                }
                float3 illumination=SampleSH(n)*.65+
                    light.color*saturate(dot(n,light.direction))*light.shadowAttenuation*.68;
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float nh=saturate(dot(n,normalize(view+light.direction)));
                float sheen=pow(nh,10)*.035+pow(nh,36)*wet*.18;
                color=color*illumination+light.color*sheen*light.shadowAttenuation;
                if(_CatwalksCoast>.5)
                {
                    float fade=(1-smoothstep(1.1,4.8,submerged))*(1-smoothstep(17,73,distanceToEye));
                    float movingDepth=submerged+.045*sin(dot(p,float2(.83,.57))-_Time.y*1.38);
                    float glimmer=CandyRefractedLight(p,submerged)*fade*smoothstep(.015,.21,movingDepth);
                    float dry=smoothstep(.0,.29,shoreHeight);
                    color*=lerp(float3(1.02,.91,.75),float3(1.46,1.19,.89),dry);
                    color=lerp(color,color*float3(.55,.80,.87),saturate(submerged*.24));
                    // Scattered light reaches the reef from the surrounding sunlit water.
                    float indirect=smoothstep(.04,.38,submerged)*(1-smoothstep(1.1,4.9,submerged));
                    color+=_BaseColor.rgb*float3(.37,.34,.25)*indirect;
                    float refractedSun=lerp(light.shadowAttenuation,max(.59,light.shadowAttenuation),indirect);
                    color+=float3(1.37,.98,.42)*glimmer*refractedSun*saturate(light.color.r);
                    float eyeDepth=_WaterHeight-_WorldSpaceCameraPos.y;
                    if(_CoastDayWater>.5 && eyeDepth<=.08)
                    {
                        float lagoon=smoothstep(.24,1.08,submerged)*(1-smoothstep(2.65,5.2,submerged));
                        color=color*lerp(float3(1,1,1),float3(.44,1.09,1.14),lagoon);
                        color+=float3(.006,.075,.062)*lagoon;
                    }
                    if(eyeDepth>.08)
                    {
                        float visibility=exp2(-distanceToEye*.072);
                        float3 scatter=lerp(float3(.006,.13,.23),float3(.009,.28,.36),1-saturate(submerged*.19));
                        float warmShelf=1-smoothstep(1.8,5.2,submerged);
                        float3 sunlitSand=color*lerp(float3(.95,1.03,1.09),float3(2.12,1.41,1.27),warmShelf);
                        float3 filtered=lerp(scatter,sunlitSand,visibility);
                        color=lerp(color,filtered,smoothstep(.08,.19,eyeDepth));
                    }
                }
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
