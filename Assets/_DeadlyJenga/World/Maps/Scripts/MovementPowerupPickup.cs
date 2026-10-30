using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class MovementPowerupPickup : MonoBehaviour,IEquipmentPickupSource
    {
        public EffectType effect;
        public float duration=20;
        public EquipmentKind Kind=>effect&&effect.name=="Speed2x"?EquipmentKind.Speed:effect&&effect.name=="Jump3x"?EquipmentKind.Jump:EquipmentKind.None;
        public Vector3 Position=>transform.position;
        public float Duration=>duration;
        int equipmentId;
        bool collected;
        float expires;
        void OnEnable(){collected=false;expires=Time.time+60;}
        void Update()
        {
            var runtime=MatchRuntime.Current as IEquipmentRuntime;
            if(runtime!=null&&equipmentId==0)equipmentId=runtime.RegisterPickup(this);
            if(equipmentId>0){var visual=transform.Find("Visual");if(visual)visual.gameObject.SetActive(false);return;}
            if(Time.time>=expires)Destroy(gameObject);
        }
        void OnTriggerEnter(Collider other)
        {
            var player=Services.Session?.Player;
            if(player==null||!other.transform.IsChildOf(player.gameObject.transform))return;
            Collect(player.gameObject);
        }
        public bool Collect(GameObject recipient)
        {
            if(collected||!effect||!recipient||!recipient.activeInHierarchy||!recipient.TryGetComponent<IAffectable>(out var target))return false;
            if(recipient.TryGetComponent<IDamageable>(out var health)&&health.Health<=0)return false;
            if(MatchRuntime.Current is IEquipmentRuntime runtime)
            {
                if(equipmentId==0)equipmentId=runtime.RegisterPickup(this);
                return equipmentId>0&&runtime.RequestPickup(equipmentId,false);
            }
            collected=true;target.Apply(new Effect{type=effect,duration=duration,strength=1,source=gameObject});
            gameObject.SetActive(false);Destroy(gameObject);return true;
        }
    }
}
