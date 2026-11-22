using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class FossilTar : MonoBehaviour
    {
        internal FossilBasinMap map;
        void OnTriggerEnter(Collider other)
        {
            if(!map || !map.Authority || other.isTrigger || other.gameObject.layer==Layers.Block)return;
            var life=other.GetComponentInParent<IDamageable>();
            if(life!=null && life.Health>0)life.Eliminate(gameObject);
        }
    }
}
