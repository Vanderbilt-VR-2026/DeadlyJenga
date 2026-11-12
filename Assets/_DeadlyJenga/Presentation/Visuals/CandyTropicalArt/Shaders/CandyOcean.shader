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
        [HideInInspector] _ReefC("Reef C",Vector)=(0,0,1,1)
        [HideInInspector] _ReefD("Reef D",Vector)=(0,0,1,1)
        [NoScaleOffset] _CoastCaustics("Refracted sunlight",2D)="black"{}
        [NoScaleOffset] _CoastPanorama("Island cloud panorama",2D)="black"{}
        [HideInInspector] _CoastReflection("Mirrored castle",2D)="black"{}
        [HideInInspector] _CoastSpectrum("Wind wave slopes and crest breakup",2D)="gray"{}
        [HideInInspector] _CoastIslet("Palm islet",Vector)=(0,0,1,1)
        [HideInInspector] _CoastIsletB("Palm islet B",Vector)=(0,0,1,1)
        [HideInInspector] _CoastIsletC("Palm islet C",Vector)=(0,0,1,1)
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
            float4 _TerrainBounds,_SoakingPool,_ReefA,_ReefB,_ReefC,_ReefD,_CoastIslet,_CoastIsletB,_CoastIsletC;
            float _CatwalksCoast,_CoastDayWater,_CoastHasPanorama,_CoastReflectionReady;
            float4x4 _CoastReflectionVP;
            float3 _CoastSun;
            float _RippleScale,_Speed,_Glints,_Shine,_FoamStrength;
            float4 _SurgeOrigin, _SurgeDirection;
            float _SurgeWidth, _SurgeDepth, _SurgeHeight;
            float _WaveHeight,_WaveSpeed,_WaveChop,_WaveExtent,_Submersion,_SandTop,_SandDepth,_Plateau,_WaterHeight,_HasTerrain,_CoastalDetail;
            CBUFFER_END
            #include "CandySurgeSurface.hlsl"
            float _CandyOutage;
            TEXTURE2D(_TerrainHeight);SAMPLER(sampler_TerrainHeight);
            TEXTURE2D(_CoastSpectrum);SAMPLER(sampler_CoastSpectrum);
            TEXTURE2D(_CoastPanorama);SAMPLER(sampler_CoastPanorama);
            TEXTURE2D(_CoastReflection);SAMPLER(sampler_CoastReflection);
            #include "../../../Resources/CandyScenery/CandyCoastPanorama.hlsl"
            #include "../../../Resources/CandyScenery/CandyCoastCaustics.hlsl"
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
                float deep=smoothstep(.30,4.9,depth);
                float2 wind=float2(dot(position,float2(.87,.49)),dot(position,float2(-.49,.87)));
                float2 warp=float2(sin(dot(position,float2(.037,-.051))),sin(dot(position,float2(.054,.033))));
                float4 broad=SAMPLE_TEXTURE2D(_CoastSpectrum,sampler_CoastSpectrum,
                    wind*float2(.0055,.020)+warp*.031-float2(.006,.012)*time);
                float4 medium=SAMPLE_TEXTURE2D(_CoastSpectrum,sampler_CoastSpectrum,
                    (wind+warp*1.9)*float2(.022,.035)+float2(.22,.47)-float2(.014,.006)*time);
                float4 fine=SAMPLE_TEXTURE2D(_CoastSpectrum,sampler_CoastSpectrum,
                    position*.088+float2(.61,.19)-float2(.033,.018)*time);
                float crest;
                float2 swell=SwellSlope(position,time,distanceToEye,crest);
                float detail=1-smoothstep(33,118,distanceToEye);
                float packet=sin(dot(wind,float2(.027,-.034))-time*.11);
                float windPhase=dot(wind,float2(.095,.32))+warp.x*.95+packet*1.63+
                    (broad.b-.5)*2.1-time*.91;
                float windCrest, windSlope;
                sincos(windPhase,windCrest,windSlope);
                float clean=1-smoothstep(.26,1.24,fwidth(windPhase));
                float swellStrength=lerp(.42,1,smoothstep(-.8,.83,packet));
                float2 dominant=float2(-.25,.97)*windSlope*clean*swellStrength*.092;
                float2 slope=swell*.16+dominant+(broad.rg-.5)*.16+(medium.rg-.5)*.085+
                    (fine.rg-.5)*(.078*detail);
                float3 normal=normalize(float3(i.normal.x*.60,1,i.normal.z*.60)+float3(slope.x,0,slope.y)*lerp(.44,1,deep));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float3 sun=normalize(_CoastSun);
                float eyeDepth=_WaterHeight-_WorldSpaceCameraPos.y;
                if(eyeDepth>.10 && view.y<0)
                {
                    // Looking up from below, most of the surface reflects the water column.
                    float3 underside=-normal;
                    float incidence=saturate(dot(underside,view));
                    float window=smoothstep(.635,.70,incidence);
                    float ripple=smoothstep(.44,.78,medium.a*.70+fine.a*.30);
                    float3 transmittedSun=refract(-sun,normal,1.0/1.333);
                    float alignment=saturate(dot(view,transmittedSun));
                    float lens=pow(alignment,28);
                    float sparkle=lens*(.10+pow(ripple,3)*.90)+pow(alignment,105)*.34;
                    float filament=1-smoothstep(.055,.22,abs(medium.b-.56+fine.r*.07));
                    float3 reflected=lerp(float3(.003,.110,.285),float3(.015,.52,.69),ripple);
                    float3 transmission=lerp(float3(.08,.54,.68),float3(.34,.81,.89),saturate(incidence));
                    float lightBands=CandyRefractedLight(i.world.xz*.56,eyeDepth);
                    float crestLine=smoothstep(.23,.66,lightBands)*smoothstep(.27,.68,ripple);
                    float3 below=lerp(reflected,transmission,window*.88)+
                        float3(.18,.52,.65)*lightBands*(.30+.70*lens)+float3(.49,.66,.64)*crestLine*.18+
                        float3(.20,.66,.68)*filament*ripple*.29+float3(1.69,1.29,.65)*sparkle;
                    return half4(below,.92);
                }

                float fresnel=.018+.982*pow(1-saturate(dot(normal,view)),5);
                float3 reflection=reflect(-view,normal);
                float3 reflectedSky=lerp(float3(.29,.65,.95),float3(.032,.23,.64),smoothstep(0,.58,reflection.y));
                UNITY_BRANCH if(_CoastHasPanorama>.5)
                    reflectedSky=lerp(reflectedSky,CandyCoastClouds(reflection),.74);
                float reflectionCoverage=0;
                UNITY_BRANCH if(_CoastReflectionReady>.5)
                {
                    float4 reflectedPosition=mul(_CoastReflectionVP,float4(i.world.x,_WaterHeight,i.world.z,1));
                    float2 ndc=reflectedPosition.xy/max(.001,reflectedPosition.w);
                    #if UNITY_UV_STARTS_AT_TOP
                        ndc.y=-ndc.y;
                    #endif
                    float2 uv=ndc*.5+.5+(slope+normal.xz*.32)*(.026+.043*deep);
                    float edge=1-smoothstep(.43,.50,max(abs(uv.x-.5),abs(uv.y-.5)));
                    half4 reflectedCastle=SAMPLE_TEXTURE2D_LOD(_CoastReflection,sampler_CoastReflection,uv,lerp(.65,1.6,deep));
                    reflectionCoverage=saturate(reflectedCastle.a*edge)*step(.001,reflectedPosition.w);
                    // The reflection target is premultiplied after its mip chain blends the silhouette.
                    half3 castle=reflectedCastle.rgb/max(.04,reflectedCastle.a);
                    reflectedSky=lerp(reflectedSky,castle,reflectionCoverage*.88);
                }
                float wave=windCrest*clean*swellStrength*.15+crest*.18+(broad.b-.5)*.20+(medium.b-.5)*.10;
                float3 blue=lerp(float3(.002,.055,.226),float3(.003,.119,.405),smoothstep(-.36,.36,wave));
                float reefTint=smoothstep(.13,2.5,depth);
                float3 turquoise=lerp(float3(.001,.54,.36),float3(.001,.45,.63),reefTint);
                turquoise*=.98+wave*.36;
                float3 color=lerp(turquoise,blue,deep);
                float reflectAmount=fresnel*lerp(.17,.53,deep);
                float3 blueSkyReflection=reflectedSky*float3(.55,.77,1);
                reflectedSky=lerp(blueSkyReflection,reflectedSky,max(reflectionCoverage,smoothstep(.66,1,fresnel)));
                color=lerp(color,reflectedSky,max(reflectAmount,reflectionCoverage*.49));
                float alignment=saturate(dot(normal,normalize(sun+view)));
                float glintMask=smoothstep(.48,.75,medium.a*.56+fine.a*.44);
                float glint=pow(alignment,20)*.085+pow(alignment,88)*(.026+glintMask*.93);
                float horizon=smoothstep(360,780,distanceToEye);
                float sunPath=pow(saturate(dot(normalize(view.xz+float2(.0001,0)),float2(.20,-.98))),7);
                float crestFlash=smoothstep(.83,.98,windCrest)*clean*smoothstep(.53,.73,medium.a)*
                    smoothstep(.36,.66,fine.a);
                float fineFlash=smoothstep(.67,.84,fine.a)*smoothstep(.50,.73,medium.a);
                glint+=(sunPath*.82+.12)*crestFlash*lerp(.58,.14,horizon);
                glint+=fineFlash*smoothstep(.14,.78,alignment)*clean*.13;
                color+=float3(1,.87,.64)*glint*(1-horizon*.65)*lerp(.34,1,deep);
                color=lerp(color,float3(.15,.43,.70),horizon*.30);

                float shoreDepth=depth+(fine.b-.5)*.091+wave*.034;
                float laceWave=abs(sin(wind.x*13+fine.r*5)+sin(wind.y*19+medium.b*5));
                float foamDetail=1-smoothstep(.22,1.25,fwidth(wind.x*13)+fwidth(wind.y*19));
                float lace=(1-smoothstep(.08,.46,laceWave))*foamDetail;
                float broken=smoothstep(.29,.66,medium.a*.51+fine.a*.49);
                float lip=smoothstep(.010,.032,shoreDepth)*(1-smoothstep(.062,.107,shoreDepth));
                float wash=smoothstep(.027,.053,shoreDepth)*(1-smoothstep(.14,.25,shoreDepth));
                float islet=1-smoothstep(.013,.056,abs(isletRadius-1.02+(fine.b-.5)*.018));
                float foam=max(max(lip*(.27+broken*.71)+wash*lace*.62,islet*.61),CandySurgeWhitecaps(i.world.xz));
                color=lerp(color,float3(1,.97,.88),saturate(foam));
                float clarity=max(reefClear*.86,1-smoothstep(.48,4.7,depth));
                float shelf=smoothstep(.24,1.2,depth)*(1-smoothstep(2.1,4.6,depth));
                color=lerp(color,float3(.001,.47,.54),shelf*.24);
                float alpha=lerp(.995,.22+reefTint*.32+fresnel*.10,clarity);
                float shimmer=smoothstep(.46,.79,broad.a*.35+medium.a*.53+fine.a*.12);
                color+=float3(.018,.20,.23)*shimmer*clarity;
                return half4(MixFog(color,i.fog),max(max(alpha,foam),reflectionCoverage*.67));
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
                    reefRadius=min(min(ReefRadius(i.world.xz,_ReefA),ReefRadius(i.world.xz,_ReefB)),
                        min(ReefRadius(i.world.xz,_ReefC),ReefRadius(i.world.xz,_ReefD)));
                    isletRadius=min(ReefRadius(i.world.xz,_CoastIslet),
                        min(ReefRadius(i.world.xz,_CoastIsletB),ReefRadius(i.world.xz,_CoastIsletC)));
                    float isletShallow=1-smoothstep(.98,1.50,isletRadius);
                    depth=lerp(depth,min(depth,1.28),isletShallow);
                    reefClear=1-smoothstep(.48,1.13,reefRadius);
                }
                if(_CatwalksCoast>.5 && _CoastDayWater>.5 && !pool && _CandyOutage<.5)
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
