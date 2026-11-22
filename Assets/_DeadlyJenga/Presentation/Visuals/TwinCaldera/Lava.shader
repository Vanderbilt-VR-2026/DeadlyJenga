Shader "DeadlyJenga/Caldera/Lava"
{
 SubShader { Tags { "RenderType"="Opaque" } Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "CalderaNoise.cginc"
 struct Data { float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO };
 Data vert(appdata_base v){UNITY_SETUP_INSTANCE_ID(v);Data o;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 half4 frag(Data i):SV_Target
 {
  float2 p=i.world.xz*.21;
  float wobble=strata(p*1.3+_Time.y*.026);
  p+=float2(wobble,strata(p.yx*.8))*1.8;
  float2 cell=floor(p), uv=frac(p);
  float nearest=8,second=8;
  [unroll] for(int x=-1;x<=1;x++) [unroll] for(int y=-1;y<=1;y++)
  {
   float2 q=float2(x,y);float2 delta=q+.18+.64*float2(hash21(cell+q),hash21(cell+q+71))-uv;
   float d=dot(delta,delta);second=min(second,max(nearest,d));nearest=min(nearest,d);
  }
  float gap=second-nearest;
  float fire=exp(-gap*14);
  float fine=pow(saturate(1-abs(noise2(p*4+wobble)-.5)*15),4);
  float3 c=lerp(float3(.12,.012,.031),float3(.82,.085,.014),strata(p*3));
  c+=float3(2.0,.47,.018)*fire+float3(1,.74,.16)*fire*fire+fine*float3(.52,.08,.008);
  float mist=smoothstep(60,350,distance(i.world,_WorldSpaceCameraPos));
  return half4(lerp(c,float3(.57,.116,.14),mist*.89),1);
 }
 ENDCG
 } }
}
