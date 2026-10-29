using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.Presentation
{
    public sealed class VillageFlatScenery : MonoBehaviour
    {
        Renderer hiddenSand;
        bool previous;
        void LateUpdate()
        {
            if(hiddenSand || !(Services.World is ISyncedWorld world) || world.Root!=transform)return;
            var environment=FindFirstObjectByType<CandyEnvironment>();
            if(environment && environment.sandRenderer)
            {hiddenSand=environment.sandRenderer;previous=hiddenSand.enabled;hiddenSand.enabled=false;}
        }
        void OnDisable(){if(hiddenSand)hiddenSand.enabled=previous;hiddenSand=null;}
    }
}
