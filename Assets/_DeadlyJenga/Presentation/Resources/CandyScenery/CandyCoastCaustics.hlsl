#ifndef CANDY_COAST_CAUSTICS_INCLUDED
#define CANDY_COAST_CAUSTICS_INCLUDED
TEXTURE2D(_CoastCaustics); SAMPLER(sampler_CoastCaustics);
float CandyRefractedLight(float2 position, float depth)
{
    float2 p=(position+float2(.48,-.32)*depth)*.185;
    float t=_Time.y;
    float3 rays=SAMPLE_TEXTURE2D(_CoastCaustics,sampler_CoastCaustics,p+float2(.013,-.017)*t).rgb;
    float2 cross=float2(.81*p.x-.59*p.y,.59*p.x+.81*p.y);
    float secondary=SAMPLE_TEXTURE2D(_CoastCaustics,sampler_CoastCaustics,cross*1.19+float2(-.018,.006)*t).g;
    float main=lerp(rays.r,rays.b,smoothstep(.18,2.8,depth));
    float focus=max(main,secondary*.57);
    return focus*focus*.86+focus*.26;
}
#endif
