Shader "Candy Tropical/Ocean"
{
    Properties
    {
        _Shallow("Clear lagoon tint",Color)=(.035,.82,.78,1)
        _Deep("Rich tropical blue",Color)=(.015,.32,.65,1)
        _Foam("Pearl foam",Color)=(.87,1,.97,1)
        _Reflection("Sky reflection tint",Color)=(.32,.68,.95,1)
        _Horizon("Horizon haze",Color)=(.48,.86,1,1)
        _RippleScale("Fine flowing ripple scale",Float)=1.25
        _Speed("Ripple flow",Float)=.42
        _Glints("Sun sparkle",Range(0,1))=.3
        _Shine("Candy gloss",Range(0,1))=.85
        _FoamStrength("Gentle crest foam",Range(0,1))=.2
        _WaveHeight("Wave height (metres)",Range(0,.5))=.32
        _WaveSpeed("Rolling wave speed",Range(0,2))=.9
        _WaveChop("Wave roundness",Range(0,1))=.4
        _WaveExtent("Detailed wave extent",Float)=72
        _Island("Island center XZ / radii",Vector)=(0,0,26,24)
        _Submersion("Support surface submersion",Float)=.08
        _SandTop("Sand support surface",Float)=0
        _SandDepth("Seabed depth",Float)=3.2
        _Plateau("Level support radius",Float)=.8
        _WaterHeight("Water surface",Float)=.08
        _TerrainHeight("Static terrain heights",2D)="black"{}
        _TerrainBounds("Terrain center / size",Vector)=(0,0,40,40)
        _HasTerrain("Terrain height map available",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Name "Clear candy lagoon"
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
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
            float4 _Shallow,_Deep,_Foam,_Reflection,_Horizon,_Island;
            float4 _TerrainBounds;
            float _RippleScale,_Speed,_Glints,_Shine,_FoamStrength;
            float4 _SurgeOrigin, _SurgeDirection;
            float _SurgeWidth, _SurgeDepth, _SurgeHeight;
            float _WaveHeight,_WaveSpeed,_WaveChop,_WaveExtent,_Submersion,_SandTop,_SandDepth,_Plateau,_WaterHeight,_HasTerrain;
            CBUFFER_END
            TEXTURE2D(_TerrainHeight);SAMPLER(sampler_TerrainHeight);
            struct Attributes{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float wave:TEXCOORD2;float fog:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
            float IslandDistance(float2 world)
            {
                float2 q=(world-_Island.xy)/max(_Island.zw,float2(1,1));float a=atan2(q.y,q.x);
                return length(q)/(1+.035*sin(a*5)+.025*sin(a*9));
            }
            float SandHeight(float r)
            {
                return _SandTop-.65*smoothstep(_Plateau,1.1,r)-max(.1,_SandDepth-.65)*smoothstep(1.05,1.65,r);
            }
            float TerrainHeight(float2 world)
            {
                if(_HasTerrain<.5)return SandHeight(IslandDistance(world));
                float2 uv=(world-_TerrainBounds.xy)/_TerrainBounds.zw+.5;
                if(any(uv<0)||any(uv>1))return _SandTop-_SandDepth;
                return _SandTop+SAMPLE_TEXTURE2D_LOD(_TerrainHeight,sampler_TerrainHeight,uv,0).r;
            }
            void Wave(float2 direction,float wavelength,float amplitude,float2 p,float time,inout float3 offset,inout float3 dx,inout float3 dz)
            {
                float k=6.2831853/wavelength,phase=k*dot(direction,p)-time*sqrt(9.81*k);
                float s,c;sincos(phase,s,c);float chop=_WaveChop*amplitude;
                offset+=float3(direction.x*chop*c,amplitude*s,direction.y*chop*c);
                dx+=float3(-direction.x*direction.x*chop*k*s,direction.x*amplitude*k*c,-direction.x*direction.y*chop*k*s);
                dz+=float3(-direction.x*direction.y*chop*k*s,direction.y*amplitude*k*c,-direction.y*direction.y*chop*k*s);
            }
            Varyings vert(Attributes i)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(i.positionOS.xyz);float2 p=world.xz-_Island.xy;
                float fade=1-smoothstep(_WaveExtent*.76,_WaveExtent,max(abs(p.x),abs(p.y)));
                float depth=max(0,_WaterHeight-TerrainHeight(world.xz));
                float amplitude=_WaveHeight*fade*lerp(.14,1,smoothstep(.15,2.2,depth));
                float3 offset=0,dx=float3(1,0,0),dz=float3(0,0,1);float time=_Time.y*_WaveSpeed;
                Wave(float2(.94,.342),11,amplitude*.55,p,time,offset,dx,dz);
                Wave(float2(-.6,.8),7.5,amplitude*.3,p,time*.87,offset,dx,dz);
                Wave(float2(.24,-.971),17,amplitude*.15,p,time*.68,offset,dx,dz);
                o.world=world+offset;o.normal=normalize(cross(dz,dx));o.wave=offset.y;
                o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float surgeCoverage=0;
                if(_SurgeHeight>.001)
                {
                    float2 delta=i.world.xz-_SurgeOrigin.xz;
                    float t=dot(delta,_SurgeDirection.xz)/max(.01,_SurgeDepth)+.5;
                    float across=abs(dot(delta,float2(_SurgeDirection.z,-_SurgeDirection.x)))/max(.01,_SurgeWidth*.5);
                    float shape=t<.72?smoothstep(0,.72,t):1-smoothstep(.72,1,t);
                    float elevation=_SurgeHeight*shape*(1-smoothstep(.85,1,across));
                    surgeCoverage=smoothstep(0,.4,elevation);clip(.4-elevation);
                }
                float distanceToEye=distance(i.world,_WorldSpaceCameraPos),r=IslandDistance(i.world.xz);
                float depth=max(.01,i.world.y-TerrainHeight(i.world.xz)),deep=smoothstep(.15,3.2,depth);
                float2 p=i.world.xz*_RippleScale;float t=_Time.y*_Speed;
                float detailFade=(1-smoothstep(25,140,distanceToEye))*(1-smoothstep(.15,.8,max(length(ddx(p)),length(ddy(p)))));
                float rippleA=sin(p.x*2.1+p.y*1.3+t),rippleB=sin(p.y*2.4-p.x*1.1-t*.85);
                float3 n=normalize(i.normal+float3(rippleA*.055,0,rippleB*.055)*detailFade);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);Light light=GetMainLight();
                if(r<1.4)light.shadowAttenuation=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.world));
                float fresnel=.025+.975*pow(1-saturate(dot(n,view)),5);
                float3 reflected=reflect(-view,n);
                float3 sky=lerp(_Horizon.rgb,_Reflection.rgb,pow(saturate(reflected.y),.45));
                float3 color=lerp(_Shallow.rgb,_Deep.rgb,deep);
                color*=lerp(.84,1,light.shadowAttenuation);
                color=lerp(color,sky,saturate((.035+fresnel*.72)*_Shine));
                float nh=saturate(dot(n,normalize(view+light.direction)));
                float spec=pow(nh,55)*.3+pow(nh,18)*.025;
                color+=light.color*spec*_Shine*light.shadowAttenuation;
                // No thresholded white speckles or repeating crossed sparkle pattern.
                float foam=0;
                color=MixFog(color,i.fog);color=lerp(color,_Horizon.rgb,smoothstep(240,700,distanceToEye));
                float alpha=lerp(.19,.99,deep);alpha=max(alpha,saturate(fresnel*.82+foam));
                return half4(color,alpha*(1-surgeCoverage));
            }
            ENDHLSL
        }
    }
}
