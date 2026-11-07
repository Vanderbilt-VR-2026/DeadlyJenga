using System;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        int menuCategory=-1,menuSection=-1,equipmentOffset;
        bool batchPage,quickGunPage,hostTools,wildlifePage,tameWildlife;
        int batchTarget;
        UnityEngine.UI.Button backButton;
        BoxCollider backTarget;
        Texture2D disasterPreview;
        bool guestPauseActive;
        bool AtHome=>!batchPage&&!mapPage&&!spectatePage&&!giveEquipmentPage&&!randomPowerupPage&&!powerupPage&&
            !tabletopPage&&!hudPage&&!modePage&&!disasterPage&&!networkPage&&!comfortPage&&!tuningPage&&menuCategory<0;

        void BuildNavigation()
        {
            backButton=HeaderButton("Back",0,out backTarget);
            ((RectTransform)backButton.transform).anchoredPosition=new Vector2(-206,510);
        }
        bool PointNavigation(bool pressed)
        {
            ((RectTransform)backButton.transform).anchoredPosition=new Vector2(-206,510);
            bool available=!DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session);
            backButton.gameObject.SetActive(available);
            if(!available)return false;
            backButton.GetComponentInChildren<UnityEngine.UI.Text>().text=AtHome&&!hostTools?"Resume":"Back";
            bool back=Hit(backTarget,pointerRay);
            backButton.image.color=back?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(!pressed)return false;
            if(!back)return false;
            if(AtHome){if(hostTools)hostTools=false;else session.SetMenu(false);return true;}
            if(wildlifePage){wildlifePage=false;return true;}
            if(tuningPage&&handPhysicsPage){handPhysicsPage=false;return true;}
            if(disasterPage&&disasterSelection>=0){disasterSelection=-1;return true;}
            if(batchPage){if(batchTarget>0)batchTarget=0;else batchPage=false;return true;}
            bool child=networkPage||comfortPage||tuningPage||modePage||hudPage||mapPage||spectatePage||
                tabletopPage||disasterPage||giveEquipmentPage||randomPowerupPage||powerupPage||batchPage;
            ResetPages();
            if(child||menuSection>=0)menuSection=-1;
            else menuCategory=-1;
            return true;
        }
        void PointGuestPause(bool pressed)
        {
            if(!guestPauseActive)
            {
                ResetPages();hostTools=false;menuCategory=menuSection=-1;
                guestPauseActive=true;
            }
            heading.text="Pause";
            help.gameObject.SetActive(false);
            joinCodeLabel.gameObject.SetActive(false);
            exitButton.gameObject.SetActive(false);
            HideDisasterSliders();
            var room=session.Multiplayer;
            bool showAppearance=session.PlayingHand&&HandLooks.Names.Length>0;
            bool canExit=room?.CanLeave==true;
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].gameObject.SetActive(i==0||i==1&&showAppearance||i==2&&canExit);
                mapThumbnails[i].gameObject.SetActive(false);
            }

            backButton.gameObject.SetActive(true);
            ((RectTransform)backButton.transform).anchoredPosition=new Vector2(-206,246);
            backButton.GetComponentInChildren<UnityEngine.UI.Text>().text="Back";
            bool back=Hit(backTarget,pointerRay);
            backButton.image.color=back?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            if(pressed&&back){roomLeave.Clear();session.SetMenu(false);return;}

            if(Row(0,70,"Controls & Comfort",CandyHudIcon.Settings,CandyHudVisuals.Card))
            {roomLeave.Clear();DeadlyJenga.Presentation.CandyComfortPanel.Open(session);return;}
            if(showAppearance&&Row(1,0,"Hand appearance  ·  "+HandLooks.ChosenName,CandyHudIcon.Hand,CandyHudVisuals.Card))
            {HandLooks.Chosen++;return;}

            if(!canExit)return;
            string code=MultiplayerPanel.JoinCode(room);
            float now=Time.unscaledTime;
            bool confirming=roomLeave.Pending(room,code,now);
            if(Row(2,-212,confirming?"Confirm exit to lobby":"Exit to Lobby",CandyHudIcon.Home,CandyHudVisuals.Peach)&&
                roomLeave.Select(room,code,true,now))
                DeadlyJenga.Presentation.MenuAccess.ExitGuestMatch(session);

            bool Row(int index,float y,string title,CandyHudIcon icon,Color color)
            {
                var button=buttons[index];
                var rect=(RectTransform)button.transform;
                rect.anchoredPosition=new Vector2(0,y);
                rect.sizeDelta=new Vector2(540,54);
                labels[index].text=title;
                labels[index].rectTransform.sizeDelta=new Vector2(472,46);
                labels[index].rectTransform.anchoredPosition=new Vector2(20,0);
                sectionGlyphs[index].texture=CandyHudVisuals.Icon(icon);
                sectionGlyphs[index].gameObject.SetActive(true);
                targets[index].enabled=true;
                bool hover=Hit(targets[index],pointerRay);
                button.image.color=hover?CandyHudVisuals.Mint:color;
                return pressed&&hover;
            }
        }
        void ResetPages()
        {
            roomLeave.Clear();
            networkPage=comfortPage=tuningPage=modePage=hudPage=mapPage=spectatePage=
                tabletopPage=disasterPage=giveEquipmentPage=randomPowerupPage=powerupPage=batchPage=quickGunPage=false;
            equipmentOffset=disasterOffset=mapOffset=batchTarget=0;disasterSelection=-1;
            wildlifePage=false;
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
            HideDisasterSliders();
            for(int i=0;i<buttons.Length;i++){var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,48);}
            var actions=new System.Collections.Generic.List<Action>();var names=new System.Collections.Generic.List<string>();var enabled=new System.Collections.Generic.List<bool>();var art=new System.Collections.Generic.List<CandyHudIcon>();var captions=new System.Collections.Generic.List<string>();var pictures=new System.Collections.Generic.List<Texture>();
            void Add(string name,Action action,bool available=true,CandyHudIcon icon=CandyHudIcon.Key,string caption="",Texture picture=null)
            {names.Add(name);actions.Add(action);enabled.Add(available);art.Add(icon);captions.Add(caption);pictures.Add(picture);}
            var match=MatchRuntime.Current;
            var bots=session.SurvivorBots;
            int humans=Mathf.Max(1,session.Multiplayer?.HumanCount??1);
            bool canBatch=CanCommand&&match?.Authority==true&&bots!=null;
            var wildlife=(session as Component)?.GetComponent<IWildlifeControls>();
            if(wildlifePage)
            {
                heading.text="ISLAND WILDLIFE";
                bool available=wildlife?.Available==true;
                Add(tameWildlife?"Companion":"Wild roaming",()=>tameWildlife=!tameWildlife,available,CandyHudIcon.Survivor);
                Add("Spawn all",()=>{foreach(var species in wildlife.Species)wildlife.Spawn(species,tameWildlife);},available,CandyHudIcon.Play);
                Add("Clear wildlife",()=>wildlife.Clear(),available,CandyHudIcon.Home);
                if(wildlife!=null)
                    foreach(string species in wildlife.Species)
                        Add("Spawn "+species,()=>wildlife.Spawn(species,tameWildlife),available,CandyHudIcon.Survivor);
            }
            else if(batchPage)
            {
                heading.text="BOT POPULATION";
                Add("Current: "+(bots?.Count??0)+" bots + "+humans+" human",()=>{},false,CandyHudIcon.Friends);
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
            }
            else if(menuCategory<0)
            {
                heading.text=hostTools?"HOST TOOLS":"PAUSE MENU";
                if(hostTools)
                {
                    for(int i=0;i<3;i++){int index=i;Add(DemoMenuSections.Tabs[i],()=>{menuCategory=index;menuSection=-1;},icon:DemoMenuSections.Icon(DemoMenuSections.Tabs[i]),caption:DemoMenuSections.TabDescriptions[i]);}
                    if(Settings!=null)Add("Match tuning",()=>tuningPage=true,icon:CandyHudIcon.Settings);
                }
                else
                {
                    Add("Controls & Comfort",()=>DeadlyJenga.Presentation.CandyComfortPanel.Open(session),icon:CandyHudIcon.Settings);
                    if(session.PlayingHand&&HandLooks.Names.Length>0)
                        Add("Hand appearance  ·  "+HandLooks.ChosenName,()=>HandLooks.Chosen++,icon:CandyHudIcon.Hand);
                    if(session.Multiplayer!=null)Add("Room",()=>networkPage=true,icon:CandyHudIcon.Friends);
                    if(session.Social!=null)Add("Audio & voice",()=>comfortPage=true,icon:CandyHudIcon.Sound);
                    var drop=match as IEquipmentDropRuntime;var carried=session.Player?.gameObject.GetComponent<IEquipmentControls>();
                    if(drop!=null&&!session.PlayingHand&&carried?.Equipment.kind>=EquipmentKind.Blaster)
                        Add("Drop weapon",()=>drop.RequestDropEquipment(),icon:CandyHudIcon.Hand);
                    if(DeadlyJenga.Presentation.MenuAccess.HostTools(session))Add("Host tools",()=>hostTools=true,icon:CandyHudIcon.Settings,caption:"Bots, equipment, map changes and development settings.");
                }
            }
            else if(menuSection<0)
            {
                heading.text=DemoMenuSections.Tabs[menuCategory];
                var sections=DemoMenuSections.Sections[menuCategory];
                for(int i=0;i<sections.Length;i++)
                {
                    int index=i;string section=sections[i];
                    if(section=="Controls"||section=="Voice"||section=="Friends")continue;
                    if(section=="Maps"&&(Demo is not IDemoMapControls maps||!maps.CanSelectMap||!DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session)))continue;
                    if(section=="Spectate"&&(Demo is not ISpectatorControls spectate||!spectate.CanSpectate&&!spectate.Spectating))continue;
                    Add(section,()=>OpenMenuSection(index),icon:DemoMenuSections.Icon(section),caption:DemoMenuSections.Caption(section));
                }
            }
            else
            {
                string name=DemoMenuSections.Sections[menuCategory][menuSection];heading.text=DemoMenuSections.Tabs[menuCategory]+" / "+name;
                if(name=="Character")
                {
                    Add(session.PlayingHand?"Become Survivor":"Become Flying Hand",SwitchDemoRole,DemoMode,CandyHudIcon.Hand,"Switch your local demo character.");
                    Add("Respawn on top",()=>{session.SetRole(false);session.SetMenu(false);},DemoMode);
                    Add("Inspect character",()=>CandyThirdPersonInspection.For(session).Begin(),CandyThirdPersonInspection.For(session)?.CanInspect==true);
                    var item=session.Player.gameObject.GetComponent<IEquipmentControls>();var gun=session.Player.gameObject.GetComponent<IBlasterControls>();
                    Add(item?.Equipment.drawn==true?"Holster "+item.Equipment.kind:"Draw "+(item?.Equipment.kind.ToString()??"equipment"),()=>item?.SetEquipped(!item.Equipment.drawn),item?.Equipment.kind>=EquipmentKind.Blaster);
                    Add("Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),()=>{if(MatchRuntime.Current?.Authority==true)gun?.SetTestingEnabled(!gun.TestingEnabled);},gun!=null&&CanCommand&&MatchRuntime.Current?.Authority==true);
                }
                if(name=="Survivors"||name=="Giant hands")
                {
                    bool hand=name=="Giant hands";Add("Spawn one",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.AddHand:DemoCommand.AddSurvivor);},CanCommand);
                    if(!hand)
                    {
                        Add("Fill to target population",()=>{if(CanCommand)Demo.Run(DemoCommand.FillSurvivors);},CanCommand);
                        if(canBatch)Add("Bot population",()=>{batchPage=true;batchTarget=0;},icon:CandyHudIcon.Friends);
                    }
                    Add("Next personality",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.NextHand:DemoCommand.NextSurvivor);},CanCommand);
                    Add("Clear population",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.ClearHands:DemoCommand.ClearSurvivors);},CanCommand);
                }
                if(name=="World")
                {
                    if(wildlife!=null)Add("Island wildlife",()=>wildlifePage=true,icon:CandyHudIcon.Survivor);
                    Add("Rebuild map",()=>{if(DeadlyJenga.Presentation.MenuAccess.HostTools(session))session.ResetMap();},DeadlyJenga.Presentation.MenuAccess.HostTools(session));
                    if(Demo is IBuildUpControls build&&build.Available)Add("Deliver scaffold tier",()=>{if(build.CanAdvance)build.AdvanceStage();},build.CanAdvance);
                    if(match is IMatchReviewControls review&&(review.DevelopmentReviewEnabled||review.CanEndGame))
                        Add(MultiplayerPanel.EndAction(session.Multiplayer),()=>{if(review.CanEndGame)review.EndGame();},review.CanEndGame,CandyHudIcon.Play);
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
                if(name=="Ground pickups")
                {
                    int max=Mathf.Min(10,EquipmentSpecifications.All.Length-1-equipmentOffset);
                    for(int j=0;j<max;j++){var item=(EquipmentKind)(equipmentOffset+j+1);Add("Spawn "+EquipmentSpecifications.Get(item).name,()=>{if(CanCommand&&DemoMode)(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment(item);},CanCommand&&!session.PlayingHand&&MatchRuntime.Current is IEquipmentRuntime);}
                    if(equipmentOffset>0)Add("Previous items",()=>equipmentOffset=Mathf.Max(0,equipmentOffset-10));if(equipmentOffset+10<EquipmentSpecifications.All.Length-1)Add("More items",()=>equipmentOffset+=10);
                }
            }
            
            var ray=pointerRay;
            sectionGlyphsActive=true;
            help.text=wildlifePage?wildlife?.Status??"":batchPage?batchTarget>0?"Preview selected: "+batchTarget+" total. Choose Fill to apply.":"Choose a target to preview; it will not spawn until you apply.":menuCategory<0?(hostTools?"Choose a Host Tools section.":"Point a controller laser at an action and press its trigger."):
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
            if(name=="Powerup spawning")randomPowerupPage=true;
        }
    }
}
