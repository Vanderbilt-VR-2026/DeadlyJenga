using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    [Serializable]
    public struct WaveFrame
    {
        public bool active;
        public Vector3 center, direction;
        public float width, depth, height, waterHeight, speed, heightRate, warningRemaining, remaining, carriedSpeedLimit;
    }
    public interface IWaveState { WaveFrame Wave { get; } }
    public interface IWorldAuthority { bool HasWorldAuthority { get; } }
    public interface IHazardMotion { Vector3 HazardVelocity { get; } }
    public interface IWaterResistance { float WaterResistance { get; } }
}
