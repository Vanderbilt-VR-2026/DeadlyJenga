using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        bool powerupPage;
        bool giveEquipmentPage,giveAllEquipment,repeatEquipment;
        int wheelCategory;
        EquipmentKind wheelSelected;
        bool wheelLayoutActive;
        WheelRowState[] wheelRows;
        Vector2 wheelHelpPosition,wheelHelpSize;
        string wheelFeedback;
        static readonly string[] WheelCategories={"RANGED","MELEE","POWERUPS"};
        static readonly EquipmentKind[][] WheelGroups={WheelItems(0),WheelItems(1),WheelItems(2)};
        static readonly Vector2[] WheelPositions={new Vector2(0,165),new Vector2(150,105),new Vector2(150,-15),new Vector2(0,-75),new Vector2(-150,-15),new Vector2(-150,105)};

        sealed class WheelRowState
        {
            public Vector3 rowPosition,rowScale,labelPosition,labelScale,iconPosition,iconScale,hitSize;
            public Quaternion rowRotation,labelRotation,iconRotation;
            public Vector2 rowSize,labelSize,iconSize;
            public int fontSize,minFont,maxFont;
            public bool bestFit,iconActive;
            public Color iconColor,rowColor,labelColor;
            public Texture texture;
            public Rect uv;
        }
        static EquipmentKind[] WheelItems(int category)
        {
            var items=new System.Collections.Generic.List<EquipmentKind>();
            for(int i=1;i<EquipmentSpecifications.All.Length;i++)
            {
                var kind=(EquipmentKind)i;var specification=EquipmentSpecifications.Get(kind);
                bool power=specification.power||kind==EquipmentKind.PoweredArmor;
                if(category==2?power:category==1?!power&&specification.melee:!power&&!specification.melee)items.Add(kind);
            }
            return items.ToArray();
        }
        void BeginEquipmentWheel()
        {
            if(wheelLayoutActive)return;
            if(quickGunPage)giveAllEquipment=false;
            wheelRows??=new WheelRowState[buttons.Length];
            for(int i=0;i<buttons.Length;i++)
            {
                var row=(RectTransform)buttons[i].transform;var label=labels[i].rectTransform;var icon=sectionGlyphs[i].rectTransform;
                var state=wheelRows[i]??=new WheelRowState();
                state.rowPosition=row.anchoredPosition3D;state.rowSize=row.sizeDelta;state.rowScale=row.localScale;state.rowRotation=row.localRotation;
                state.labelPosition=label.anchoredPosition3D;state.labelSize=label.sizeDelta;state.labelScale=label.localScale;state.labelRotation=label.localRotation;
                state.iconPosition=icon.anchoredPosition3D;state.iconSize=icon.sizeDelta;state.iconScale=icon.localScale;state.iconRotation=icon.localRotation;
                state.rowColor=buttons[i].image.color;state.labelColor=labels[i].color;state.hitSize=targets[i].size;state.fontSize=labels[i].fontSize;state.minFont=labels[i].resizeTextMinSize;state.maxFont=labels[i].resizeTextMaxSize;state.bestFit=labels[i].resizeTextForBestFit;
                state.iconColor=sectionGlyphs[i].color;state.texture=sectionGlyphs[i].texture;state.uv=sectionGlyphs[i].uvRect;state.iconActive=sectionGlyphs[i].gameObject.activeSelf;
            }
            wheelHelpPosition=help.rectTransform.anchoredPosition;wheelHelpSize=help.rectTransform.sizeDelta;
            wheelLayoutActive=true;wheelFeedback="";wheelCategory=0;equipmentOffset=0;wheelSelected=EquipmentKind.Blaster;
        }
        void RestoreEquipmentWheelLayout()
        {
            if(!wheelLayoutActive)return;
            wheelLayoutActive=false;sectionGlyphsActive=false;
            for(int i=0;i<buttons.Length;i++)
            {
                var state=wheelRows?[i];if(state==null||!buttons[i]||!labels[i]||!sectionGlyphs[i])continue;
                var row=(RectTransform)buttons[i].transform;var label=labels[i].rectTransform;var icon=sectionGlyphs[i].rectTransform;
                row.anchoredPosition3D=state.rowPosition;row.sizeDelta=state.rowSize;row.localScale=state.rowScale;row.localRotation=state.rowRotation;
                label.anchoredPosition3D=state.labelPosition;label.sizeDelta=state.labelSize;label.localScale=state.labelScale;label.localRotation=state.labelRotation;
                icon.anchoredPosition3D=state.iconPosition;icon.sizeDelta=state.iconSize;icon.localScale=state.iconScale;icon.localRotation=state.iconRotation;
                buttons[i].image.color=state.rowColor;labels[i].color=state.labelColor;targets[i].size=state.hitSize;labels[i].fontSize=state.fontSize;labels[i].resizeTextMinSize=state.minFont;labels[i].resizeTextMaxSize=state.maxFont;labels[i].resizeTextForBestFit=state.bestFit;
                sectionGlyphs[i].color=state.iconColor;sectionGlyphs[i].texture=state.texture;sectionGlyphs[i].uvRect=state.uv;sectionGlyphs[i].gameObject.SetActive(state.iconActive);
            }
            if(help){help.rectTransform.anchoredPosition=wheelHelpPosition;help.rectTransform.sizeDelta=wheelHelpSize;}
        }
        void WheelRow(int index,Vector2 position,Vector2 size,string title,bool enabled,int font=19)
        {
            buttons[index].gameObject.SetActive(true);targets[index].enabled=enabled;
            var row=(RectTransform)buttons[index].transform;row.anchoredPosition3D=new Vector3(position.x,position.y,0);row.sizeDelta=size;row.localScale=Vector3.one;row.localRotation=Quaternion.identity;
            targets[index].size=new Vector3(size.x,size.y,4);
            labels[index].text=title;labels[index].fontSize=font;labels[index].resizeTextForBestFit=false;
            labels[index].rectTransform.anchoredPosition3D=Vector3.zero;labels[index].rectTransform.sizeDelta=size-new Vector2(14,4);labels[index].rectTransform.localScale=Vector3.one;labels[index].rectTransform.localRotation=Quaternion.identity;
            sectionGlyphs[index].gameObject.SetActive(false);buttons[index].image.color=CandyHudVisuals.Card;
        }
        static bool WheelAlternateView(EquipmentKind kind)=>kind==EquipmentKind.DualRifle&&Mathf.FloorToInt(Time.unscaledTime/1.5f)%2==1;
        void WheelIcon(int index,EquipmentKind kind,Vector2 position,Vector2 box)
        {
            var image=sectionGlyphs[index];var sprite=DeadlyJenga.Presentation.CandyWeaponIcons.Get(kind,index==6&&WheelAlternateView(kind));
            image.gameObject.SetActive(true);image.rectTransform.anchoredPosition3D=new Vector3(position.x,position.y,0);image.rectTransform.localScale=Vector3.one;image.rectTransform.localRotation=Quaternion.identity;
            if(sprite)
            {
                image.texture=sprite.texture;var rect=sprite.rect;
                image.uvRect=new Rect(rect.x/sprite.texture.width,rect.y/sprite.texture.height,rect.width/sprite.texture.width,rect.height/sprite.texture.height);
                float fit=Mathf.Min(box.x/rect.width,box.y/rect.height);image.rectTransform.sizeDelta=new Vector2(rect.width*fit,rect.height*fit);image.color=Color.white;
            }
            else
            {
                var icon=kind==EquipmentKind.Jump?CandyHudIcon.Jump:kind==EquipmentKind.Speed?CandyHudIcon.Move:kind==EquipmentKind.FlyingHand?CandyHudIcon.Hand:CandyHudIcon.Key;
                image.texture=CandyHudVisuals.Icon(icon);image.uvRect=new Rect(0,0,1,1);image.rectTransform.sizeDelta=Vector2.one*Mathf.Min(box.x,box.y);
                image.color=kind==EquipmentKind.Jump?new Color(.25f,1,.30f):kind==EquipmentKind.Speed?new Color(.25f,.70f,1):CandyHudVisuals.Mint;
            }
        }
        void PointGiveEquipmentPage(Camera camera,bool pressed)
        {
            BeginEquipmentWheel();scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();sectionGlyphsActive=true;
            foreach(var preview in mapThumbnails)if(preview)preview.gameObject.SetActive(false);
            var items=WheelGroups[wheelCategory];int pageCount=Mathf.Max(1,(items.Length+5)/6);
            equipmentOffset=Mathf.Clamp(equipmentOffset/6,0,pageCount-1)*6;
            if(System.Array.IndexOf(items,wheelSelected)<0)wheelSelected=items.Length>0?items[equipmentOffset]:EquipmentKind.None;
            var runtime=MatchRuntime.Current;var self=runtime as IDemoEquipmentControls;var everyone=runtime as IDemoLoadoutControls;
            bool host=DemoMode&&CanCommand&&runtime?.Authority==true;
            bool canSelf=host&&!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true&&self!=null;
            bool canEveryone=host&&everyone!=null&&wheelSelected!=EquipmentKind.FlyingHand;
            if(!giveAllEquipment||wheelSelected==EquipmentKind.FlyingHand)repeatEquipment=false;
            bool canGive=wheelSelected!=EquipmentKind.None&&(giveAllEquipment?canEveryone:canSelf);
            heading.text="EQUIPMENT WHEEL / "+WheelCategories[wheelCategory];
            heading.rectTransform.anchoredPosition=new Vector2(0,232);heading.rectTransform.sizeDelta=new Vector2(900,44);
            for(int i=0;i<6;i++)
            {
                int item=equipmentOffset+i;bool visible=item<items.Length;
                buttons[i].gameObject.SetActive(visible);targets[i].enabled=visible;if(!visible){sectionGlyphs[i].gameObject.SetActive(false);continue;}
                var kind=items[item];WheelRow(i,WheelPositions[i],new Vector2(130,96),EquipmentSpecifications.Get(kind).name,true,17);
                labels[i].rectTransform.anchoredPosition=new Vector2(0,-30);labels[i].rectTransform.sizeDelta=new Vector2(122,40);
                WheelIcon(i,kind,new Vector2(0,14),new Vector2(100,48));
                if(kind==wheelSelected)buttons[i].image.color=CandyHudVisuals.Lavender;
            }
            string previewName=wheelSelected==EquipmentKind.DualRifle?"DUAL RIFLE\n"+(WheelAlternateView(wheelSelected)?"SHOTGUN VIEW":"RIFLE VIEW"):EquipmentSpecifications.Get(wheelSelected).name;
            WheelRow(6,new Vector2(0,45),new Vector2(150,100),previewName,false,18);
            labels[6].rectTransform.anchoredPosition=new Vector2(0,-33);labels[6].rectTransform.sizeDelta=new Vector2(142,40);WheelIcon(6,wheelSelected,new Vector2(0,18),new Vector2(130,52));
            WheelRow(7,new Vector2(0,-160),new Vector2(520,52),"GIVE SELECTED / "+(giveAllEquipment?"EVERYONE":"MYSELF"),canGive,22);
            WheelRow(8,new Vector2(-355,70),new Vector2(250,46),"Target: "+(giveAllEquipment?"EVERYONE":"MYSELF"),host&&everyone!=null,18);
            WheelRow(9,new Vector2(355,130),new Vector2(250,46),giveAllEquipment?repeatEquipment?"NOW + EVERY RESPAWN":"THIS LIFE ONLY":"SELF / THIS LIFE ONLY",host&&everyone!=null&&giveAllEquipment&&wheelSelected!=EquipmentKind.FlyingHand,17);
            WheelRow(10,new Vector2(355,70),new Vector2(250,46),"Stop everyone respawn grants",host&&everyone!=null&&everyone.RespawnEquipment!=EquipmentKind.None,18);
            WheelRow(11,new Vector2(-355,130),new Vector2(250,46),"Category: "+WheelCategories[wheelCategory]+" / change",true,20);
            WheelRow(12,new Vector2(-355,10),new Vector2(250,46),"< PREVIOUS",equipmentOffset>0,17);
            WheelRow(13,new Vector2(355,10),new Vector2(250,46),"NEXT >",equipmentOffset+6<items.Length,17);
            WheelRow(14,new Vector2(0,-222),new Vector2(300,46),"BACK / "+(equipmentOffset/6+1)+" OF "+pageCount,true,17);
            help.rectTransform.anchoredPosition=new Vector2(0,-292);help.rectTransform.sizeDelta=new Vector2(700,68);
            string state=!host?"Host/demo authority is required to give. Preview is available.":giveAllEquipment&&wheelSelected==EquipmentKind.FlyingHand?"Flying hand transformation is self-only; choose Myself.":wheelSelected==EquipmentKind.DualRifle?"One item; center preview alternates rifle / shotgun.":giveAllEquipment?"Everyone = living survivor players + bots; no bots-only grant.":"One inventory item at a time; select a card before Give.";
            help.text=(string.IsNullOrEmpty(wheelFeedback)?state:wheelFeedback)+"\nRespawn grant: "+EquipmentSpecifications.Get(everyone?.RespawnEquipment??EquipmentKind.None).name;
            for(int i=0;i<buttons.Length;i++)
            {
                if(!buttons[i].gameObject.activeSelf)continue;
                bool hover=targets[i].enabled&&Hit(targets[i],pointerRay);if(hover)buttons[i].image.color=CandyHudVisuals.Mint;
                labels[i].color=buttons[i].image.color.grayscale>.5f?CandyHudVisuals.Ink:targets[i].enabled?Color.white:CandyHudVisuals.Muted;
                if(!hover||!pressed)continue;
                wheelFeedback="";
                if(i<6)wheelSelected=items[equipmentOffset+i];
                else if(i==7)
                {
                    bool given=giveAllEquipment?everyone.GiveEveryoneEquipment(wheelSelected,repeatEquipment):self.GiveEquipment(wheelSelected);
                    if(given){RestoreEquipmentWheelLayout();session.SetMenu(false);}else wheelFeedback="Request not completed: current rules or eligible recipients may have changed.";
                }
                else if(i==8){giveAllEquipment=!giveAllEquipment;if(!giveAllEquipment)repeatEquipment=false;}
                else if(i==9)repeatEquipment=!repeatEquipment;
                else if(i==10){if(!everyone.GiveEveryoneEquipment(EquipmentKind.None,false))wheelFeedback="Stop request was not accepted; check the current respawn grant below.";}
                else if(i==11){wheelCategory=(wheelCategory+1)%WheelGroups.Length;equipmentOffset=0;wheelSelected=WheelGroups[wheelCategory][0];quickGunPage=false;}
                else if(i==12||i==13){equipmentOffset+=i==12?-6:6;wheelSelected=items[equipmentOffset];}
                else if(i==14){RestoreEquipmentWheelLayout();giveEquipmentPage=quickGunPage=false;menuSection=-1;}
                break;
            }
        }
        void PointPowerupPage(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            heading.text="POWERUPS / ENEMIES\nHost can spawn jaguar enemies for testing";
            var enemies=session.SurvivorBots as IEnemyPopulation;bool canEnemies=enemies!=null&&MatchRuntime.Current?.Authority==true;
            var gun=session.Player.gameObject.GetComponent<IBlasterControls>();var equipment=session.Player.gameObject.GetComponent<IEquipmentControls>();
            string[] names={"Back","Spawn 2x speed + infinite stamina","Spawn 3x jump + infinite stamina","Give myself a weapon / powerup","Spawn jaguar enemy (20 damage)","Clear jaguar enemies","Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),equipment?.Equipment.drawn==true?"Holster weapon":"Draw weapon","Dominant hand: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),"Spawn alien blaster","Spawn murderer sword","Spawn sheriff revolver","Spawn dual rifle / shotgun","Spawn giant boxing glove","Spawn powered iron suit"};
            var ray=pointerRay;
            bool canSpawn=DemoMode&&CanCommand&&!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true;
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].gameObject.SetActive(i<names.Length);if(i>=names.Length)continue;
                targets[i].enabled=i>=9?canSpawn:i==6?DemoMode&&canEnemies&&gun!=null:i==7?equipment?.Equipment.kind>=EquipmentKind.Blaster:i==8?gun!=null:i>=4?canEnemies:i==0||i==3||canSpawn;labels[i].text=names[i];
                bool hover=targets[i].enabled&&Hit(targets[i],ray);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;
                labels[i].color=targets[i].enabled?Color.white:CandyHudVisuals.Muted;
                if(!hover||!pressed)continue;
                if(i==0)powerupPage=false;
                if(i==3){giveEquipmentPage=true;}
                if(i==1||i==2){Demo.Run(i==1?DemoCommand.SpawnSpeedPowerup:DemoCommand.SpawnJumpPowerup);session.SetMenu(false);}
                if(i==4){enemies.SpawnEnemy(new EnemySpawnSettings());session.SetMenu(false);}
                if(i==5)enemies.ClearEnemies();
                if(i==6)gun.SetTestingEnabled(!gun.TestingEnabled);
                if(i==7){equipment.SetEquipped(!equipment.Equipment.drawn);session.SetMenu(false);}
                if(i==8)gun.SetLeftHanded(!gun.LeftHanded);
                if(i>=9){(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment((EquipmentKind)(i-6));session.SetMenu(false);}
                break;
            }
            help.text=canSpawn?"Both powers last 20 seconds after collection.\nTimers appear in your view. Repeat pickups refresh the timer.":"Return to your own survivor to test powerups.";
            
        }
    }
}
