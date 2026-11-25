// Molten lava sea for Twin Caldera, built like the candy ocean: a static grid with slow
// swells in the vertex shader. The colour comes from a temperature value: cooled crust
// rafts drift on top, cracks and open channels glow from red up to yellow-white.
Shader "DeadlyJenga/Caldera/Lava"
{
    Properties
    {
        _PlateSize("Crust raft size (m)",Float)=5.5
        _Drift("Drift XZ (m/s)",Vector)=(.16,-.07,0,0)
        _Warp("Current bend (m)",Float)=16
        _SwellHeight("Swell height (m)",Range(0,.4))=.13
        _SwellSpeed("Swell speed",Range(0,1))=.35
        _SwellExtent("Detailed swell extent (m)",Float)=110
        _Glow("Glow strength",Range(0,3))=1
        _ShieldFoot("Left cone footprint (center XZ, half size)",Vector)=(-23,1.5,19,27)
        _SpireFoot("Right cone footprint (center XZ, half size)",Vector)=(22.6,4.6,17,24)
        _ShoreHeat("Heat where rock meets lava",Range(0,1))=.62
        _ShoreFalloff("Shore glow falloff (m)",Float)=7
        _Horizon("Distant haze",Color)=(.58,.13,.12,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "Lava"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CalderaNoise.cginc"
            CBUFFER_START(UnityPerMaterial)
            float4 _Drift,_ShieldFoot,_SpireFoot,_Horizon;
            float _PlateSize,_Warp,_SwellHeight,_SwellSpeed,_SwellExtent,_Glow,_ShoreHeat,_ShoreFalloff;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};

            // Thick lava rolls slowly: long, low swells with no chop.
            void Swell(float2 direction,float wavelength,float amplitude,float2 p,float time,inout float height,inout float2 slope)
            {
                float k=6.2831853/wavelength,phase=k*dot(direction,p)-time*sqrt(9.81*k)*.18;
                float s,c;sincos(phase,s,c);
                height+=amplitude*s;slope+=direction*amplitude*k*c;
            }
            Varyings vert(Attributes i)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(i.positionOS.xyz);
                float fade=1-smoothstep(_SwellExtent*.7,_SwellExtent,max(abs(world.x),abs(world.z)));
                float a=_SwellHeight*fade,t=_Time.y*_SwellSpeed,height=0;float2 slope=0;
                Swell(float2(.83,.55),23,a*.55,world.xz,t,height,slope);
                Swell(float2(-.42,.91),14,a*.30,world.xz,t*.9,height,slope);
                Swell(float2(.17,-.98),37,a*.35,world.xz,t*.7,height,slope);
                world.y+=height;
                o.world=world;o.normal=normalize(float3(-slope.x,1,-slope.y));
                o.positionCS=TransformWorldToHClip(world);o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float RoundBox(float2 p,float4 box,float radius)
            {
                float2 d=abs(p-box.xy)-(box.zw-radius);
                return length(max(d,0))+min(max(d.x,d.y),0)-radius;
            }
            // Black crust, dark red, orange, yellow, then near-white at the hottest.
            float3 HeatColor(float t)
            {
                t=saturate(t);
                float3 c=lerp(float3(.018,.008,.010),float3(.32,.018,.004),smoothstep(.10,.36,t));
                c=lerp(c,float3(1.0,.19,.012),smoothstep(.34,.62,t));
                c=lerp(c,float3(1.45,.60,.05),smoothstep(.60,.84,t));
                c=lerp(c,float3(1.75,1.30,.48),smoothstep(.86,1.0,t));
                return c*_Glow;
            }

            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 w=i.world.xz;float time=_Time.y;
                float eye=distance(i.world,_WorldSpaceCameraPos);

                // Slowly bending currents carry everything along.
                float2 bend=float2(noise2(w*.021+float2(0,time*.011)),noise2(w*.021+float2(31,-time*.013)))-.5;
                float2 q=w+bend*_Warp-_Drift.xy*time;

                // Hot rivers wind through the sea; more heat near the cones and in the channel.
                float river=1-abs(noise2(q*.017)*.68+noise2(q*.041+7)*.32-.5)*2;
                float heat=smoothstep(.58,.96,river);
                float shore=min(RoundBox(w,_ShieldFoot,9),RoundBox(w,_SpireFoot,9));
                heat=saturate(heat+_ShoreHeat*exp(-max(0,shore)/max(.5,_ShoreFalloff)));
                heat=saturate(heat+.07*sin(time*.6+noise2(q*.09)*6.3));

                // Crust rafts: one cell per raft. Hot areas break the rafts apart.
                float2 cp=q/_PlateSize,cell=floor(cp),f=frac(cp);
                float nearest=8,second=8,raft=0;
                [unroll] for(int x=-1;x<=1;x++) [unroll] for(int y=-1;y<=1;y++)
                {
                    float2 g=float2(x,y),id=cell+g;
                    float2 delta=g+.15+.7*float2(hash21(id),hash21(id+57))-f;
                    float d=dot(delta,delta);
                    if(d<nearest){second=nearest;nearest=d;raft=hash21(id+113);}
                    else second=min(second,d);
                }
                float edge=sqrt(second)-sqrt(nearest);
                float aa=fwidth(edge)*1.5+.01;
                float crackWidth=lerp(.035,.30,heat);
                float crack=1-smoothstep(crackWidth,crackWidth+aa,edge);
                float sunk=1-smoothstep(-.04,.04,raft-(heat*.8-.1));
                float molten=saturate(max(crack,sunk));

                // Temperature: crust is cool in its middle and warm near its cracks.
                float grain=noise2(q*.85);
                float churn=noise2(q*2.1+float2(time*.35,-time*.27));
                float crustT=.04+.28*(1-smoothstep(0,.32,edge))+.05*grain;
                float moltenT=.52+.32*heat+.16*churn;

                // Bubbles rise in the open melt, swell and pop.
                float2 b=w*.42+_Drift.xy*time*.4;float2 bid=floor(b),bf=frac(b)-.5;
                float bh=hash21(bid),life=frac(time*.22+bh);
                float radius=life*.38,dist=length(bf+.15*(float2(hash21(bid+9),hash21(bid+3))-.5));
                float bubble=step(bh,.4)*(smoothstep(radius-.08,radius,dist)-smoothstep(radius,radius+.03,dist))*(1-life);
                moltenT+=bubble*.35;

                // Hide fine detail far away so it does not shimmer.
                float far=smoothstep(55,240,eye);
                float detail=1-far;
                molten=lerp(saturate(heat*.55+.12),molten,detail);
                float temperature=lerp(crustT,moltenT,molten);
                float3 color=HeatColor(temperature);

                // Cooled crust is dark glassy rock lit by the dusk sky and the melt around it.
                float3 n=normalize(i.normal+float3(grain-.5,0,churn-.5)*.25*detail);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                Light light=GetMainLight();
                float diffuse=saturate(dot(n,light.direction))*.45+.25;
                float3 rock=float3(.045,.026,.036)*(.65+.7*grain)*(diffuse*light.color+float3(.12,.05,.08));
                float sheen=pow(saturate(dot(n,normalize(view+light.direction))),48)*.18;
                float fresnel=pow(1-saturate(dot(n,view)),4)*.12;
                rock+=light.color*sheen+float3(.42,.10,.16)*fresnel;
                color=lerp(rock+color*.65,color,molten);

                float haze=smoothstep(140,720,eye);
                color=lerp(color,_Horizon.rgb,haze*.82);
                color=MixFog(color,i.fog);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
