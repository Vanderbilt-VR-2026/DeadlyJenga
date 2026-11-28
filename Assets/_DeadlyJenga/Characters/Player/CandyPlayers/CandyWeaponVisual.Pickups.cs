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
                    var material=renderer.sharedMaterial;
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    var color=block.HasColor("_BaseColor") ? block.GetColor("_BaseColor") :
                        material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                    parts.Add(new EquipmentModelPart{mesh=mesh.sharedMesh,material=material,local=model.worldToLocalMatrix*renderer.transform.localToWorldMatrix,color=color});
                }
                // Skinned weapon pieces (for example the Chaingun's rotating barrels) are baked into still meshes so
                // the pickup shows the whole weapon. The Iron Suit keeps its badge icon: baking it copies the whole body.
                if(value!=EquipmentKind.PoweredArmor)
                foreach(var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!skinned.enabled||!skinned.sharedMesh)continue;
                    var baked=new Mesh{name=skinned.sharedMesh.name+" (pickup)"};skinned.BakeMesh(baked,true);
                    var material=skinned.sharedMaterial;
                    var block=new MaterialPropertyBlock();skinned.GetPropertyBlock(block);
                    var color=block.HasColor("_BaseColor") ? block.GetColor("_BaseColor") :
                        material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                    parts.Add(new EquipmentModelPart{mesh=baked,material=material,
                        local=model.worldToLocalMatrix*Matrix4x4.TRS(skinned.transform.position,skinned.transform.rotation,Vector3.one),color=color});
                }
                EquipmentModelData.Parts[value]=parts.ToArray();model.gameObject.SetActive(false);Object.Destroy(model.gameObject);
            }
            barrel=rotor=null;barrelInitialized=false;
        }
    }
}
