using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    public sealed class CandyAvatarAttachments : MonoBehaviour
    {
        [SerializeField] AvatarAppearance appearance;
        CandyAvatarWardrobe wardrobe;
        CandyJaguarAppearance jaguar;
        CandyMushroomAppearance mushroom;
        [SerializeField] MeshRenderer[] renderers;
        public AvatarAppearance Appearance=>appearance;
        public void Apply(CandyPlayerRig rig,AvatarAppearance value)
        {
            value.accessories&=CandyPlayerAvatar.SupportedAccessories;
            if(rig.headRenderer)rig.headRenderer.enabled=(value.accessories&AvatarAccessory.SkeletonMask)==0;
            if(!wardrobe)wardrobe=GetComponent<CandyAvatarWardrobe>();if(!wardrobe)wardrobe=gameObject.AddComponent<CandyAvatarWardrobe>();
            wardrobe.Apply(rig,value);
            if(!mushroom)mushroom=GetComponent<CandyMushroomAppearance>();
            if(!mushroom&&value.skin==AvatarSkin.MushroomSprite)mushroom=gameObject.AddComponent<CandyMushroomAppearance>();
            if(mushroom)mushroom.Apply(rig,value.skin==AvatarSkin.MushroomSprite,(value.accessories&AvatarAccessory.SkeletonMask)!=0);
            const AvatarAccessory feline=AvatarAccessory.PantherHead|AvatarAccessory.PantherClaws|AvatarAccessory.PantherTail;
            if(!jaguar)jaguar=GetComponent<CandyJaguarAppearance>();
            if(!jaguar&&(value.accessories&feline)!=0)jaguar=gameObject.AddComponent<CandyJaguarAppearance>();
            if(jaguar)jaguar.Apply(rig,value.accessories);
            if(renderers!=null&&appearance.accessories==value.accessories){appearance=value;return;}
            if(renderers!=null)foreach(var r in renderers)if(r){r.gameObject.SetActive(false);Destroy(r.gameObject);}
            appearance=value;var list=new System.Collections.Generic.List<MeshRenderer>();
            for(int i=0;i<32;i++)
            {
                var option=(AvatarAccessory)(1u<<i);if((value.accessories&option)==0)continue;
                if((option&feline)!=0||option==AvatarAccessory.PumpkinHelmet)continue;
                if(option==AvatarAccessory.PantherClaws){Add(option,rig.LeftPalm);Add(option,rig.RightPalm);}
                else Add(option,option==AvatarAccessory.Cape||option==AvatarAccessory.PantherTail||option==AvatarAccessory.AngelWings||option==AvatarAccessory.ButterflyWings||option==AvatarAccessory.DragonWings?rig.BodyAnchor:rig.HeadAnchor);
            }
            renderers=list.ToArray();Tint(rig);
            void Add(AvatarAccessory option,Transform bone)
            {
                var go=new GameObject("Attachment - "+option);go.transform.SetParent(bone,false);
                go.AddComponent<MeshFilter>().sharedMesh=CandyAccessoryMeshes.Get(option);list.Add(go.AddComponent<MeshRenderer>());
            }
        }
        public void RefreshPalette(CandyPlayerRig rig){if(wardrobe)wardrobe.RefreshPalette(rig);}
        public void Tint(CandyPlayerRig rig)
        {
            if(renderers==null||!rig.bodyRenderer)return;var properties=new MaterialPropertyBlock();rig.bodyRenderer.GetPropertyBlock(properties);
            foreach(var r in renderers)if(r){r.sharedMaterial=rig.bodyRenderer.sharedMaterial;r.SetPropertyBlock(properties);}
        }
        public void FirstPerson(CandyPlayerRig rig,bool active)
        {
            if(wardrobe)wardrobe.FirstPerson(rig,active);
            if(jaguar)jaguar.FirstPerson(active);
            if(mushroom)mushroom.FirstPerson(active);
            if(renderers==null)return;
            foreach(var r in renderers)if(r)r.shadowCastingMode=active&&(r.transform.parent==rig.HeadAnchor||r.transform.parent==rig.BodyAnchor)?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
        }
    }
}
