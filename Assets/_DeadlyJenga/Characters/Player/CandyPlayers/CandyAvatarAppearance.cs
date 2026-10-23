using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerAvatar : IAvatarAppearance
    {
        const string AppearanceKey="CandyAvatar.Accessories.v1";
        public const AvatarAccessory SupportedAccessories=(AvatarAccessory)65535;
        [SerializeField] AvatarAppearance appearance;
        public AvatarAppearance Appearance=>appearance;
        public AvatarAppearance EquippedAppearance
        {get{var value=appearance;if(GetComponent<CandyEquipment>()?.Equipment.kind==EquipmentKind.PoweredArmor)value.poweredArmor=true;return value;}}
        bool OwnAppearance=>!remoteDriven&&(!survivor||!survivor.externallyDriven);

        public static AvatarAppearance SanitizeAppearance(AvatarAppearance value)
        {
            value.accessories&=SupportedAccessories;
            if((byte)value.skin>6)value.skin=0;if((byte)value.hair>12)value.hair=0;if((byte)value.eyewear>7)value.eyewear=0;
            value.clothing&=(AvatarClothing)511;return value;
        }
        public static uint StyleBits(AvatarAppearance value)=> (uint)value.skin|((uint)value.hair<<3)|((uint)value.eyewear<<7)|((uint)value.clothing<<10)|(value.poweredArmor?1u<<19:0);
        public static AvatarAppearance WithStyle(AvatarAppearance value,uint bits)
        {value.skin=(AvatarSkin)(bits&7);value.hair=(AvatarHair)((bits>>3)&15);value.eyewear=(AvatarEyewear)((bits>>7)&7);value.clothing=(AvatarClothing)((bits>>10)&511);value.poweredArmor=(bits&(1u<<19))!=0;return SanitizeAppearance(value);}
        void LoadAppearance()
        {
            if(OwnAppearance){appearance.accessories=(AvatarAccessory)(uint)PlayerPrefs.GetInt(AppearanceKey,0)&SupportedAccessories;
                var json=PlayerPrefs.GetString("CandyAvatar.Style.v2","");if(!string.IsNullOrEmpty(json)){try{appearance=JsonUtility.FromJson<AvatarAppearance>(json);}catch(System.ArgumentException){}}}
            appearance=SanitizeAppearance(appearance);
            Rig.SetAppearance(appearance);
        }
        public void ApplyAppearance(AvatarAppearance value)
        {
            value=SanitizeAppearance(value);appearance=value;
            if(Rig)Rig.SetAppearance(value);
            if(OwnAppearance){PlayerPrefs.SetInt(AppearanceKey,(int)value.accessories);PlayerPrefs.SetString("CandyAvatar.Style.v2",JsonUtility.ToJson(value));PlayerPrefs.Save();}
        }
    }
}
