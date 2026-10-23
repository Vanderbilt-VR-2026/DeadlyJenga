using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    [Serializable] public sealed class DisasterParameter
    {
        public string id, label, unit;
        public float min, max, initial;
        public bool whole;
        public DisasterParameter(string id, string label, float min, float max, float initial, string unit="", bool whole=false)
        { this.id=id; this.label=label; this.min=min; this.max=max; this.initial=initial; this.unit=unit; this.whole=whole; }
        public float Clamp(float value) => whole ? Mathf.Round(Mathf.Clamp(value,min,max)) : Mathf.Clamp(value,min,max);
    }
    public interface IDemoHazard
    {
        IReadOnlyList<DisasterParameter> Parameters { get; }
        void Configure(float[] values);
        bool Toggle { get; }
    }
    [Serializable] public struct DisasterPoint
    { public int id; public Vector3 position, velocity; public float size, life; }
    [Serializable] public struct BlockBurnFrame
    { public int block; public float flameRemaining, smokeRemaining; }
    [Serializable] public sealed class DisasterFrame
    {
        public string kind, label;
        public Vector3 center, size;
        public float age, warning, remaining;
        public float[] values;
        public DisasterPoint[] points;
        public int[] blocks;
        public BlockBurnFrame[] burns;
        public bool residualOnly;
        [NonSerialized] public float sampledAt;
    }
    public interface IDisasterState { DisasterFrame Capture(); }
    public static class DisasterStates
    {
        public static readonly List<IDisasterState> Active = new List<IDisasterState>();
        public static float Slipperiness
        {
            get { foreach(var source in Active) { var f=source.Capture(); if(f!=null&&f.kind=="Icey"&&f.warning<=0)return f.values!=null&&f.values.Length>0?f.values[0]:1; } return 0; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>Active.Clear();
    }
}
