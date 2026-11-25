Shader "DeadlyJenga/Caldera/VolcanicSkyline"
{
    // The eroded background volcanoes from Blender. Vertex colour: R = lava river glow,
    // G = pale ash dust. Terrain beyond 400 m is pulled toward the camera so it stays
    // inside the far clip while keeping the same size on screen.
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CalderaNoise.cginc"
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Data { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 paint:TEXCOORD2; float fog:TEXCOORD3; float dist:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };

            float3 PullIn(float3 world)
            {
                float3 v=world-_WorldSpaceCameraPos; float d=length(v);
                float pulled=d>400?400+(d-400)*.45:d;
                return _WorldSpaceCameraPos+v*(pulled/max(d,.001));
            }

            Data vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v); Data o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.vertex.xyz);
                o.dist=length(o.world-_WorldSpaceCameraPos);
                o.pos=TransformWorldToHClip(PullIn(o.world));
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.paint=v.color.rg; o.fog=ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(Data i):SV_Target
            {
                float3 n=normalize(i.normal);
                float2 p=i.world.xz*.035+i.world.y*.05;
                float grain=strata(p*3.1), crags=strata(float2(i.world.x+i.world.z,i.world.y*2.2)*.02);
                float sun=saturate(dot(n,normalize(float3(-.48,.62,-.62))));
                float shade=sun*sun*.9+.06+.1*saturate(n.y);
                // Black crags with plum-brown faces, pale ash on the gentle upper slopes.
                float3 rock=lerp(float3(.006,.005,.006),float3(.15,.095,.085),shade*(.35+crags*.9));
                rock*=.45+grain*.9;
                float3 ash=float3(.24,.21,.19)*(.35+.75*shade)*(.7+grain*.45);
                rock=lerp(rock,ash,saturate(i.paint.g*.9));
                // Lava rivers: dim red edges, orange body, yellow core, with a slow pulse.
                float flow=strata(float2(i.world.x*.03,i.world.y*.09-_Time.y*.18));
                float lava=saturate(i.paint.r*(.75+.5*flow));
                float3 heat=lava<.5?lerp(float3(.35,.02,0),float3(1.9,.38,.03),lava*2):lerp(float3(1.9,.38,.03),float3(3.2,1.7,.45),lava*2-1);
                rock=lerp(rock,heat,smoothstep(.06,.3,lava));
                // Underlit by the lava sea, then hazed by distance into the ash sky.
                rock+=float3(.10,.018,.004)*saturate(-n.y*.6+.4)*exp(-max(0,i.world.y)*.012);
                // Light haze only: the scene fog would bleach the silhouettes, so use half of it.
                float haze=1-exp(-i.dist*.0011);
                rock=lerp(rock,float3(.30,.08,.07),haze*.38);
                return half4(lerp(rock,MixFog(rock,i.fog),.45),1);
            }
            ENDHLSL
        }
    }
}
