using System;
using DeadlyJenga.Core;
using UnityEngine;
using DeadlyJenga.Presentation;

namespace DeadlyJenga.CandyAudio
{
    // One cursor per observer. Joining establishes a baseline instead of replaying old action history.
    public sealed class CandyEquipmentActionCursor
    {
        string epoch;int sequence;bool initialized;
        public void Baseline(MatchFrame frame)
        {epoch=frame?.equipmentEpoch;sequence=frame?.equipmentSequence??0;initialized=frame!=null;}
        public void Read(MatchFrame frame,Action<EquipmentActionFrame> receive)
        {
            if(frame==null)return;
            if(!initialized){Baseline(frame);return;}
            if(frame.equipmentEpoch!=epoch){Baseline(frame);return;}
            if(frame.equipmentSequence<=sequence)return;
            foreach(var item in frame.equipmentActions??Array.Empty<EquipmentActionFrame>())
                if(item!=null&&item.id>sequence&&item.id<=frame.equipmentSequence&&item.remaining>0)receive(item);
            sequence=frame.equipmentSequence;
        }
    }
    [DefaultExecutionOrder(350)]
    public sealed class CandyWeaponFeedback : MonoBehaviour
    {
        readonly CandyEquipmentActionCursor cursor=new CandyEquipmentActionCursor();
        IMatchRuntime runtime;CandyBulletTrails trails;
        public int ConsumedActions {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=host=>{if(!host.GetComponent<CandyWeaponFeedback>())host.AddComponent<CandyWeaponFeedback>();};
        void Awake(){trails=GetComponent<CandyBulletTrails>();if(!trails)trails=gameObject.AddComponent<CandyBulletTrails>();Bind();}
        void Bind(){runtime=MatchRuntime.Current;cursor.Baseline(runtime?.Frame);}
        void Update()
        {
            if(!ReferenceEquals(runtime,MatchRuntime.Current)){Bind();return;}
            cursor.Read(runtime?.Frame,Present);
        }
        void Present(EquipmentActionFrame item)
        {
            ConsumedActions++;
            if(item.action==EquipmentAction.Shot&&item.kind==EquipmentKind.DualRifle)
            {
                var camera=Services.Session?.ActiveCamera;
                if(camera&&(camera.transform.position-item.position).sqrMagnitude<90*90)trails.Fire(item.position,item.endpoints,item.shotgun);
            }
            var engine=CandyAudioEngine.Instance;if(!engine)return;
            CandySound sound;
            switch(item.action)
            {
                case EquipmentAction.Draw:sound=item.kind==EquipmentKind.PoweredArmor?CandySound.SuitIgnition:CandySound.WeaponDraw;break;
                case EquipmentAction.Holster:sound=item.kind==EquipmentKind.PoweredArmor?CandySound.SuitShutdown:CandySound.WeaponHolster;break;
                case EquipmentAction.Pickup:sound=CandySound.WeaponPickup;break;
                case EquipmentAction.Swap:sound=CandySound.WeaponSwap;break;
                case EquipmentAction.Drop:sound=CandySound.WeaponDrop;break;
                case EquipmentAction.ModeSwitch:sound=CandySound.WeaponTransform;break;
                case EquipmentAction.Impact:sound=CandySound.WeaponPlasmaImpact;break;
                case EquipmentAction.Shot:
                    sound=item.kind==EquipmentKind.DualRifle?(item.shotgun?CandySound.WeaponShotgun:CandySound.WeaponRifle):
                        (item.kind==EquipmentKind.Blaster||item.kind==EquipmentKind.PoweredArmor)?CandySound.WeaponBlaster:item.kind==EquipmentKind.MurderSword?CandySound.WeaponSword:
                        item.kind==EquipmentKind.SheriffHammer?CandySound.WeaponHammer:CandySound.WeaponBoxing;break;
                default:return;
            }
            float pitch=item.action==EquipmentAction.ModeSwitch?(item.shotgun?.88f:1.12f):1;
            engine.Play(sound,item.position,1,item.actor?.GetHashCode()??0,pitch);
        }
    }
}
