using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        sealed class EquipmentAnnouncement { public EquipmentActionFrame value;public float expires; }
        readonly List<EquipmentAnnouncement> equipmentAnnouncements=new List<EquipmentAnnouncement>();
        string equipmentEpoch=Guid.NewGuid().ToString("N");int equipmentSequence;
        void ResetEquipmentAnnouncements()
        {equipmentAnnouncements.Clear();}
        void AnnounceEquipment(string id,EquipmentState state,EquipmentAction action,Vector3 position,Vector3[] endpoints=null)
        {
            if(!Authority||state==null||state.kind==EquipmentKind.None||!Finite(position))return;
            if(equipmentSequence==int.MaxValue){ResetEquipmentAnnouncements();equipmentSequence=0;equipmentEpoch=Guid.NewGuid().ToString("N");}
            if(equipmentAnnouncements.Count>=32)equipmentAnnouncements.RemoveAt(0);
            equipmentAnnouncements.Add(new EquipmentAnnouncement{expires=Time.time+.8f,value=new EquipmentActionFrame{
                id=++equipmentSequence,actor=id,kind=state.kind,action=action,shotgun=state.shotgun,position=position,
                endpoints=endpoints??Array.Empty<Vector3>(),remaining=.8f}});
            PublishEquipmentAnnouncements();
        }
        void PublishEquipmentAnnouncements()
        {
            equipmentAnnouncements.RemoveAll(item=>item.expires<=Time.time);
            frame.equipmentEpoch=equipmentEpoch;frame.equipmentSequence=equipmentSequence;
            frame.equipmentActions=new EquipmentActionFrame[equipmentAnnouncements.Count];
            for(int i=0;i<equipmentAnnouncements.Count;i++)
            {
                var item=equipmentAnnouncements[i];var value=item.value;
                frame.equipmentActions[i]=new EquipmentActionFrame{id=value.id,actor=value.actor,kind=value.kind,
                    action=value.action,shotgun=value.shotgun,position=value.position,endpoints=(Vector3[])value.endpoints.Clone(),
                    remaining=Mathf.Clamp(item.expires-Time.time,0,.8f)};
            }
        }
    }
}
