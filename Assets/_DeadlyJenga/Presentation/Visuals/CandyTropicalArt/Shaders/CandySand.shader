Shader "Candy Tropical/Sand"
{
    Properties
    {
        _BaseColor("Warm molded sand",Color)=(.86,.67,.4,1)
        _WaterHeight("Water surface",Float)=.08
        _Grain("Sand grain",Range(0,1))=.55
        _ScenicStyle("Scenic style",Float)=0
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
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _WaterHeight,_Grain,_ScenicStyle,_CatwalksCoast;
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
            float ShoreCaustics(float2 position)
            {
                float t=_Time.y*.54;
                float2 warped=position*3.1+float2(sin(position.y*1.12+t),sin(position.x*1.22-t))*.83;
                float first=sin(warped.x+t)+sin(warped.y-t*.76);
                float second=sin(dot(warped,float2(.47,.88))+t*.61);
                float stripe=abs(first+second*.54);
                float focus=1-smoothstep(.026,.18,stripe);
                return focus*(.55+.45*saturate(second));
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
                float3 color=_BaseColor.rgb*lerp(float3(.78,.64,.46),float3(1.11,1.06,.94),patch);
                color*=1+facing*(dune*.09+ridge*.075+fine*.025)+grain*.26;
                color*=lerp(float3(1,1,1),float3(.76,.71,.64),wet);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                if(_ScenicStyle>2.5&&_ScenicStyle<3.5)color*=lerp(.95,1.12,noise(p*.7));
                if(_ScenicStyle>.5&&_ScenicStyle<1.5)color*=lerp(.55,1.2,noise(p*1.7));
                if(_ScenicStyle>3.5)color=lerp(color,float3(.88,.98,1),pow(saturate(noise(p*.9)),3)*.5);
                float submerged=max(0,_WaterHeight-i.world.y);
                color=lerp(color,color*float3(.64,.89,.88),saturate(submerged*.25));
                float3 illumination=SampleSH(n)*.65+
                    light.color*saturate(dot(n,light.direction))*light.shadowAttenuation*.68;
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float nh=saturate(dot(n,normalize(view+light.direction)));
                float sheen=pow(nh,10)*.035+pow(nh,36)*wet*.18;
                color=color*illumination+light.color*sheen*light.shadowAttenuation;
                if(_CatwalksCoast>.5)
                {
                    float fade=(1-smoothstep(1.1,4.8,submerged))*(1-smoothstep(17,73,distanceToEye));
                    float glimmer=ShoreCaustics(p)*fade*smoothstep(-.04,.21,submerged);
                    color=lerp(color*float3(1.16,1.05,.83),color*float3(.40,.82,.80),saturate(submerged*.20));
                    color+=float3(1,.84,.48)*glimmer*.36*light.shadowAttenuation*saturate(light.color.r);
                }
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
