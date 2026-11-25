// One step of the tsunami's obstacle ripple field (128 x 64, in wave space: x = across, y = along, water
// flows toward +y). r = height now, g = height last step. Obstacles (r = weight) push water up on their
// upstream face, leave a dip in their lee, and the damped wave equation lets the water around refill it.
Shader "Hidden/Candy Tropical/Surge Ripple"
{
    Properties { _MainTex("Ripple", 2D) = "black" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Obstacles); SAMPLER(sampler_Obstacles);
            float4 _MainTex_TexelSize;
            float _Damping, _Speed, _Force;
            struct A { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.position = TransformObjectToHClip(i.vertex.xyz); o.uv = i.uv; return o; }
            float H(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).r; }
            float O(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_Obstacles, sampler_Obstacles, uv, 0).r; }
            float4 frag(V i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy, uv = i.uv;
                float2 now = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rg;
                float lap = H(uv + float2(t.x, 0)) + H(uv - float2(t.x, 0)) + H(uv + float2(0, t.y)) + H(uv - float2(0, t.y)) - 4 * now.r;
                float next = (2 * now.r - now.g + _Speed * lap) * _Damping;
                float here = O(uv);
                float ahead = max(O(uv + float2(0, t.y)), O(uv + float2(0, 2 * t.y)) * .6);   // obstacle just downstream: we are its upstream face
                float behind = max(O(uv - float2(0, t.y)), O(uv - float2(0, 2 * t.y)) * .6);  // obstacle just upstream: we are in its lee
                next += _Force * (ahead - behind * .7) * (1 - here);
                next = here > .5 ? next * .3 : next;      // water inside an obstacle drains away
                return float4(clamp(next, -1.5, 2.5), now.r, 0, 0);
            }
            ENDHLSL
        }
    }
}
