#ifndef TWIN_CALDERA_NOISE
#define TWIN_CALDERA_NOISE
float hash21(float2 p)
{
    float3 q=frac(float3(p.xyx)*.1031);
    q+=dot(q,q.yzx+33.33);
    return frac((q.x+q.y)*q.z);
}
float noise2(float2 p)
{
    float2 i=floor(p), f=frac(p);
    f=f*f*(3-2*f);
    return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),
                lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
}
float strata(float2 p)
{
    return noise2(p)*.54 + noise2(p*2.13+13)*.27 + noise2(p*4.31-7)*.13 + noise2(p*9.1)*.06;
}
#endif
