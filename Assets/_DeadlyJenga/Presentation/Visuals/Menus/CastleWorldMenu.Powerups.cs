using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        bool powerupPage;
        bool giveEquipmentPage,giveAllEquipment,repeatEquipment;
        void PointGiveEquipmentPage(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            heading.text="GIVE EQUIPMENT\n"+(giveAllEquipment?"All survivor players + bots":"Myself");
            var runtime=MatchRuntime.Current;var controls=runtime as IDemoEquipmentControls;
            var everyone=runtime as IDemoLoadoutControls;bool host=DemoMode&&CanCommand&&runtime?.Authority==true;
            bool can=host&&(giveAllEquipment?everyone!=null:!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true&&controls!=null);
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            for(int i=0;i<buttons.Length;i++)
            {
                int item=equipmentOffset+i;bool entry=i>=1&&i<=8&&item<EquipmentSpecifications.All.Length;
                buttons[i].gameObject.SetActive(entry||i==0||i>=9);targets[i].enabled=i==0||entry&&can||i==9&&equipmentOffset>0||i==10&&equipmentOffset+8<EquipmentSpecifications.All.Length-1||i==11&&everyone!=null||i==12&&giveAllEquipment||i==13&&host&&everyone!=null&&everyone.RespawnEquipment!=EquipmentKind.None||i==14;
                labels[i].text=i==0?"Back":i==9?"Previous items":i==10?"More items":i==11?"Target: "+(giveAllEquipment?"ALL PLAYERS + BOTS":"MYSELF"):i==12?repeatEquipment?"NOW + EVERY RESPAWN":"THIS LIFE ONLY":i==13?"Stop respawn grants":i==14?"Random spawn settings":entry?"Give "+EquipmentSpecifications.All[item].name:"";
                bool hover=targets[i].enabled&&targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=targets[i].enabled?Color.white:CandyHudVisuals.Muted;
                if(!hover||!pressed)continue;
                if(i==0){giveEquipmentPage=false;menuSection=-1;}
                else if(i==9)equipmentOffset=Mathf.Max(0,equipmentOffset-8);
                else if(i==10)equipmentOffset+=8;
                else if(i==11)giveAllEquipment=!giveAllEquipment;
                else if(i==12)repeatEquipment=!repeatEquipment;
                else if(i==13)everyone.GiveEveryoneEquipment(EquipmentKind.None,false);
                else if(i==14){giveEquipmentPage=false;randomPowerupPage=true;}
                else if(entry&&(giveAllEquipment?everyone.GiveEveryoneEquipment((EquipmentKind)item,repeatEquipment):controls.GiveEquipment((EquipmentKind)item)))session.SetMenu(false);
                break;
            }
            help.text=giveAllEquipment?"Choose target and duration below. Bulk grants replace inventory directly.\nActive respawn item: "+DeadlyJenga.Presentation.CandyEquipmentView.Name(everyone?.RespawnEquipment??EquipmentKind.None):"One item at a time. Replaced items drop at your feet.\nTimed powers last 20 seconds. Host only.";
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();
        }
        void PointPowerupPage(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            heading.text="POWERUPS / ENEMIES\nHost can spawn jaguar enemies for testing";
            var enemies=session.SurvivorBots as IEnemyPopulation;bool canEnemies=enemies!=null&&MatchRuntime.Current?.Authority==true;
            var gun=session.Player.gameObject.GetComponent<IBlasterControls>();var equipment=session.Player.gameObject.GetComponent<IEquipmentControls>();
            string[] names={"Back","Spawn 2x speed + infinite stamina","Spawn 3x jump + infinite stamina","Give myself a weapon / powerup","Spawn jaguar enemy (20 damage)","Clear jaguar enemies","Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),equipment?.Equipment.drawn==true?"Holster weapon":"Draw weapon","Dominant hand: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),"Spawn alien blaster","Spawn murderer sword","Spawn sheriff revolver","Spawn dual rifle / shotgun","Spawn giant boxing glove","Spawn powered iron suit"};
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            bool canSpawn=DemoMode&&CanCommand&&!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true;
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].gameObject.SetActive(i<names.Length);if(i>=names.Length)continue;
                targets[i].enabled=i>=9?canSpawn:i==6?DemoMode&&canEnemies&&gun!=null:i==7?equipment?.Equipment.kind>=EquipmentKind.Blaster:i==8?gun!=null:i>=4?canEnemies:i==0||i==3||canSpawn;labels[i].text=names[i];
                bool hover=targets[i].enabled&&targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;
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
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();
        }
    }
}
