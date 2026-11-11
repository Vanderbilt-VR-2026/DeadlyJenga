using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.Presentation
{
    public sealed class VillageFlatScenery : MonoBehaviour
    {
        public bool openWater;
        Renderer hiddenSand;
        bool previous;
        void LateUpdate()
        {
            if(hiddenSand || !(Services.World is ISyncedWorld world) || world.Root!=transform)return;
            var environment=CandyEnvironment.ForMap(this);
            if(environment && environment.sandRenderer)
            {hiddenSand=environment.sandRenderer;previous=hiddenSand.enabled;hiddenSand.enabled=false;}
        }
        void OnDisable(){if(hiddenSand)hiddenSand.enabled=previous;hiddenSand=null;}
    }
}
