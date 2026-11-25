// Icey disaster: frost drawn over ANY block as an extra material pass. No UVs needed: the Blender frost tile
// (R = crystal coverage, G = sparkle facets, B = fine rime) is sampled in world space from three directions.
// _FrostAmount (0..1, per renderer) spreads the frost in: tops crust over first, then crystals creep up the sides.
Shader "Candy Tropical/Frost Overlay"
{
    Properties
    {
        _FrostTex("Frost tile (Blender)",2D)="white"{}
        _FrostScale("Tiles per metre",Float)=.45
        _FrostAmount("Frost amount",Range(0,1))=1
        _FrostColor("Frost",Color)=(.86,.94,1,1)
        _GlazeColor("Ice glaze",Color)=(.62,.84,1,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-60" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_FrostTex); SAMPLER(sampler_FrostTex);
            float _FrostScale,_FrostAmount; float4 _FrostColor,_GlazeColor;
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
            V vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.normal=TransformObjectToWorldNormal(i.normalOS);o.positionCS=TransformWorldToHClip(o.world);return o;}
            float3 Triplanar(float3 p,float3 n)
            {
                float3 w=pow(abs(n),4);w/=w.x+w.y+w.z+1e-4;
                float3 x=SAMPLE_TEXTURE2D(_FrostTex,sampler_FrostTex,p.zy).rgb;
                float3 y=SAMPLE_TEXTURE2D(_FrostTex,sampler_FrostTex,p.xz).rgb;
                float3 z=SAMPLE_TEXTURE2D(_FrostTex,sampler_FrostTex,p.xy).rgb;
                return x*w.x+y*w.y+z*w.z;
            }
            half4 frag(V i):SV_Target
            {
                float3 n=normalize(i.normal),view=normalize(_WorldSpaceCameraPos-i.world);
                float3 f=Triplanar(i.world*_FrostScale,n);
                float3 f2=Triplanar(i.world*_FrostScale*2.7+.37,n);
                float crystal=saturate(f.r*.75+f2.r*.4);
                float top=saturate(n.y*1.4-.2);
                // Frost grows in: tops first, then the crystal pattern climbs over the sides.
                float grow=crystal*.85+top*.6+f.b*.15;
                float threshold=1.25-_FrostAmount*1.3;
                float cover=smoothstep(threshold,threshold+.12,grow);
                Light light=GetMainLight();
                float diffuse=.55+.45*saturate(dot(n,light.direction));
                float fresnel=pow(1-saturate(dot(n,view)),3);
                float3 frost=_FrostColor.rgb*diffuse*(.85+f2.b*.25);
                // Sparkle: tiny facets that glint as the view moves.
                float glint=pow(saturate(f2.g*1.1),8)*step(.6,frac(dot(i.world,float3(3.1,1.7,2.3))+dot(view,float3(4.3,2.9,3.7))));
                float3 glaze=lerp(_GlazeColor.rgb*diffuse,float3(1,1,1),fresnel*.7);
                float3 color=lerp(glaze,frost,cover)+glint*float3(1.4,1.6,1.8)*_FrostAmount;
                float alpha=max(cover*.93,_FrostAmount*(.22+fresnel*.45));
                return half4(color*light.color,saturate(alpha+glint));
            }
            ENDHLSL
        }
    }
}
