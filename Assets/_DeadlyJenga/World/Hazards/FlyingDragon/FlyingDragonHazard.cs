using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    public sealed class FlyingDragonHazard : ShrineDragonHazard
    {
        [SerializeField] FireHazard ignitionTemplate;
        FireHazard fire;
        protected override bool Airborne => true;
        protected override bool ValidateSettings(out string error)
        {
            if(!base.ValidateSettings(out error))return false;
            if(ignitionTemplate)return true;
            error="Assign the Fire disaster for breath impacts.";return false;
        }
        protected override void IgniteImpact(Rigidbody block)
        {
            if(!fire || !fire.Active){
                fire=Instantiate(ignitionTemplate,transform.parent);
                fire.StartAt(block);
            }else fire.IgniteBlock(block);
        }
    }
}
