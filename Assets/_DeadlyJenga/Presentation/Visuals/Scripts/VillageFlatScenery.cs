using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.Presentation
{
    public sealed class VillageFlatScenery : MonoBehaviour
    {
        Renderer hiddenSand;
        bool previous;
        float nextSearch;
        void LateUpdate()
        {
            if(hiddenSand || !(Services.World is ISyncedWorld world) || world.Root!=transform)return;
            if(Time.unscaledTime<nextSearch)return;nextSearch=Time.unscaledTime+1; // scene-wide search, once a second
            var environment=FindFirstObjectByType<CandyEnvironment>();
            if(environment && environment.sandRenderer)
            {hiddenSand=environment.sandRenderer;previous=hiddenSand.enabled;hiddenSand.enabled=false;}
        }
        void OnDisable(){if(hiddenSand)hiddenSand.enabled=previous;hiddenSand=null;}
    }
}
