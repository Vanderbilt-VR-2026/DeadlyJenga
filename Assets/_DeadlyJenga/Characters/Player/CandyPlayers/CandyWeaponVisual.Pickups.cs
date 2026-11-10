using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        // Pickup models are built by a private builder: on demand in the lobby, all at once while a match scene loads.
        static CandyWeaponVisual builder;
        CandyWeaponVisual(Material material,bool _)
        {
            var host=new GameObject("Pickup model builder");Object.DontDestroyOnLoad(host);host.transform.position=Vector3.down*500;
            parent=host.transform;this.material=material;
        }
        Transform ArmorPickup()
        {
            var prefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");if(!prefab)return new GameObject("Armor preview unavailable").transform;
            var rig=Object.Instantiate(prefab,parent,false);rig.ownerCamera=null;
            rig.SetAppearance(new AvatarAppearance{poweredArmor=true});
            rig.ApplyFrame(new CandyAvatarFrame{feet=Vector3.zero,head=Vector3.up*1.6f,headRotation=Quaternion.identity,scale=1,left=new CandyHandPose{position=new Vector3(-.35f,.85f,0),rotation=Quaternion.identity},right=new CandyHandPose{position=new Vector3(.35f,.85f,0),rotation=Quaternion.identity}});
            return rig.transform;
        }
        static bool Pickup(EquipmentKind value)=>value>=EquipmentKind.Blaster&&(!EquipmentSpecifications.Get(value).power||value==EquipmentKind.FlyingHand);
        void PreparePickupModels()
        {
            if(builder!=null&&builder.parent)return;
            builder=new CandyWeaponVisual(material,true);
            EquipmentModelData.Parts.Clear();
            EquipmentModelData.Build=kind=>builder!=null&&builder.BuildPickup(kind);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded-=BuildAllOnMatchLoad;UnityEngine.SceneManagement.SceneManager.sceneLoaded+=BuildAllOnMatchLoad;
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();BuildAllOnMatchLoad(active,default);
        }
        // Matches build every pickup model while their scene loads, so the cost hides inside the load.
        static void BuildAllOnMatchLoad(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if(builder==null||scene.name=="Lobby"||scene.name=="Home")return;
            foreach(EquipmentKind value in System.Enum.GetValues(typeof(EquipmentKind)))builder.BuildPickup(value);
        }
        bool BuildPickup(EquipmentKind value)
        {
            if(!Pickup(value))return false;
            if(EquipmentModelData.Parts.ContainsKey(value))return true;
            if(!parent)return false;
            var model=value==EquipmentKind.PoweredArmor?ArmorPickup():Create(value);var parts=new List<EquipmentModelPart>();
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled)continue;var mesh=renderer.GetComponent<MeshFilter>();if(!mesh||!mesh.sharedMesh)continue;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);var color=block.GetColor("_BaseColor");if(color.a==0)color=Color.white;
                parts.Add(new EquipmentModelPart{mesh=mesh.sharedMesh,material=renderer.sharedMaterial,local=model.worldToLocalMatrix*renderer.transform.localToWorldMatrix,color=color});
            }
            EquipmentModelData.Parts[value]=parts.ToArray();model.gameObject.SetActive(false);Object.Destroy(model.gameObject);
            barrel=rotor=null;barrelInitialized=false;
            return true;
        }
    }
}
