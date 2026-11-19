Shader "Candy Tropical/Body Feedback"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _SandTex ("Shared island sand", 2D) = "white" {}
        _Masks ("Ink and sweat masks", 2D) = "black" {}
        _State ("Fatigue / lost health / sprint / sand", Vector) = (0,0,0,0)
        _Ink ("Player ink", Color) = (0,1,1,1)
        _Recovery ("Healing", Float) = 0
        _Impact ("Impact", Float) = 0
        _Bearing ("Hit direction", Vector) = (0,0,0,0)
        _FeedbackTime ("Unscaled clock", Float) = 0
        _DeathKind ("Sand 1 / water 2", Float) = 0
        _Age ("Death age", Float) = 0
        _Wipe ("Return to play", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Masks); SAMPLER(sampler_Masks);
            TEXTURE2D(_SandTex); SAMPLER(sampler_SandTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _State, _Ink, _Bearing;
            half _Recovery, _Impact, _DeathKind, _Wipe;
            float _Age, _FeedbackTime;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float4 screen:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.screen=ComputeScreenPos(o.positionCS);
                return o;
            }
            // Premultiplied accumulation, converted once for the UI blend at the end.
            void Layer(inout half4 result, half3 color, half alpha)
            {
                alpha=saturate(alpha);
                result.rgb=color*alpha+result.rgb*(1-alpha);
                result.a=alpha+result.a*(1-alpha);
            }
            float Grain(float2 p)
            {
                float3 q=frac(float3(p.xyx)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            void ImpactPaint(inout half4 result, float2 p)
            {
                float2 bearing=_Bearing.xy;
                float2 freeBearing=float2(-1,.62);
                bearing=dot(bearing,bearing)>.01 ? bearing : freeBearing;
                float2 inward=-normalize(bearing);
                float2 tangent=float2(-inward.y,inward.x);
                float2 local=p-bearing*.94;
                float size=lerp(.64,1,_Impact);
                float2 splat=float2(dot(local,tangent)*1.85,dot(local,inward)*3.1)/size;
                float2 distorted=splat+float2(.075*sin(splat.y*19),.085*sin(splat.x*22));
                float shape=1-length(distorted);
                // Detached droplets extend the impact along the edge.
                shape=max(shape,.19-length((splat-float2(-1.13,.36))*float2(.8,1)));
                shape=max(shape,.13-length(splat-float2(1.27,.08)));
                shape=max(shape,.16-length((splat-float2(.59,.99))*float2(1,.75)));
                float antialias=max(fwidth(shape),.001);
                half coat=smoothstep(-antialias,antialias,shape);
                half bevel=smoothstep(0,.15,shape)*(1-smoothstep(.15,.47,shape));
                float2 slope=float2(ddx(shape),ddy(shape))/max(fwidth(shape),.001);
                float3 normal=normalize(float3(-slope*bevel*2.6,1));
                half glint=pow(saturate(dot(normal,normalize(float3(-.31,.44,1)))),26);
                half3 baseColor=lerp(_Ink.rgb,half3(1,.32,.66),.38);
                half3 paint=baseColor*(.66+.48*smoothstep(0,.45,shape));
                paint+=half3(1,.92,.85)*glint*.85;
                Layer(result,paint,coat*smoothstep(0,.42,_Impact)*.96);
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Per-eye projected UVs, independent of the oversized stereo safety plane.
                float2 uv=i.screen.xy/i.screen.w;
                float2 p=uv*2-1;
                half4 result=0;
                half edge=length(p*float2(1,.92));
                if (_DeathKind<.5)
                {
                    half fatigue=_State.x;
                    if (fatigue>.001)
                    {
                        // Tunnel vision: the dark ring moves in and gets darker as grip runs out.
                        half inner=lerp(1.04,.6,fatigue);
                        half closing=smoothstep(inner,inner+.42,edge);
                        Layer(result,half3(.012,.008,.025),closing*(.3+.66*fatigue));
                        // Heat haze: a faint warm veil that drifts over the edges, never the center.
                        half haze=fatigue*(.08+.04*sin(uv.y*21+_FeedbackTime*1.7)+.03*sin(uv.x*13-_FeedbackTime*1.1));
                        half hazeEdge=smoothstep(.5,1.1,edge);
                        Layer(result,half3(.95,.86,.74),haze*hazeEdge);
                        // Sweat runs down the screen and gathers near the edges.
                        half near=smoothstep(.45,1.05,edge);
                        half4 sweat=SAMPLE_TEXTURE2D(_Masks,sampler_Masks,float2(uv.x,frac(uv.y+_FeedbackTime*.03)));
                        Layer(result,half3(.11,.27,.31),sweat.b*near*fatigue*.32);
                        Layer(result,half3(.82,.97,1),sweat.a*near*fatigue*.9);
                    }
                    if (_State.z>.001)
                    {
                        half sprint = smoothstep(0, 1, _State.z);
                        half vignette = smoothstep(.70, 1.28, edge) * sprint * .17;
                        Layer(result, half3(.018, .022, .03), vignette);
                    }
                    if (_State.y>.001)
                    {
                        // Glossy candy-paint splatter: bright shifting colors, wet highlights, drips and sugar sparkle.
                        half4 masks=SAMPLE_TEXTURE2D(_Masks,sampler_Masks,uv);
                        half damage=saturate(_State.y*1.3);
                        half growth=saturate((damage-masks.g)*6);
                        half blob=smoothstep(.02,.06,masks.r);
                        half ink=blob*growth;
                        // Drips run down from paint above them and get longer as health drops.
                        float cols=26;
                        float column=floor(uv.x*cols);
                        float dripLen=(.04+Grain(float2(column,3))*.16)*saturate(damage*1.4);
                        half dripShape=1-smoothstep(.1,.42,abs(frac(uv.x*cols)-.5)*2);
                        half2 dripMask=SAMPLE_TEXTURE2D(_Masks,sampler_Masks,float2(uv.x,saturate(uv.y+dripLen*.55))).rg;
                        half above=smoothstep(.02,.06,dripMask.r)*saturate((damage-dripMask.g)*6);
                        half drip=above*dripShape*step(.35,Grain(float2(column,9)))*(1-ink);
                        half coat=saturate(ink+drip);
                        // Pretty candy palette around the player's own color.
                        half hueMix=frac(masks.r*3.1+Grain(floor(uv*float2(5,4)))*.55);
                        half3 pink=half3(1,.38,.74), mint=half3(.35,1,.78), sun=half3(1,.88,.35), grape=half3(.66,.45,1);
                        half3 color=lerp(_Ink.rgb,pink,smoothstep(.12,.34,hueMix));
                        color=lerp(color,sun,smoothstep(.34,.52,hueMix));
                        color=lerp(color,mint,smoothstep(.52,.72,hueMix));
                        color=lerp(color,grape,smoothstep(.72,.92,hueMix));
                        color=lerp(color,_Ink.rgb*1.15,.35);
                        // Fake a rounded wet surface from the blob mask gradient.
                        float2 slope=float2(ddx(masks.r),ddy(masks.r))*38;
                        float3 normal=normalize(float3(-slope,1));
                        float3 lightDir=normalize(float3(-.45,.7,.6));
                        float3 halfway=normalize(lightDir+float3(0,0,1));
                        half gloss=pow(saturate(dot(normal,halfway)),48)*(.55+.45*saturate(masks.r*2));
                        half rim=(1-smoothstep(.04,.14,masks.r))*blob;
                        half depth=saturate(masks.r*1.6);
                        color*=.78+.3*depth;
                        color=lerp(color,half3(1,1,1),rim*.28);
                        color+=gloss*half3(1,.97,1);
                        half sparkle=step(.985,Grain(floor(uv*float2(260,150))))*(.5+.5*sin(_FeedbackTime*5+Grain(floor(uv*160))*40));
                        color+=sparkle*ink*half3(1,1,1)*.9;
                        color=lerp(color,half3(.7,1,.85),_Recovery*.2);
                        Layer(result,color,coat*.95);
                    }
                    if (_Impact>.001) ImpactPaint(result,p);
                    Layer(result,half3(.35,1,.76),smoothstep(.88,1.25,edge)*_Recovery*.22);
                    result *= smoothstep(.28, .55, edge);
                }
                if (_State.w>.001 || (_DeathKind>.5 && _DeathKind<1.5))
                {
                    half dead=_DeathKind>.5 ? saturate(_Age/.32) : 0;
                    float progress=_State.w;
                    float level=lerp(.015+progress*.37,1.15,dead);
                    float wave=sin(uv.x*7+1)*.025+sin(uv.x*19+_FeedbackTime*.4)*.012+sin(uv.x*43)*.005;
                    float bank=max(level-uv.y+wave,(abs(p.x)-1+progress*.25-uv.y*.22)*.55);
                    half sand=smoothstep(-.009,.009,bank);
                    half ridge=1-smoothstep(.008,.055,abs(bank));
                    half3 grains=SAMPLE_TEXTURE2D(_SandTex,sampler_SandTex,uv*float2(1.4,.9)).rgb;
                    half dune=.86+.09*sin(uv.y*17+sin(uv.x*9)*2);
                    half3 color=grains*dune+half3(.15,.09,.025)*ridge;
                    color*=1-.16*exp(-abs(bank-.05)*35);
                    Layer(result,color,sand);
                }
                if (_DeathKind>1.5)
                {
                    half fall=saturate(_Age/.45);
                    half3 water=lerp(half3(.015,.48,.58),half3(.006,.045,.15),saturate(fall*.7+(1-uv.y)*.4));
                    half rays=pow(saturate(sin(uv.x*24+sin(uv.y*5+_FeedbackTime)*.6)),6)*uv.y*.07;
                    float2 grid=uv*float2(7,5)+float2(0,-_Age*.9);
                    float2 id=floor(grid);
                    float seed=Grain(id);
                    float2 cell=frac(grid)-float2(.2+seed*.6,.2+Grain(id+19)*.6);
                    float bubble=length(cell*float2(1,1.2));
                    float radius=.045+seed*.065;
                    half ring=(1-smoothstep(.006,.017,abs(bubble-radius)))*smoothstep(.45,1,edge);
                    ring*=step(.43,seed)*(.35+.65*saturate(-cell.x/radius+cell.y/radius));
                    Layer(result,water+rays,lerp(.3,.96,fall));
                    Layer(result,half3(.49,.9,1),ring*.38*(1-fall*.65));
                }
                result.rgb/=max(result.a,.0001);
                result.a*=1-saturate(_Wipe);
                return result;
            }
            ENDHLSL
        }
    }
}
