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
        [HideInInspector] _CatwalksCoast("Catwalks reef coast",Float)=0
        [HideInInspector] _ReefA("Reef A",Vector)=(0,0,1,1)
        [HideInInspector] _ReefB("Reef B",Vector)=(0,0,1,1)
        [HideInInspector] _CoastSpectrum("Wind wave slopes and crest breakup",2D)="gray"{}
        [HideInInspector] _CoastIslet("Palm islet",Vector)=(0,0,1,1)
        [HideInInspector] _CoastalDetail("Physical coast detail",Float)=0
        [HideInInspector] _SoakingPool("Soaking pool mode, depth and fade",Vector)=(0,0,0,0)
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
            float4 _TerrainBounds,_SoakingPool,_ReefA,_ReefB,_CoastIslet;
            float _CatwalksCoast;
            float _RippleScale,_Speed,_Glints,_Shine,_FoamStrength;
            float4 _SurgeOrigin, _SurgeDirection;
            float _SurgeWidth, _SurgeDepth, _SurgeHeight;
            float _WaveHeight,_WaveSpeed,_WaveChop,_WaveExtent,_Submersion,_SandTop,_SandDepth,_Plateau,_WaterHeight,_HasTerrain,_CoastalDetail;
            CBUFFER_END
            #include "CandySurgeSurface.hlsl"
            float _CandyOutage;
            TEXTURE2D(_TerrainHeight);SAMPLER(sampler_TerrainHeight);
            TEXTURE2D(_CoastSpectrum);SAMPLER(sampler_CoastSpectrum);
            struct Attributes{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 poolLocal:TEXCOORD2;float fog:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
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
            float ReefRadius(float2 world,float4 reef)
            {
                return length((world-reef.xy)/max(float2(1,1),reef.zw));
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
                o.poolLocal=i.positionOS.xz*2;
                if(_SoakingPool.x>.5)
                {
                    o.world=world;o.normal=float3(0,1,0);
                    o.positionCS=TransformWorldToHClip(world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
                }
                float fade=1-smoothstep(_WaveExtent*.76,_WaveExtent,max(abs(p.x),abs(p.y)));
                float depth=max(0,_WaterHeight-TerrainHeight(world.xz));
                float amplitude=_WaveHeight*fade*lerp(.14,1,smoothstep(.15,2.2,depth));
                float3 offset=0,dx=float3(1,0,0),dz=float3(0,0,1);float time=_Time.y*_WaveSpeed;
                Wave(float2(.94,.342),11,amplitude*.55,p,time,offset,dx,dz);
                Wave(float2(-.6,.8),7.5,amplitude*.3,p,time*.87,offset,dx,dz);
                Wave(float2(.24,-.971),17,amplitude*.15,p,time*.68,offset,dx,dz);
                float3 wake=_SurgeHeight>.001?(CandySurgeWakeSample(world.xz)+CandySurgeOuterSample(world.xz)):0;
                o.world=world+offset+float3(0,wake.x,0);o.normal=normalize(cross(dz,dx)+float3(wake.y,0,wake.z));
                o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            float2 SwellSlope(float2 p,float time,float distanceToEye,out float crest)
            {
                // The coarse outer mesh stays flat. These slopes keep its lighting moving.
                float2 direction=float2(.25,.14),crossDirection=float2(-.21,.27);
                float wind=sin(dot(p,float2(-.064,.087))+time*.18);
                float group=sin(dot(p,float2(.071,.036))-time*.22+wind);
                float phase=dot(p,direction)-time*1.27+wind*1.8+group*.6;
                float crossPhase=dot(p,crossDirection)-time*.94+group*1.25;
                float filterA=1-smoothstep(.35,2.2,fwidth(phase));
                float filterB=1-smoothstep(.35,2.2,fwidth(crossPhase));
                float s,c,cs,cc;
                sincos(phase,s,c);
                sincos(crossPhase,cs,cc);
                float visibility=1-smoothstep(300,730,distanceToEye);
                float height=lerp(.45,1,saturate(_WaveHeight*3.5))*visibility;
                float mainPacket=lerp(.2,.82,smoothstep(-.9,.65,group))*filterA;
                float crossPacket=lerp(.27,.74,smoothstep(-.65,.8,wind))*filterB;
                crest=(s*mainPacket+cs*crossPacket)*height;
                return -(direction*c*mainPacket+crossDirection*cc*crossPacket)*height;
            }
            half4 CatwalksWater(Varyings i,float depth,float reefClear,float isletRadius,float distanceToEye)
            {
                float2 position=i.world.xz-_Island.xy;
                float time=_Time.y;
                float deep=smoothstep(.13,4.4,depth);
                // Two independent wind directions cross the slow swell. Mips attenuate distant ripples.
                float4 broad=SAMPLE_TEXTURE2D(_CoastSpectrum,sampler_CoastSpectrum,position*.010-float2(.008,.004)*time);
                float2 crossed=float2(.62*position.x-.78*position.y,.78*position.x+.62*position.y);
                float4 medium=SAMPLE_TEXTURE2D(_CoastSpectrum,sampler_CoastSpectrum,crossed*.024+float2(.22,.47)-float2(.022,.007)*time);
                float4 fine=SAMPLE_TEXTURE2D(_CoastSpectrum,sampler_CoastSpectrum,position*.075+float2(.61,.19)-float2(.041,.024)*time);
                float rippleFade=1-smoothstep(22,105,distanceToEye);
                float2 slope=(broad.rg-.5)*.42+(medium.rg-.5)*.16+(fine.rg-.5)*(.07*rippleFade);
                float3 surfaceNormal=normalize(float3(i.normal.x*.54,1,i.normal.z*.54)+float3(slope.x,0,slope.y)*lerp(.48,1,deep));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float angle=saturate(dot(surfaceNormal,view));
                float fresnel=.025+.975*pow(1-angle,5);
                float3 reflection=reflect(-view,surfaceNormal);
                float3 reflectedSky=lerp(float3(.27,.63,.90),float3(.052,.29,.71),smoothstep(0,.63,reflection.y));
                float cloud=smoothstep(.57,.82,broad.a*.65+medium.a*.35);
                reflectedSky=lerp(reflectedSky,float3(.64,.80,.94),cloud*.25);

                float crest=(broad.b-.5)*.88+(medium.b-.5)*.45;
                float3 blue=lerp(float3(.003,.050,.21),float3(.008,.182,.44),smoothstep(-.29,.32,crest));
                float reefTint=smoothstep(.05,1.4,depth);
                float3 turquoise=lerp(float3(.009,.55,.39),float3(.002,.43,.51),reefTint);
                turquoise*=.94+crest*.38;
                float3 color=lerp(turquoise,blue,deep);
                color=lerp(color,reflectedSky,fresnel*lerp(.20,.46,deep));
                float3 sun=normalize(float3(.65,.28,.71));
                float alignment=saturate(dot(surfaceNormal,normalize(sun+view)));
                float glintMask=smoothstep(.51,.77,medium.a*.46+fine.a*.54);
                float glint=pow(alignment,23)*.055+pow(alignment,122)*(.02+glintMask*.95);
                float horizon=smoothstep(350,730,distanceToEye);
                color+=float3(1,.80,.55)*glint*(1-horizon*.75)*lerp(.33,1,deep);
                color=lerp(color,_Horizon.rgb,horizon*.52);

                float shoreDepth=depth+(fine.b-.5)*.042;
                float lip=smoothstep(.009,.026,shoreDepth)*(1-smoothstep(.049,.082,shoreDepth));
                float wash=smoothstep(.018,.048,shoreDepth)*(1-smoothstep(.08,.16,shoreDepth));
                float2 frothPosition=position*14+(fine.rg-.5)*3.5;
                float mesh=abs(sin(frothPosition.x)+sin(frothPosition.y));
                float foamDetail=1-smoothstep(.26,1.3,fwidth(frothPosition.x)+fwidth(frothPosition.y));
                float lace=(1-smoothstep(.11,.48,mesh))*foamDetail;
                float islet=1-smoothstep(.009,.06,abs(isletRadius-1.02));
                float foam=max(max(lip*.72+wash*lace*.33,islet*.65),CandySurgeWhitecaps(i.world.xz));
                color=lerp(color,float3(1,.96,.80),saturate(foam));
                float clarity=max(reefClear*.81,1-smoothstep(.36,3.9,depth));
                float alpha=lerp(.99,.12+reefTint*.18+fresnel*.34,clarity);
                if(_WorldSpaceCameraPos.y<_WaterHeight-.12) alpha=lerp(alpha,.23,clarity*.85);
                return half4(MixFog(color,i.fog),max(alpha,foam));
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float distanceToEye=distance(i.world,_WorldSpaceCameraPos),r=IslandDistance(i.world.xz);
                bool pool=_SoakingPool.x>.5;
                float poolEdge=pool?1-smoothstep(.74,1,length(i.poolLocal)):1;
                float depth=pool?max(.01,_SoakingPool.y*poolEdge):max(.01,i.world.y-TerrainHeight(i.world.xz));
                float reefRadius=2;
                float reefClear=0,isletRadius=3;
                if(_CatwalksCoast>.5 && !pool)
                {
                    reefRadius=min(ReefRadius(i.world.xz,_ReefA),ReefRadius(i.world.xz,_ReefB));
                    isletRadius=ReefRadius(i.world.xz,_CoastIslet);
                    float isletShallow=1-smoothstep(.98,1.50,isletRadius);
                    depth=lerp(depth,min(depth,1.28),isletShallow);
                    reefClear=1-smoothstep(.48,1.13,reefRadius);
                }
                if(_CatwalksCoast>.5 && !pool && _CandyOutage<.5)
                    return CatwalksWater(i,depth,reefClear,isletRadius,distanceToEye);
                float deep=smoothstep(.15,3.2,depth);
                float2 p=i.world.xz*_RippleScale;float t=_Time.y*_Speed;
                float detailFade=(1-smoothstep(25,140,distanceToEye))*(1-smoothstep(.15,.8,max(length(ddx(p)),length(ddy(p)))));
                float rippleA=sin(p.x*2.1+p.y*1.3+t),rippleB=sin(p.y*2.4-p.x*1.1-t*.85);
                float crest=0;
                float2 swell=0;
                if(!pool)swell=SwellSlope(i.world.xz-_Island.xy,_Time.y*_WaveSpeed,distanceToEye,crest);
                float windPhase=dot(p,float2(.54,-.74))+sin(p.x*.13+t)*.9-t;
                float chopPhase=dot(p,float2(.97,.46))+sin(p.y*.19-t)*1.3+t*.73;
                float2 windChop=float2(cos(windPhase)*(1-smoothstep(.45,2,fwidth(windPhase))),
                    cos(chopPhase)*(1-smoothstep(.45,2,fwidth(chopPhase))));
                float windFade=1-smoothstep(130,420,distanceToEye);
                float2 slope=float2(rippleA,rippleB)*(.055*detailFade)+
                    (swell*.43+windChop*(.038*windFade))*lerp(.15,1,deep)*(pool?0:1);
                if(_CatwalksCoast>.5)
                {
                    float2 q=i.world.xz;
                    float warp=sin(dot(q,float2(.19,.39))-_Time.y*.48);
                    float waveA=dot(q,float2(1.41,2.18))+.83*warp-_Time.y*1.15;
                    float waveB=dot(q,float2(-3.3,1.44))+.66*warp-_Time.y*1.73;
                    float2 shape=float2(cos(waveA)*(1-smoothstep(.26,1.8,fwidth(waveA))),cos(waveB)*(1-smoothstep(.26,1.8,fwidth(waveB))));
                    slope+=shape*.098*(1-smoothstep(190,490,distanceToEye))*lerp(.27,1,deep);
                }
                float3 n=normalize(i.normal+float3(slope.x,0,slope.y));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);Light light=GetMainLight();
                if(r<1.4)light.shadowAttenuation=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.world));
                float fresnel=.025+.975*pow(1-saturate(dot(n,view)),5);
                float3 reflected=reflect(-view,n);
                float3 sky=lerp(_Horizon.rgb,_Reflection.rgb,pow(saturate(reflected.y),.45));
                float3 color=lerp(_Shallow.rgb,_Deep.rgb,deep);
                color*=lerp(.84,1,light.shadowAttenuation);
                float reflectionWeight=pool?.72:(_CatwalksCoast>.5?.23:.47);
                color=lerp(color,sky,saturate((.035+fresnel*reflectionWeight)*_Shine));
                float nh=saturate(dot(n,normalize(view+light.direction)));
                float spec=pow(nh,55)*.3+pow(nh,18)*.025;
                if(!pool)
                {
                    float face=dot(slope,normalize(view.xz+float2(.0001,0)));
                    float sheen=smoothstep(.035,.14,face)*(1-smoothstep(.32,.66,abs(view.y)));
                    float brightCrest=smoothstep(.78,1.3,crest);
                    color*=1+crest*.058*deep;
                    color+=sky*(sheen*(.055+.05*fresnel)+brightCrest*.025)*_Shine*deep;
                    spec+=pow(nh,20)*_Glints*.13;
                }
                color+=light.color*spec*_Shine*light.shadowAttenuation;
                // No thresholded white speckles or repeating crossed sparkle pattern.
                float foam=pool?0:CandySurgeWhitecaps(i.world.xz);
                if(!pool&&_CoastalDetail>.5)
                {
                    float2 water=i.world.xz-_Island.xy;
                    float band=sin(dot(water,float2(.53,.31))-_Time.y*1.5+sin(water.y*.12)*.65);
                    float visibility=1-smoothstep(.3,1.4,fwidth(band));
                    float surf=smoothstep(.015,.13,depth)*(1-smoothstep(.18,.74,depth));
                    foam=max(foam,surf*smoothstep(.47,.95,band)*visibility*lerp(.52,.78,_FoamStrength));
                }
                color=lerp(color,_Foam.rgb,foam);
                if(!pool)
                {
                    float farDepth=-TransformWorldToView(i.world).z;
                    float clipFade=smoothstep(.88,1,farDepth/_ProjectionParams.z);
                    float haze=smoothstep(340,780,distanceToEye);
                    color=lerp(color,_Horizon.rgb,max(haze,clipFade)*.68);
                }
                color=MixFog(color,i.fog);
                float alpha=lerp(.19,.99,deep);alpha=max(alpha,saturate(fresnel*.82+foam));
                if(pool)alpha*=poolEdge*_SoakingPool.z;
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
