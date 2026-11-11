#ifndef CANDY_COAST_PANORAMA_INCLUDED
#define CANDY_COAST_PANORAMA_INCLUDED
float2 CandyCoastSkyUV(float3 direction)
{
    float heading=atan2(direction.z,direction.x)*(1.0/(2.0*PI))+.66;
    float latitude=asin(clamp(direction.y,-1,1))*(1.0/PI);
    return float2(heading,.486+latitude*.90);
}
half3 CandyCoastClouds(float3 direction)
{
    float2 uv=CandyCoastSkyUV(direction);
    // Blend around the wrap where the painted cloud shapes meet.
    float seam=1-smoothstep(0,.035,min(frac(uv.x),1-frac(uv.x)));
    float2 dx=ddx(uv), dy=ddy(uv);
    dx.x-=round(dx.x);
    dy.x-=round(dy.x);
    half3 painted=SAMPLE_TEXTURE2D_GRAD(_CoastPanorama,sampler_CoastPanorama,uv,dx,dy).rgb;
    float blend=seam*smoothstep(.02,.30,direction.y);
    UNITY_BRANCH if(blend>.001)
    {
        half3 opposite=SAMPLE_TEXTURE2D_GRAD(_CoastPanorama,sampler_CoastPanorama,float2(.5,uv.y),dx,dy).rgb;
        painted=lerp(painted,opposite,blend);
    }
    return painted;
}
#endif
