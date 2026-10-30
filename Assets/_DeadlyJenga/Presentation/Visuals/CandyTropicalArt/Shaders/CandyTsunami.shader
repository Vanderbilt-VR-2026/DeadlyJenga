Shader "Candy Tropical/Tsunami"
{
    Properties
    {
        _FoamDetail("Breaking foam detail",2D)="white"{}
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
            ZWrite On Cull Off
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
            #include "CandySurgeSurface.hlsl"
            TEXTURE2D(_TerrainHeight);SAMPLER(sampler_TerrainHeight);
            TEXTURE2D(_FoamDetail);SAMPLER(sampler_FoamDetail);
            struct Attributes{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float wave:TEXCOORD2;float fog:TEXCOORD3;float2 uv:TEXCOORD4;float whitewater:TEXCOORD5;float bulk:TEXCOORD6;float cap:TEXCOORD7;UNITY_VERTEX_OUTPUT_STEREO};
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
            float3 SurgePoint(float2 uv)
            {
                float phase=uv.y+.5;
                phase+=.09*sin(uv.x*_SurgeWidth*.075-_Time.y*.5)*sin(3.14159265*phase)*smoothstep(.35,1,phase);
                float2 curve=CandySurgeCurve(phase);
                float edge=1;
                return float3(uv.x*_SurgeWidth,curve.y*_SurgeHeight*edge*CandySurgeCrestScale(uv.x*_SurgeWidth),(curve.x-.5)*_SurgeDepth);
            }
            float3 SwellBackPoint(float2 uv)
            {
                float t=saturate(uv.y+.5),v=1-t;
                float3 tip=SurgePoint(float2(uv.x,.5));
                float scale=CandySurgeCrestScale(uv.x*_SurgeWidth);
                float footprint=.45+.55*sqrt(saturate(1-pow(abs(uv.x)/.65,2)));
                float2 a=float2(-1.6*_SurgeDepth*footprint,0),b=float2(-.45*_SurgeDepth*footprint,1.5*_SurgeHeight*scale),c=float2(.65*_SurgeDepth,1.32*_SurgeHeight*scale);
                float2 swell=v*v*v*a+3*v*v*t*b+3*v*t*t*c+t*t*t*tip.zy;
                return float3(uv.x*_SurgeWidth,swell.y,swell.x);
            }
            float3 SurfacePoint(float2 uv,float bulk){return bulk>.5?SwellBackPoint(uv):SurgePoint(uv);}
            Varyings vert(Attributes i)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float cap=step(2.5,i.positionOS.y),side=i.positionOS.y>3.5?1:-1;
                float2 uv=cap>.5?float2(side*.65,i.positionOS.x-.5):i.positionOS.xz;
                float whitewater=step(.5,i.positionOS.y)*(1-step(1.5,i.positionOS.y));
                float bulk=cap>.5?i.positionOS.z:step(1.5,i.positionOS.y);
                float3 local=SurfacePoint(uv,bulk);
                if(whitewater>.5)local=float3(uv.x*_SurgeWidth,.18,(uv.y*.85+.58)*_SurgeDepth);
                float y=local.y;
                // Extended ocean apron must stay flat outside the original wave span.
                if(uv.y<-.5||uv.y>.5)local.z=uv.y*_SurgeDepth;
                float3 world=TransformObjectToWorld(local);
                float3 tx=SurfacePoint(uv+float2(.001,0),bulk)-SurfacePoint(uv-float2(.001,0),bulk);
                float3 tz=SurfacePoint(uv+float2(0,.001),bulk)-SurfacePoint(uv-float2(0,.001),bulk);
                float3 surfaceNormal=normalize(cross(tz,tx)+float3(0,.000001,0));
                float3 n=TransformObjectToWorldNormal(cap>.5?normalize(float3(side,0,0)*.7+surfaceNormal*.3):surfaceNormal);
                float3 offset=0,dx=float3(1,0,0),dz=float3(0,0,1);
                float2 p=world.xz-_Island.xy;float time=_Time.y*_WaveSpeed;
                float fade=1-smoothstep(_WaveExtent*.76,_WaveExtent,max(abs(p.x),abs(p.y)));
                float oceanDepth=max(0,_WaterHeight-TerrainHeight(world.xz));
                float amplitude=_WaveHeight*fade*lerp(.14,1,smoothstep(.15,2.2,oceanDepth));
                Wave(float2(.94,.342),11,amplitude*.55,p,time,offset,dx,dz);
                Wave(float2(-.6,.8),7.5,amplitude*.3,p,time*.87,offset,dx,dz);
                Wave(float2(.24,-.971),17,amplitude*.15,p,time*.68,offset,dx,dz);
                float3 wake=CandySurgeWakeSample(world.xz);
                o.world=world+offset+float3(0,wake.x+.012,0);o.normal=normalize(n+((normalize(cross(dz,dx))-float3(0,1,0))+float3(wake.y,0,wake.z))*lerp(1,.1,smoothstep(.2,2,y)));o.wave=y;o.uv=uv;o.whitewater=whitewater;o.bulk=cap>.5?.65:bulk;o.cap=cap;
                o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // The ordinary ocean already renders the flat apron; drawing it twice z-fights.
                if(i.whitewater<.5)clip(i.wave-.035);
                float distanceToEye=distance(i.world,_WorldSpaceCameraPos),r=IslandDistance(i.world.xz);
                float depth=max(.01,i.world.y-TerrainHeight(i.world.xz)),deep=smoothstep(.15,3.2,depth);
                float2 p=(i.world.xz+float2(i.wave*.18,i.wave*.64))*_RippleScale;float t=_Time.y*_Speed;
                float detailFade=(1-smoothstep(25,140,distanceToEye))*(1-smoothstep(.15,.8,max(length(ddx(p)),length(ddy(p)))));
                float rippleA=sin(p.x*2.1+p.y*1.3+t),rippleB=sin(p.y*2.4-p.x*1.1-t*.85);
                float3 n=normalize(i.normal+float3(rippleA*.055,0,rippleB*.055)*detailFade);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);Light light=GetMainLight();
                n=dot(n,view)<0?-n:n;
                if(r<1.4)light.shadowAttenuation=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.world));
                float fresnel=.025+.975*pow(1-saturate(dot(n,view)),5);
                float3 reflected=reflect(-view,n);
                float3 sky=lerp(_Horizon.rgb,_Reflection.rgb,pow(saturate(reflected.y),.45));
                float thinLip=smoothstep(.68,1,i.uv.y+.5)*(1-i.bulk*.65);
                float3 color=lerp(_Shallow.rgb,_Deep.rgb,deep*(1-thinLip*.45));
                float striation=noise(float2(i.uv.x*_SurgeWidth*1.8+t*.8,(i.uv.y+.5)*_SurgeHeight*.38-t*1.8));
                color*=(.82+striation*.32)*lerp(1,.78,i.bulk);
                color*=lerp(.84,1,light.shadowAttenuation);
                color=lerp(color,sky,saturate((.035+fresnel*.72)*_Shine));
                float nh=saturate(dot(n,normalize(view+light.direction)));
                float spec=pow(nh,55)*.3+pow(nh,18)*.025;
                color+=light.color*spec*_Shine*light.shadowAttenuation;
                // No thresholded white speckles or repeating crossed sparkle pattern.
                float2 delta=i.world.xz-_SurgeOrigin.xz;
                float along=dot(delta,_SurgeDirection.xz)/_SurgeDepth+.5;
                float2 local=CandySurgeCoordinates(i.world.xz);
                float crest=smoothstep(.58,.78,i.uv.y+.5)*smoothstep(.5,2,_SurgeHeight);
                float2 flow=float2(i.uv.x*_SurgeWidth*.55,(i.uv.y+.5)*_SurgeHeight*1.7+t*2.2);
                float cells=noise(flow)*.65+noise(flow*2.7)*.35;
                float broken=smoothstep(.43,.68,cells);
                float streak=pow(saturate(noise(float2(local.x*.8,i.wave*.45-local.y*.2+t))*1.6-.6),3);
                float front=smoothstep(.70,.76,along)*(1-smoothstep(.86,1.01,along));
                float filaments=noise(float2(local.x*1.8+cells*2,i.wave*.22-local.y*.12+t*3));
                float2 foamUV=float2(i.uv.x*_SurgeWidth*.025,(i.uv.y+.5)*_SurgeHeight*.065-t*.05);
                if(i.cap>.5)foamUV=float2(local.y*.025,i.wave*.065-t*.05);
                float fineFoam=SAMPLE_TEXTURE2D(_FoamDetail,sampler_FoamDetail,foamUV+cells*.08).r;
                float fineLayer=SAMPLE_TEXTURE2D(_FoamDetail,sampler_FoamDetail,foamUV*1.73-float2(t*.07,t*.11)).r;
                float lace=saturate(fineFoam*.85+fineLayer*.4);
                float foamPatches=smoothstep(.46,.67,noise(foamUV*1.6+float2(t*.14,-t*.2)));
                float foam=saturate(lerp(lace*.28,lace*.85+.12,crest)+smoothstep(.85,1,i.uv.y+.5)*.45)*smoothstep(.12,1,i.wave);
                foam=max(foam,CandySurgeWhitecaps(i.world.xz)*(1-smoothstep(.2,1,i.wave)));
                if(i.whitewater>.5)
                {
                    float2 uvw=i.uv*2;
                    float margin=(1-smoothstep(.55,1,abs(uvw.x)))*(1-smoothstep(.45,1,abs(uvw.y)));
                    float froth=noise(local*.65+float2(t*1.7,-t*2.8));
                    float veins=noise(local*2.4+froth*4);
                    float cover=margin*smoothstep(.24,.60,froth)*smoothstep(.2,.7,_SurgeHeight);
                    clip(cover-.035);
                    return half4(MixFog(lerp(_Shallow.rgb,_Foam.rgb,.6+lace*.4),i.fog),cover*.95);
                }
                foam*=lerp(1,.42,i.bulk);
                color=lerp(color,_Foam.rgb,foam);
                color=MixFog(color,i.fog);color=lerp(color,_Horizon.rgb,smoothstep(240,700,distanceToEye));
                float alpha=lerp(.19,.99,deep);alpha=max(alpha,smoothstep(.15,2,i.wave)*.98);alpha=max(alpha,saturate(fresnel*.82+foam));
                float border=i.cap>.5?1:(1-smoothstep(.65,.66,abs(i.uv.x)))*(1-smoothstep(.88,1.25,abs(i.uv.y)));
                clip(border-.002);
                return half4(color,alpha*border*(1-smoothstep(.965,1,i.uv.y+.5)));
            }
            ENDHLSL
        }
    }
}
