#ifndef CANDY_COAST_PANORAMA_INCLUDED
#define CANDY_COAST_PANORAMA_INCLUDED
float3 CandyCoastMarineAir(float3 direction,float3 sun)
{
    float sunward=pow(saturate(dot(normalize(direction.xz+float2(.0001,0)),normalize(sun.xz+float2(.0001,0)))),5);
    return lerp(float3(.169,.432,.639),float3(.286,.488,.624),sunward*.59);
}
float CandyCoastViewDepth(float3 direction)
{
    return max(0,-dot(direction,UNITY_MATRIX_V[2].xyz));
}
#ifndef CANDY_COAST_MARINE_ONLY
float2 CandyCoastSkyUV(float3 direction)
{
    float heading=atan2(direction.z,direction.x)*(1.0/(2.0*PI))+.66;
    float latitude=asin(clamp(direction.y,-1,1))*(1.0/PI);
    return float2(heading,.486+latitude*.90);
}
half3 CandyCoastClouds(float3 direction)
{
    float2 uv=CandyCoastSkyUV(direction);
    // Use a continuous interior band where the source panorama edges meet.
    float signedSeam=frac(uv.x+.5)-.5;
    float blend=1-smoothstep(.025,.106,abs(signedSeam));
    float2 dx=ddx(uv), dy=ddy(uv);
    dx.x-=round(dx.x);
    dy.x-=round(dy.x);
    half3 painted=SAMPLE_TEXTURE2D_GRAD(_CoastPanorama,sampler_CoastPanorama,uv,dx,dy).rgb;
    UNITY_BRANCH if(blend>.001)
    {
        half3 opposite=SAMPLE_TEXTURE2D_GRAD(_CoastPanorama,sampler_CoastPanorama,float2(.5+signedSeam,uv.y),dx,dy).rgb;
        painted=lerp(painted,opposite,blend);
    }
    float horizon=1-smoothstep(.012,.155,abs(direction.y));
    return lerp(painted,half3(.19,.54,.84),horizon*.16);
}
#endif
#endif
