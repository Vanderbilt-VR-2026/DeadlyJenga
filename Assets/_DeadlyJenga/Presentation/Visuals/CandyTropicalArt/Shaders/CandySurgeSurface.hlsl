#ifndef CANDY_SURGE_SURFACE_INCLUDED
#define CANDY_SURGE_SURFACE_INCLUDED
float2 CandySurgeCoordinates(float2 world)
{
    float2 delta=world-_SurgeOrigin.xz;
    return float2(dot(delta,float2(_SurgeDirection.z,-_SurgeDirection.x)),dot(delta,_SurgeDirection.xz));
}
float CandySurgeShape(float t)
{
    t=saturate(t);
    return t<.72?smoothstep(0,.72,t):1-smoothstep(.72,1,t);
}
float2 CandySurgeCurve(float t)
{
    t=saturate(t);
    if(t<.42){float a=(t/.42)*1.57079633;return float2(.34*sin(a),.43*(1-cos(a)));}
    // Open barrel: broad concave rise, overhead arch, then a lip thrown forward.
    float angle=lerp(3.14159265,.68,(t-.42)/.58);
    return float2(.67+.33*cos(angle),.43+.57*sin(angle));
}
float CandySurgeCrestScale(float across)
{
    return 1+.18*sin(across*.063)+.055*sin(across*.17+1);
}
float CandySurgeElevation(float2 world)
{
    float2 p=CandySurgeCoordinates(world);
    float t=p.y/max(.01,_SurgeDepth)+.5;
    float across=abs(p.x)/max(.01,_SurgeWidth*.5);
    float shape=t<.72?smoothstep(0,.72,t):1-smoothstep(.72,1,t);
    return _SurgeHeight*shape*(1-smoothstep(.85,1,across));
}
// Decorative storm sea shares displacement across the ocean and breaking wave.
float CandySurgeDisturbance(float2 p)
{
    float across=1-smoothstep(_SurgeWidth*.42,_SurgeWidth*.5+90,abs(p.x));
    float along=1-smoothstep(_SurgeDepth*.55,_SurgeDepth*1.6+110,abs(p.y));
    return saturate(_SurgeHeight*.12)*across*along;
}
float3 CandySurgeWakeSample(float2 world)
{
    float2 p=CandySurgeCoordinates(world);
    float detail=1-smoothstep(_WaveExtent*.68,_WaveExtent,max(abs(world.x-_Island.x),abs(world.y-_Island.y)));
    float strength=min(4.5,_SurgeHeight*.24)*CandySurgeDisturbance(p)*detail;
    float sa,ca,sb,cb,sc,cc,sd,cd;
    sincos(p.y*.105+p.x*p.x*.00018-_Time.y*.8,sa,ca);
    sincos(p.y*.19-p.x*.028-_Time.y*1.25,sb,cb);
    sincos(p.y*.48+p.x*.065-_Time.y*2.1,sc,cc);
    sincos(p.y*.85-p.x*.09-_Time.y*2.8,sd,cd);
    float dx=strength*(ca*.53*p.x*.00036-cb*.3*.028+cc*.14*.065-cd*.065*.09);
    float dz=strength*(ca*.53*.105+cb*.3*.19+cc*.14*.48+cd*.065*.85);
    float2 gradient=float2(_SurgeDirection.z,-_SurgeDirection.x)*dx+_SurgeDirection.xz*dz;
    return float3(strength*(.23+sa*.53+sb*.3+sc*.14+sd*.065),-gradient.x,-gradient.y);
}
// Raised shoulders belong to the surrounding ocean, not the rolled crest mesh.
float3 CandySurgeOuterSample(float2 world)
{
    float2 p=CandySurgeCoordinates(world);
    float2 axes=max(float2(12,12),float2(_SurgeWidth*.65,_SurgeDepth*1.6));
    float2 q=(p+float2(0,_SurgeDepth*.35))/axes;
    float radius=max(.001,length(q));
    float wobble=p.x*.055+p.y*.025-_Time.y*.6;
    float r=radius+.09*sin(wobble);
    float2 dr=q/(radius*axes)+.09*cos(wobble)*float2(.055,.025);
    float a=saturate(1-abs(r-1.12)/.58),b=saturate(1-abs(r-1.95)/.5);
    float lift=a*a*(3-2*a)+.35*b*b*(3-2*b);
    float slope=-6*a*(1-a)*sign(r-1.12)/.58-.35*6*b*(1-b)*sign(r-1.95)/.5;
    float phase=p.x*.09-_Time.y*.8;
    float modulation=.8+.2*sin(phase);
    float detail=1-smoothstep(_WaveExtent*.68,_WaveExtent,max(abs(world.x-_Island.x),abs(world.y-_Island.y)));
    float strength=min(7,_SurgeHeight*.36)*saturate(_SurgeHeight*.12)*detail;
    float2 grad=strength*(dr*slope*modulation+float2(.018*cos(phase)*lift,0));
    float2 gradient=float2(_SurgeDirection.z,-_SurgeDirection.x)*grad.x+_SurgeDirection.xz*grad.y;
    return float3(strength*lift*modulation,-gradient.x,-gradient.y);
}
float CandySurgeFoamNoise(float2 p)
{
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    float4 h=frac(sin(float4(dot(cell,float2(127.1,311.7)),dot(cell+float2(1,0),float2(127.1,311.7)),dot(cell+float2(0,1),float2(127.1,311.7)),dot(cell+1,float2(127.1,311.7))))*43758.5453);
    return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
}
float CandySurgeWhitecaps(float2 world)
{
    if(_SurgeHeight<.001)return 0;
    float2 p=CandySurgeCoordinates(world);
    float2 flow=p*float2(.32,.48)+float2(_Time.y*.12,-_Time.y*.65);
    float n=CandySurgeFoamNoise(flow)*.65+CandySurgeFoamNoise(flow*2.7)*.35;
    float swell=sin(p.y*.19-p.x*.028-_Time.y*1.25);
    float chop=sin(p.y*.48+p.x*.065-_Time.y*2.1);
    float crest=max(smoothstep(.58,.95,swell),smoothstep(.72,.98,chop)*.7);
    float broken=smoothstep(.49,.73,n);
    float flat=1-smoothstep(.3,1.5,CandySurgeElevation(world));
    return CandySurgeDisturbance(p)*crest*broken*.78*flat;
}
#endif
