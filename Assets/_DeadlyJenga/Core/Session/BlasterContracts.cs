using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    public interface IBlasterControls
    {
        bool TestingEnabled { get; }
        bool Drawn { get; }
        bool LeftHanded { get; }
        void SetTestingEnabled(bool enabled);
        void SetDrawn(bool drawn);
        void SetLeftHanded(bool left);
    }
    public interface IBlasterRuntime
    {
        bool BlasterTestingEnabled { get; }
        void SetBlasterTesting(bool enabled);
        bool RequestBlasterShot(GameObject source,Pose muzzle);
    }
    [Serializable] public sealed class PlasmaShotFrame
    {
        public int id;
        public Vector3 position,velocity;
    }
    [Serializable] public sealed class PlasmaImpactFrame
    {
        public int id;
        public Vector3 position;
        public float remaining;
    }
    [Serializable] public sealed class BlasterFrame
    {
        public bool enabled;
        public bool testingEnabled;
        public PlasmaShotFrame[] shots=Array.Empty<PlasmaShotFrame>();
        public PlasmaImpactFrame[] impacts=Array.Empty<PlasmaImpactFrame>();
    }
}
