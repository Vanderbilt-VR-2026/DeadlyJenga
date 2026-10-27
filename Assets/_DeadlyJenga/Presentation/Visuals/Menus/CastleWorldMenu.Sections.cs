using System;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        int menuCategory=-1,menuSection=-1,equipmentOffset;
        void PointOrganizedMenu(Camera camera,bool pressed)
        {
            HideDisasterSliders();scaffoldButton.gameObject.SetActive(false);
            for(int i=0;i<buttons.Length;i++){var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,277-i*50);rect.sizeDelta=new Vector2(540,46);}
            var actions=new System.Collections.Generic.List<Action>();var names=new System.Collections.Generic.List<string>();var enabled=new System.Collections.Generic.List<bool>();
            void Add(string name,Action action,bool available=true){names.Add(name);actions.Add(action);enabled.Add(available);}
            if(menuCategory<0){heading.text="DEMO CONTROLS";for(int i=0;i<4;i++){int index=i;Add(DemoMenuSections.Tabs[i],()=>{menuCategory=index;menuSection=-1;});}Add("Resume",()=>session.SetMenu(false));}
            else if(menuSection<0)
            {
                heading.text=DemoMenuSections.Tabs[menuCategory];Add("Back to main tabs",()=>menuCategory=-1);
                var sections=DemoMenuSections.Sections[menuCategory];for(int i=0;i<sections.Length;i++){int index=i;Add(sections[i],()=>OpenMenuSection(index));}
            }
            else
            {
                string name=DemoMenuSections.Sections[menuCategory][menuSection];heading.text=DemoMenuSections.Tabs[menuCategory]+" / "+name;Add("Back to categories",()=>menuSection=-1);
                if(name=="Character")
                {
                    Add(session.PlayingHand?"Become a survivor":"Become a giant hand",()=>{session.SetRole(!session.PlayingHand);session.SetMenu(false);},DemoMode);
                    Add("Respawn on top",()=>{session.SetRole(false);session.SetMenu(false);},DemoMode);
                    Add("Third person inspection",()=>CandyThirdPersonInspection.For(session).Begin(),CandyThirdPersonInspection.For(session)?.CanInspect==true);
                    var item=session.Player.gameObject.GetComponent<IEquipmentControls>();var gun=session.Player.gameObject.GetComponent<IBlasterControls>();
                    Add(item?.Equipment.drawn==true?"Holster equipment":"Draw equipment",()=>item?.SetEquipped(!item.Equipment.drawn),item?.Equipment.kind>=EquipmentKind.Blaster);
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
                    Add("End game",()=>{if(MatchRuntime.Current is IMatchReviewControls r&&r.CanEndGame)r.EndGame();},(MatchRuntime.Current as IMatchReviewControls)?.CanEndGame==true);
                }
                if(name=="Enemies")
                {
                    var enemies=session.SurvivorBots as IEnemyPopulation;
                    Add("Spawn jaguar enemy",()=>{if(CanCommand)enemies?.SpawnEnemy(new EnemySpawnSettings());},CanCommand&&enemies!=null);
                    Add("Spawn mushroom enemy",()=>{if(CanCommand)enemies?.SpawnEnemy(new EnemySpawnSettings{archetypeId="mushroom",hitDamage=10});},CanCommand&&enemies!=null);
                    Add("Clear enemy NPCs",()=>{if(CanCommand)enemies?.ClearEnemies();},CanCommand&&enemies!=null);
                }
                if(name=="Controls"){Add("VR movement / gestures",()=>comfortPage=true);Add("Stamina / audio / scenery",()=>tuningPage=true);}
                if(name=="Ground pickups")
                {
                    int max=Mathf.Min(10,EquipmentSpecifications.All.Length-1-equipmentOffset);
                    for(int j=0;j<max;j++){var item=(EquipmentKind)(equipmentOffset+j+1);Add("Spawn "+EquipmentSpecifications.Get(item).name,()=>{if(CanCommand&&DemoMode)(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment(item);},CanCommand&&!session.PlayingHand&&MatchRuntime.Current is IEquipmentRuntime);}
                    if(equipmentOffset>0)Add("Previous items",()=>equipmentOffset=Mathf.Max(0,equipmentOffset-10));if(equipmentOffset+10<EquipmentSpecifications.All.Length-1)Add("More items",()=>equipmentOffset+=10);
                }
            }
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            for(int i=0;i<buttons.Length;i++){buttons[i].gameObject.SetActive(i<names.Count);if(i>=names.Count)continue;labels[i].text=names[i];targets[i].enabled=enabled[i];bool hover=enabled[i]&&targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=Color.white;if(hover&&pressed){actions[i]();break;}}
            help.text="Choose a category, then a control. Host permissions apply to shared changes.";
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();
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
