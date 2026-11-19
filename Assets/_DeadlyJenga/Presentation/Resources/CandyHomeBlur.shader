Shader "Hidden/DeadlyJenga/Home Blur"
{
    Properties { _MainTex("Source",2D)="white"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float2 _BlurDirection;
            half4 frag(v2f_img i):SV_Target
            {
                half4 color=tex2D(_MainTex,i.uv)*.227027;
                color+=(tex2D(_MainTex,i.uv+_BlurDirection*1.384615)+tex2D(_MainTex,i.uv-_BlurDirection*1.384615))*.316216;
                color+=(tex2D(_MainTex,i.uv+_BlurDirection*3.230769)+tex2D(_MainTex,i.uv-_BlurDirection*3.230769))*.070270;
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
