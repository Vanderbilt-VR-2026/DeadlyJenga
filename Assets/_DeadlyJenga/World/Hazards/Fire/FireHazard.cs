using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    public sealed class FireHazard : BasicDisaster
    {
        protected override string Kind => "Fire";
        bool impactSpawned;

        internal void StartAt(Rigidbody block)
        {
            impactSpawned=true;
            var settings=new float[Parameters.Count];
            for(int i=0;i<settings.Length;i++)
                settings[i]=Parameters[i].id=="warning"?0:Parameters[i].initial;
            Configure(settings);
            Begin();
            if(!Active){Destroy(gameObject);return;}
            InitializeContactFire();
            IgniteBlock(block);
        }
        protected override void EndHazard()
        {
            base.EndHazard();
            if(impactSpawned)Destroy(gameObject);
        }
    }
}
