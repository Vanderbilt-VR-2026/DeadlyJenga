#ifndef SHRINE_MOONLIT_LAKE
#define SHRINE_MOONLIT_LAKE

// The cylinder puts the tree line at the real waterline from both roof and street height.
// Its radius and the moon position are shared by the view and its water reflection.
float3 LakeSky(float3 origin, float3 ray, float4 lake, TEXTURE2D_PARAM(panorama, samp))
{
    float2 eye = origin.xz-lake.xy;
    float2 dir = ray.xz;
    float a = max(dot(dir,dir),.00001);
    float b = dot(eye,dir);
    float t = max(0,(-b+sqrt(max(0,b*b+a*(lake.z*lake.z-dot(eye,eye)))))/a);
    float3 shore = origin+t*ray;
    float2 radial = shore.xz-lake.xy;
    float angle = atan2(radial.x,radial.y);
    float u = frac(.59+angle*.159155+.19*sin(angle));
    float h = shore.y-lake.w;
    float v = saturate(.214+h/49);
    float3 painted = SAMPLE_TEXTURE2D_LOD(panorama,samp,float2(u,v),0).rgb;
    // Compress the blue chroma and leave enough tonal separation for the cedar silhouettes.
    painted = lerp(dot(painted,float3(.2126,.7152,.0722)).xxx,painted,.68)*.71;
    float3 night = float3(.009,.018,.063);
    float3 sky = lerp(painted,night,smoothstep(29,64,h));
    float3 moon = float3(lake.x-lake.z*.65,lake.w+18,lake.y+lake.z*.76);
    float chord = length(ray-normalize(moon-origin));
    float disc = 1-smoothstep(.0046,.0057,chord);
    sky += float3(.15,.09,.035)*exp(-chord*82);
    return lerp(sky,float3(1.15,.83,.46),disc);
}
#endif
