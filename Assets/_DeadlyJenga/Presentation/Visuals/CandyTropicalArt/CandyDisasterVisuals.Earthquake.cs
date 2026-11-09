using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyDisasterVisuals
    {
        const int QuakeGrid=17,QuakeProbesPerFrame=20,QuakeRippleLimit=48;
        const float QuakeSweep=3.2f,QuakeAftershock=3.55f,QuakeCycle=9f;
        static readonly Vector3 QuakeDirection=new Vector3(.82f,0,.57f).normalized;
        struct QuakeSample
        {
            public Vector3 point;
            public bool land;
        }
        readonly QuakeSample[] quakeSamples=new QuakeSample[QuakeGrid*QuakeGrid];
        readonly List<Matrix4x4> quakeDust=new List<Matrix4x4>(128),quakeSpray=new List<Matrix4x4>(48),
            quakeRipples=new List<Matrix4x4>(QuakeRippleLimit);
        readonly float[] quakeRippleOpacity=new float[QuakeRippleLimit];
        IWorldQuery quakeWorld;
        Bounds quakeBounds;
        CandySandbar quakeSandbar;
        int quakeProbes;
        bool quakeSeen;
        float quakeCellScale=1;
        Material quakeDustMaterial,quakeSprayMaterial,quakeRippleMaterial;
        MaterialPropertyBlock quakeRippleProperties;

        void InitializeEarthquake()
        {
            quakeDustMaterial=ObjectMaterial("CandyDisasterSmoke",new Color(.63f,.46f,.30f,.59f));
            quakeSprayMaterial=ObjectMaterial("CandyStormMist",new Color(.73f,.94f,.88f,.78f));
            var shader=Resources.Load<Shader>("CandyScenery/CandyQuakeRipple");
            if(shader)
            {
                quakeRippleMaterial=new Material(shader){name="Earthquake offshore ripples",enableInstancing=true};
                quakeRippleProperties=new MaterialPropertyBlock();
            }
        }

        void BeginEarthquake()
        {
            quakeSeen=false;
            quakeDust.Clear();
            quakeSpray.Clear();
            quakeRipples.Clear();
        }

        void ClearEarthquake()
        {
            quakeWorld=null;
            quakeSandbar=null;
            quakeProbes=0;
            BeginEarthquake();
        }

        void DestroyEarthquake()
        {
            if(quakeDustMaterial)Destroy(quakeDustMaterial);
            if(quakeSprayMaterial)Destroy(quakeSprayMaterial);
            if(quakeRippleMaterial)Destroy(quakeRippleMaterial);
        }

        // Build a small surface map during the warning where possible. No probes are needed while drawing.
        void SampleEarthquake(DisasterFrame frame)
        {
            var world=Services.World;
            if(world==null)return;
            quakeSeen=true;
            var bounds=new Bounds(frame.center,frame.size);
            if(!ReferenceEquals(world,quakeWorld)||bounds!=quakeBounds)
            {
                quakeWorld=world;
                quakeBounds=bounds;
                quakeProbes=0;
                quakeSandbar=environment&&environment.sandRenderer&&environment.sandRenderer.enabled?
                    environment.sandRenderer.GetComponent<CandySandbar>():null;
            }
            float pad=Mathf.Clamp(Mathf.Min(bounds.size.x,bounds.size.z)*.12f,4,18);
            var half=bounds.extents+new Vector3(pad,0,pad);
            if(quakeSandbar)
            {
                var sandHalf=quakeSandbar.HalfSize;
                var sandCenter=quakeSandbar.transform.position-bounds.center;
                half.x=Mathf.Max(half.x,sandHalf.x+Mathf.Abs(sandCenter.x)+2);
                half.z=Mathf.Max(half.z,sandHalf.y+Mathf.Abs(sandCenter.z)+2);
            }
            float stepX=half.x*2/(QuakeGrid-1),stepZ=half.z*2/(QuakeGrid-1);
            quakeCellScale=Mathf.Clamp(Mathf.Sqrt(stepX*stepZ)/3,.65f,1.8f);
            int end=Mathf.Min(quakeSamples.Length,quakeProbes+QuakeProbesPerFrame);
            while(quakeProbes<end)
            {
                int index=quakeProbes++;
                int x=index%QuakeGrid,z=index/QuakeGrid;
                float px=bounds.center.x-half.x+(x+(Hash(index+17)-.5f)*.34f)*stepX;
                float pz=bounds.center.z-half.z+(z+(Hash(index+421)-.5f)*.34f)*stepZ;
                var probe=new Vector3(px,bounds.max.y+5,pz);
                float water=world.WaterHeight(probe);
                var sample=new QuakeSample{point=new Vector3(px,water,pz)};
                float rayLength=Mathf.Max(0,probe.y-water);
                if(rayLength>0&&Physics.Raycast(probe,Vector3.down,out var hit,rayLength,1<<Layers.Surface,QueryTriggerInteraction.Ignore))
                {
                    sample.land=true;
                    sample.point=hit.point;
                }
                else if(quakeSandbar)
                {
                    var local=quakeSandbar.transform.InverseTransformPoint(probe);
                    var horizontal=new Vector2(local.x,local.z);
                    if(quakeSandbar.ShapeDistance(horizontal)<=0)
                    {
                        local.y=quakeSandbar.SampleHeight(horizontal);
                        var point=quakeSandbar.transform.TransformPoint(local);
                        if(point.y>=water-.04f){sample.land=true;sample.point=point;}
                    }
                }
                if(!sample.land&&rayLength>0&&Physics.Raycast(probe,Vector3.down,out hit,rayLength,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore))
                {
                    sample.land=true;
                    sample.point=hit.point;
                }
                quakeSamples[index]=sample;
            }
        }

        internal static float QuakeFrontArrival(Vector3 point,Bounds bounds)
        {
            float span=Mathf.Max(.01f,Mathf.Abs(QuakeDirection.x)*bounds.extents.x+Mathf.Abs(QuakeDirection.z)*bounds.extents.z);
            float across=Vector3.Dot(point-bounds.center,QuakeDirection);
            return .2f+Mathf.Clamp01((across/span+1)*.5f)*QuakeSweep;
        }

        internal static float QuakeActiveAge(DisasterFrame frame,float now)
        {
            float warning=frame.values!=null&&frame.values.Length>3?frame.values[3]:0;
            return Mathf.Max(0,frame.age+Mathf.Clamp(now-frame.sampledAt,0,.24f)-warning);
        }

        void Earthquake(DisasterFrame frame)
        {
            if(quakeWorld==null||quakeProbes==0)return;
            float age=QuakeActiveAge(frame,Time.time);
            float cycleAge=Mathf.Repeat(age,QuakeCycle);
            float strength=age<QuakeCycle?1:.66f;
            strength*=Mathf.Clamp01(frame.remaining/1.25f);
            if(strength<=0)return;
            for(int i=0;i<quakeProbes;i++)
            {
                var sample=quakeSamples[i];
                float since=cycleAge-QuakeFrontArrival(sample.point,quakeBounds)-(Hash(i+389)-.5f)*.20f;
                DrawQuakeSample(i,sample,since,strength);
                DrawQuakeSample(i,sample,since-QuakeAftershock,strength*.52f);
            }
        }

        void DrawQuakeSample(int index,QuakeSample sample,float age,float strength)
        {
            if(age<=0||age>=2.25f)return;
            if(sample.land)
            {
                float lifetime=1.1f+.65f*Hash(index+148);
                if(age>=lifetime||quakeDust.Count>=120)return;
                float life=age/lifetime;
                float envelope=Mathf.Sin(Mathf.PI*life);
                float width=(.8f+1.15f*Hash(index+36)+life*1.2f)*strength*envelope*quakeCellScale;
                Vector3 drift=QuakeDirection*(age*.7f)+Vector3.up*(.16f+life*.52f);
                Puff(quakeDust,sample.point+drift,width,Mathf.Min(1.35f,width*.56f),index*31);
                if(index%5==0&&age<.65f&&candyChips.Count<384)
                {
                    float height=age*(1.6f+Hash(index+99))-3.9f*age*age;
                    if(height>0)
                    {
                        var chip=sample.point+QuakeDirection*age*.85f+Vector3.up*height;
                        candyChips.Add(Matrix4x4.TRS(chip,Quaternion.Euler(age*520,index*51,age*280),
                            Vector3.one*(.09f+.10f*Hash(index))*strength));
                    }
                }
                return;
            }

            Vector3 seaward=ShoreDirection(index,sample.point);
            bool shoreline=seaward.sqrMagnitude>.01f;
            // On maps with no nearby beach, carry the shock onto open water in sparse patches.
            if(!shoreline)
            {
                if(index%11!=0)return;
                seaward=QuakeDirection;
            }
            else if(index%2!=0)return;
            var at=sample.point+seaward*.24f;
            at.y=quakeWorld.WaterHeight(at);
            if(!float.IsFinite(at.y))return;
            if(shoreline&&age<.85f&&quakeSpray.Count<44)
            {
                float t=age/.85f;
                float height=4*t*(1-t);
                float size=(.68f+Hash(index+220)*.77f)*strength*(1-t);
                Puff(quakeSpray,at+seaward*age*1.75f+Vector3.up*(.1f+height*1.4f*strength),
                    size,size*(1.2f+height),-16+Hash(index+53)*32);
            }
            if(quakeRipples.Count>=QuakeRippleLimit)return;
            float distance=(.8f+age*3.2f)*(.78f+Hash(index+14)*.38f);
            quakeRippleOpacity[quakeRipples.Count]=(1-age/2.25f)*strength*(shoreline?1:.37f);
            quakeRipples.Add(Matrix4x4.TRS(at+Vector3.up*.085f,Quaternion.LookRotation(Vector3.up,seaward),
                new Vector3(distance*1.28f,distance,1)));
        }

        Vector3 ShoreDirection(int index,Vector3 point)
        {
            int x=index%QuakeGrid,z=index/QuakeGrid;
            Vector3 sum=Vector3.zero;
            Add(x-1,z);Add(x+1,z);Add(x,z-1);Add(x,z+1);
            return sum.sqrMagnitude>.01f?sum.normalized:Vector3.zero;

            void Add(int nx,int nz)
            {
                if(nx<0||nx>=QuakeGrid||nz<0||nz>=QuakeGrid)return;
                int next=nz*QuakeGrid+nx;
                if(next>=quakeProbes||!quakeSamples[next].land)return;
                var away=point-quakeSamples[next].point;
                away.y=0;
                sum+=away.normalized;
            }
        }

        void RenderEarthquake()
        {
            if(!quakeSeen){quakeProbes=0;return;}
            DrawObjects(quakeDust,quad,quakeDustMaterial);
            DrawObjects(quakeSpray,quad,quakeSprayMaterial);
            if(!quakeRippleMaterial||quakeRipples.Count==0)return;
            quakeRipples.CopyTo(matrices);
            quakeRippleProperties.SetFloatArray("_RippleOpacity",quakeRippleOpacity);
            Graphics.DrawMeshInstanced(quad,0,quakeRippleMaterial,matrices,quakeRipples.Count,
                quakeRippleProperties,ShadowCastingMode.Off,false);
        }
    }
}
