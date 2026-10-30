using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    public interface IWildlifeControls
    {
        IReadOnlyList<string> Species { get; }
        int Count { get; }
        bool Available { get; }
        string Status { get; }
        GameObject Spawn(string species, bool tame);
        void Clear();
    }

    public interface IWildlifeActor
    {
        string Species { get; }
        bool Tame { get; }
        Transform FollowTarget { get; }
        void SetCompanion(Transform target);
    }
}
