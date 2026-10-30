using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        static bool prepared;
        Transform ArmorPickup()
        {
            var prefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");if(!prefab)return new GameObject("Armor preview unavailable").transform;
            var rig=Object.Instantiate(prefab,parent,false);rig.ownerCamera=null;
            rig.SetAppearance(new AvatarAppearance{poweredArmor=true});
            rig.ApplyFrame(new CandyAvatarFrame{feet=Vector3.zero,head=Vector3.up*1.6f,headRotation=Quaternion.identity,scale=1,left=new CandyHandPose{position=new Vector3(-.35f,.85f,0),rotation=Quaternion.identity},right=new CandyHandPose{position=new Vector3(.35f,.85f,0),rotation=Quaternion.identity}});
            return rig.transform;
        }
        void PreparePickupModels()
        {
            if(prepared)return;prepared=true;EquipmentModelData.Parts.Clear();
            foreach(EquipmentKind value in System.Enum.GetValues(typeof(EquipmentKind)))
            {
                if(value<EquipmentKind.Blaster||EquipmentSpecifications.Get(value).power&&value!=EquipmentKind.FlyingHand)continue;
                var model=value==EquipmentKind.PoweredArmor?ArmorPickup():Create(value);var parts=new List<EquipmentModelPart>();
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    if(!renderer.enabled)continue;var mesh=renderer.GetComponent<MeshFilter>();if(!mesh||!mesh.sharedMesh)continue;
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);var color=block.GetColor("_BaseColor");if(color.a==0)color=Color.white;
                    parts.Add(new EquipmentModelPart{mesh=mesh.sharedMesh,material=renderer.sharedMaterial,local=model.worldToLocalMatrix*renderer.transform.localToWorldMatrix,color=color});
                }
                EquipmentModelData.Parts[value]=parts.ToArray();model.gameObject.SetActive(false);Object.Destroy(model.gameObject);
            }
            barrel=rotor=null;barrelInitialized=false;
        }
    }
}
