using System;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        int menuCategory=-1,menuSection=-1,equipmentOffset;
        bool batchPage,quickGunPage;
        int batchTarget;
        UnityEngine.UI.Button backButton,homeButton;
        BoxCollider backTarget,homeTarget;
        Texture2D disasterPreview;
        bool AtHome=>!batchPage&&!mapPage&&!spectatePage&&!giveEquipmentPage&&!randomPowerupPage&&!powerupPage&&
            !tabletopPage&&!hudPage&&!modePage&&!disasterPage&&!networkPage&&!comfortPage&&!tuningPage&&menuCategory<0;

        void BuildNavigation()
        {
            backButton=HeaderButton("Back",0,out backTarget);
            homeButton=HeaderButton("Demo Home",0,out homeTarget);
            ((RectTransform)backButton.transform).anchoredPosition=new Vector2(-206,510);
            ((RectTransform)homeButton.transform).anchoredPosition=new Vector2(206,510);
        }
        bool PointNavigation(bool pressed)
        {
            bool available=!DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session);
            backButton.gameObject.SetActive(available);homeButton.gameObject.SetActive(available);
            if(!available)return false;
            homeTarget.enabled=!AtHome;
            backButton.GetComponentInChildren<UnityEngine.UI.Text>().text=AtHome?"Resume":"Back";
            bool back=Hit(backTarget,pointerRay),home=Hit(homeTarget,pointerRay);
            backButton.image.color=back?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            homeButton.image.color=home?CandyHudVisuals.Mint:homeTarget.enabled?CandyHudVisuals.Lavender:CandyHudVisuals.Card;
            homeButton.GetComponentInChildren<UnityEngine.UI.Text>().color=homeTarget.enabled?CandyHudVisuals.Ink:CandyHudVisuals.Muted;
            if(!pressed)return false;
            if(home){ResetPages();menuCategory=menuSection=-1;return true;}
            if(!back)return false;
            if(AtHome){session.SetMenu(false);return true;}
            if(disasterPage&&disasterSelection>=0){disasterSelection=-1;return true;}
            if(batchPage&&batchTarget>0){batchTarget=0;return true;}
            bool child=networkPage||comfortPage||tuningPage||modePage||hudPage||mapPage||spectatePage||
                tabletopPage||disasterPage||giveEquipmentPage||randomPowerupPage||powerupPage||batchPage;
            ResetPages();
            if(child||menuSection>=0)menuSection=-1;
            else menuCategory=-1;
            return true;
        }
        void ResetPages()
        {
            roomLeave.Clear();
            networkPage=comfortPage=tuningPage=modePage=hudPage=mapPage=spectatePage=
                tabletopPage=disasterPage=giveEquipmentPage=randomPowerupPage=powerupPage=batchPage=quickGunPage=false;
            equipmentOffset=disasterOffset=mapOffset=batchTarget=0;disasterSelection=-1;
        }
        void SetRowPreview(int i,Texture image)
        {
            if(!image)return;
            var preview=mapThumbnails[i];preview.texture=image;preview.gameObject.SetActive(true);
            float fit=Mathf.Min(108f/image.width,43f/image.height);
            preview.rectTransform.sizeDelta=new Vector2(image.width*fit,image.height*fit);
            labels[i].rectTransform.sizeDelta=new Vector2(350,46);labels[i].rectTransform.anchoredPosition=new Vector2(-34,0);
        }
        void SwitchDemoRole()
        {
            if(!DemoMode)return;
            session.SetRole(!session.PlayingHand);
            session.SetMenu(false);
        }
        void PointOrganizedMenu(Camera camera,bool pressed)
        {
            HideDisasterSliders();scaffoldButton.gameObject.SetActive(false);
            for(int i=0;i<buttons.Length;i++){var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,48);}
            var actions=new System.Collections.Generic.List<Action>();var names=new System.Collections.Generic.List<string>();var enabled=new System.Collections.Generic.List<bool>();var art=new System.Collections.Generic.List<CandyHudIcon>();var captions=new System.Collections.Generic.List<string>();var pictures=new System.Collections.Generic.List<Texture>();
            void Add(string name,Action action,bool available=true,CandyHudIcon icon=CandyHudIcon.Key,string caption="",Texture picture=null)
            {names.Add(name);actions.Add(action);enabled.Add(available);art.Add(icon);captions.Add(caption);pictures.Add(picture);}
            var match=MatchRuntime.Current;
            var bots=session.SurvivorBots;
            int humans=Mathf.Max(1,session.Multiplayer?.HumanCount??1);
            bool canBatch=CanCommand&&match?.Authority==true&&bots!=null;
            var map=Demo as IDemoMapControls;
            var mapPreview=map!=null?MapPreviewImages.Get(map.CurrentMapId):null;
            if(batchPage)
            {
                heading.text="BOT POPULATION";
                Add("Current: "+(bots?.Count??0)+" bots + "+humans+" human",()=>{},false,CandyHudIcon.Friends,picture:mapPreview);
                long capacity=(long)humans+(bots?.Capacity??0);
                foreach(int value in new[]{10,25,50,100,150,200})
                {
                    if(value>capacity)continue;
                    int total=value,needed=Mathf.Max(0,total-humans-(bots?.Count??0));
                    Add("Preview "+total+" total  /  add up to "+needed+" bots",()=>batchTarget=total,canBatch,CandyHudIcon.Survivor,
                        "Total includes humans. No existing bots are removed.");
                }
                if(batchTarget>0)
                {
                    Add("Fill to "+batchTarget+" total",()=>{
                        if(!canBatch)return;bots.TargetPopulation=batchTarget;Demo.Run(DemoCommand.FillSurvivors);
                    },canBatch,CandyHudIcon.Play,"Apply the preview using the existing host fill command.");
                }
                Add("Cancel",()=>{batchPage=false;batchTarget=0;},icon:CandyHudIcon.Home);
            }
            else if(menuCategory<0){heading.text="IN-GAME CONTROLS";
                Add("CONTROLS / LEFT OR RIGHT HAND",()=>DeadlyJenga.Presentation.CandyComfortPanel.Open(session),true,CandyHudIcon.Settings,"Dominant: look / jump / fire. Other: move / sprint.");
                if(!disasterPreview)disasterPreview=Resources.Load<Texture2D>("LobbyModePictures/disasters");
                Add("Rebuild Map",()=>session.ResetMap(),session.CanEditWorld,CandyHudIcon.Home,"Rebuild the current map. Host only.",mapPreview);
                Add("Disasters",()=>{disasterPage=true;disasterSelection=-1;},Demo!=null,CandyHudIcon.Flight,"Preview and tune disasters before launch.",disasterPreview);
                Add("Give Yourself a Gun",()=>{quickGunPage=true;giveEquipmentPage=true;giveAllEquipment=false;equipmentOffset=0;},
                    CanCommand&&!session.PlayingHand&&match?.Authority==true&&match is IDemoEquipmentControls,CandyHudIcon.Trigger,
                    "Choose a firearm. Your previous item drops at your feet.",DeadlyJenga.Presentation.CandyEquipmentView.Outline(EquipmentKind.Blaster));
                Add("Bot Population",()=>{batchPage=true;batchTarget=0;},canBatch,CandyHudIcon.Friends,"Preview a large batch before applying.");
                Add(session.PlayingHand?"Become Survivor":"Become Flying Hand",SwitchDemoRole,DemoMode,CandyHudIcon.Hand,"Switch your local demo character.");
                var hand=session.Player?.gameObject.GetComponent<IBlasterControls>();
                Add("Game / Demo & Round Setup",()=>DeadlyJenga.Presentation.GameExperiencePanel.Open(session),DeadlyJenga.Presentation.GameExperiencePanel.Resolve(session)!=null,CandyHudIcon.Play,"Profile, readiness and automatic rounds");
                Add("Hand: "+(hand?.LeftHanded==true?"LEFT":"RIGHT")+" / change controls",()=>DeadlyJenga.Presentation.CandyComfortPanel.Open(session),true,CandyHudIcon.Hand);
                Add("Hand Appearance",()=>CharacterPicker.Open(session),HandLooks.Names.Length>0,CandyHudIcon.Hand,"Choose the appearance of your flying hand.");
                var drop=match as IEquipmentDropRuntime;var carried=session.Player?.gameObject.GetComponent<IEquipmentControls>();
                Add("Drop weapon here",()=>drop?.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&carried?.Equipment.kind>=EquipmentKind.Blaster,CandyHudIcon.Hand,"Drops your carried item. No controller chord is required.");
                for(int i=0;i<4;i++){int index=i;Add(DemoMenuSections.Tabs[i],()=>{menuCategory=index;menuSection=-1;},icon:DemoMenuSections.Icon(DemoMenuSections.Tabs[i]),caption:DemoMenuSections.TabDescriptions[i]);}Add("Resume",()=>session.SetMenu(false));}
            else if(menuSection<0)
            {
                heading.text=DemoMenuSections.Tabs[menuCategory];Add("All tabs",()=>menuCategory=-1,icon:CandyHudIcon.Home);
                var sections=DemoMenuSections.Sections[menuCategory];for(int i=0;i<sections.Length;i++){int index=i;Add(sections[i]+"   /   "+DemoMenuSections.Caption(sections[i]),()=>OpenMenuSection(index),icon:DemoMenuSections.Icon(sections[i]),caption:DemoMenuSections.Caption(sections[i]));}
            }
            else
            {
                string name=DemoMenuSections.Sections[menuCategory][menuSection];heading.text=DemoMenuSections.Tabs[menuCategory]+" / "+name;Add("Back to "+DemoMenuSections.Tabs[menuCategory],()=>menuSection=-1,icon:CandyHudIcon.Home);
                if(name=="Character")
                {
                    Add(session.PlayingHand?"Become Survivor":"Become Flying Hand",SwitchDemoRole,DemoMode,CandyHudIcon.Hand,"Switch your local demo character.");
                    Add("Respawn on top",()=>{session.SetRole(false);session.SetMenu(false);},DemoMode);
                    Add("Third person inspection",()=>CandyThirdPersonInspection.For(session).Begin(),CandyThirdPersonInspection.For(session)?.CanInspect==true);
                    var item=session.Player.gameObject.GetComponent<IEquipmentControls>();var gun=session.Player.gameObject.GetComponent<IBlasterControls>();
                    Add(item?.Equipment.drawn==true?"Holster "+item.Equipment.kind:"Draw "+(item?.Equipment.kind.ToString()??"equipment"),()=>item?.SetEquipped(!item.Equipment.drawn),item?.Equipment.kind>=EquipmentKind.Blaster);
                    var drop=MatchRuntime.Current as IEquipmentDropRuntime;
                    Add("Drop weapon here",()=>drop?.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&item?.Equipment.kind>=EquipmentKind.Blaster,CandyHudIcon.Hand);
                    Add("Dominant hand: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),()=>gun?.SetLeftHanded(!gun.LeftHanded),gun!=null);
                    Add("Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),()=>{if(MatchRuntime.Current?.Authority==true)gun?.SetTestingEnabled(!gun.TestingEnabled);},gun!=null&&CanCommand&&MatchRuntime.Current?.Authority==true);
                }
                if(name=="Survivors"||name=="Giant hands")
                {
                    bool hand=name=="Giant hands";Add("Spawn one",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.AddHand:DemoCommand.AddSurvivor);},CanCommand);
                    if(!hand)Add("Fill to target population",()=>{if(CanCommand)Demo.Run(DemoCommand.FillSurvivors);},CanCommand);
                    Add("Next personality",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.NextHand:DemoCommand.NextSurvivor);},CanCommand);
                    Add("Clear population",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.ClearHands:DemoCommand.ClearSurvivors);},CanCommand);
                }
                if(name=="World")
                {
                    Add("Reset / rebuild map",()=>{if(session.CanEditWorld)session.ResetMap();},session.CanEditWorld);
                    if(Demo is IBuildUpControls build&&build.Available)Add("Deliver scaffold tier",()=>{if(build.CanAdvance)build.AdvanceStage();},build.CanAdvance);
                    Add(MultiplayerPanel.EndAction(session.Multiplayer),()=>{if(MatchRuntime.Current is IMatchReviewControls r&&r.CanEndGame)r.EndGame();},(MatchRuntime.Current as IMatchReviewControls)?.CanEndGame==true);
                }
                if(name=="Enemies")
                {
                    var enemies=session.SurvivorBots as IEnemyPopulation;
                    Add("Spawn jaguar enemy",()=>{if(CanCommand)enemies?.SpawnEnemy(new EnemySpawnSettings());},CanCommand&&enemies!=null);
                    Add("Spawn mushroom enemy",()=>{if(CanCommand)enemies?.SpawnEnemy(new EnemySpawnSettings{archetypeId="mushroom",hitDamage=10});},CanCommand&&enemies!=null);
                    Add("Spawn Longhand",()=>enemies?.SpawnEnemy(new EnemySpawnSettings{archetypeId="longhand",hitDamage=25}),CanCommand&&enemies!=null);
                    Add("Spawn Gum Brute",()=>enemies?.SpawnEnemy(new EnemySpawnSettings{archetypeId="gum-brute",hitDamage=40}),CanCommand&&enemies!=null);
                    Add("Clear enemy NPCs",()=>{if(CanCommand)enemies?.ClearEnemies();},CanCommand&&enemies!=null);
                }
                if(name=="Controls"){Add("VR movement / gestures",()=>comfortPage=true);
                    var equipment=session.Player.gameObject.GetComponent<IEquipmentControls>();
                    var hand=session.Player.gameObject.GetComponent<IBlasterControls>();
                    Add("Dominant hand: "+(hand?.LeftHanded==true?"LEFT":"RIGHT"),()=>hand?.SetLeftHanded(!hand.LeftHanded),hand!=null);
                    Add(equipment?.Equipment.drawn==true?"Holster "+equipment.Equipment.kind:"Draw "+(equipment?.Equipment.kind.ToString()??"equipment"),()=>equipment?.SetEquipped(!equipment.Equipment.drawn),equipment?.Equipment.kind>=EquipmentKind.Blaster);
                    var drop=MatchRuntime.Current as IEquipmentDropRuntime;
                    Add("Drop weapon here",()=>drop?.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&equipment?.Equipment.kind>=EquipmentKind.Blaster,CandyHudIcon.Hand);
                    Add("Jump: dominant lower button / pinch. Hold to climb.",()=>{},false);Add("Stamina / audio / scenery",()=>tuningPage=true);}
                if(name=="Ground pickups")
                {
                    int max=Mathf.Min(10,EquipmentSpecifications.All.Length-1-equipmentOffset);
                    for(int j=0;j<max;j++){var item=(EquipmentKind)(equipmentOffset+j+1);Add("Spawn "+EquipmentSpecifications.Get(item).name,()=>{if(CanCommand&&DemoMode)(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment(item);},CanCommand&&!session.PlayingHand&&MatchRuntime.Current is IEquipmentRuntime);}
                    if(equipmentOffset>0)Add("Previous items",()=>equipmentOffset=Mathf.Max(0,equipmentOffset-10));if(equipmentOffset+10<EquipmentSpecifications.All.Length-1)Add("More items",()=>equipmentOffset+=10);
                }
            }
            
            var ray=pointerRay;
            sectionGlyphsActive=true;
            help.text=batchPage?batchTarget>0?"Preview selected: "+batchTarget+" total. Choose Fill to apply.":"Choose a target to preview; it will not spawn until you apply.":menuCategory<0?"Choose a tab. Aim at a row and select.":
                menuSection<0?"Choose a section. Each row shows what it contains.":
                DemoMenuSections.Caption(DemoMenuSections.Sections[menuCategory][menuSection])+". Shared changes may require the host.";
            for(int i=0;i<buttons.Length;i++)
            {
                bool visible=i<names.Count;
                buttons[i].gameObject.SetActive(visible);targets[i].enabled=visible&&enabled[i];
                if(!visible)continue;
                labels[i].text=names[i];sectionGlyphs[i].gameObject.SetActive(true);sectionGlyphs[i].texture=CandyHudVisuals.Icon(art[i]);SetRowPreview(i,pictures[i]);
                bool hover=targets[i].enabled&&Hit(targets[i],ray);
                buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;
                labels[i].color=hover?CandyHudVisuals.Ink:enabled[i]?Color.white:CandyHudVisuals.Muted;
                if(hover&&!string.IsNullOrEmpty(captions[i]))help.text=captions[i];
                if(hover&&pressed){actions[i]();break;}
            }
        }
        void OpenMenuSection(int index)
        {
            menuSection=index;string name=DemoMenuSections.Sections[menuCategory][index];equipmentOffset=0;
            if(name=="Equipment"||name=="Bot equipment"){giveAllEquipment=name=="Bot equipment";giveEquipmentPage=true;}
            if(name=="Spectate")spectatePage=true;if(name=="Maps")mapPage=true;if(name=="Modes")modePage=true;if(name=="Disasters")disasterPage=true;
            if(name=="Settings"||name=="Voice")comfortPage=true;if(name=="Friends")networkPage=true;if(name=="Powerup spawning")randomPowerupPage=true;
        }
    }
}
