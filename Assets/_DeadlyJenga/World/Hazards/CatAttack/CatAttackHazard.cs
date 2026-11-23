using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    public sealed class CatAttackHazard : TreeCreatureHazard
    {
        protected override string Kind => "CatAttack";
        [SerializeField, Range(1,3)] int passes = 1;
        [SerializeField, Range(0,360)] float heading = 139;
        [SerializeField, Range(.2f,3)] float crouchSeconds = 1;
        [SerializeField, Range(25,85)] float runSpeed = 44;
        [SerializeField, Range(0,22)] float pounceHeight = 12;
        Vector3 start, finish, headingVector;
        float windup, travel;
        int lastPass;

        protected override void Setup()
        {
            headingVector = Quaternion.Euler(0,heading,0) * Vector3.forward;
            float reach = Mathf.Abs(headingVector.x)*area.extents.x + Mathf.Abs(headingVector.z)*area.extents.z + 45;
            start = area.center - headingVector*reach;
            finish = area.center + headingVector*reach;
            start.y = Ground(start);
            finish.y = Ground(finish);
            float budget = (V(3)-.1f)/Mathf.Clamp(passes,1,3);
            windup = Mathf.Min(crouchSeconds,budget*.32f);
            travel = Mathf.Min(2*reach/Mathf.Max(1,runSpeed),budget-windup);
            lastPass = -1;
            points = new[]{new DisasterPoint{id=1, position=start, velocity=headingVector, life=-1}};
        }

        protected override void Step(float dt)
        {
            float cycle = travel+windup;
            int pass = Mathf.FloorToInt(Elapsed/cycle);
            if(pass >= Mathf.Clamp(passes,1,3)){ End(); return; }
            if(pass != lastPass){ touched.Clear(); lastPass=pass; }
            bool reverse = pass%2 != 0;
            Vector3 direction = reverse ? -headingVector : headingVector;
            float clock = Elapsed-pass*cycle;
            float progress = Mathf.Clamp01((clock-windup)/travel);
            var p = Vector3.Lerp(reverse?finish:start,reverse?start:finish,progress);
            float arc = Mathf.Clamp01((progress-.24f)/.24f);
            float leap = 4*arc*(1-arc);
            p.y = Ground(p)+pounceHeight*leap;
            bool running = clock >= windup;
            points[0] = new DisasterPoint{id=1,position=p,velocity=direction*(running?Vector3.Distance(start,finish)/travel:1),
                life=running?progress:clock/windup-1,size=leap};
            if(running) Impact(p+Vector3.up*19,new Vector3(11,19,25),Quaternion.LookRotation(direction),(direction+Vector3.up*.25f)*V(0));
        }
    }
}
