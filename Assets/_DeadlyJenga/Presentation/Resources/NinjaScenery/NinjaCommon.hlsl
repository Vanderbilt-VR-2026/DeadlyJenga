#ifndef NINJA_COMMON_INCLUDED
#define NINJA_COMMON_INCLUDED
float4 _NinjaFogColor, _NinjaFogFar, _NinjaFogParams, _NinjaSkyAmb, _NinjaGroundAmb, _NinjaWarm, _NinjaWind;
float _NinjaBlood;

half3 NinjaFog(half3 color, float3 positionWS)
{
    float dist = distance(positionWS, GetCameraPositionWS());
    float f = saturate((dist - _NinjaFogParams.x) / max(1.0, _NinjaFogParams.y - _NinjaFogParams.x));
    float low = exp(-max(0.0, positionWS.y - _NinjaFogParams.w) * _NinjaFogParams.z);
    float amount = saturate(f * (0.65 + 0.35 * low) + low * 0.12 * saturate(dist / 60.0));
    half3 fogColor = lerp(_NinjaFogColor.rgb, _NinjaFogFar.rgb, saturate(dist / (_NinjaFogParams.y * 1.5)));
    return lerp(color, fogColor, amount);
}
float3 NinjaWind(float3 positionWS, float weight, float strength)
{
    float t = _Time.y * _NinjaWind.y;
    float phase = positionWS.x * 0.21 + positionWS.z * 0.17;
    float s = sin(t + phase) + 0.45 * sin(t * 2.3 + phase * 1.7) + 0.2 * sin(t * 5.1 + phase * 3.1);
    return float3(_NinjaWind.z, 0, _NinjaWind.w) * s * weight * strength * _NinjaWind.x;
}
float NinjaHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}
float NinjaNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = NinjaHash(float3(i, 1)), b = NinjaHash(float3(i + float2(1, 0), 1));
    float c = NinjaHash(float3(i + float2(0, 1), 1)), d = NinjaHash(float3(i + 1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}
#endif
