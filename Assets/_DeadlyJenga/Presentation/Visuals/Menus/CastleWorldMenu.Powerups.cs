using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        bool powerupPage;
        void PointPowerupPage(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            heading.text="POWERUPS / ENEMIES\nHost can spawn jaguar enemies for testing";
            var enemies=session.SurvivorBots as IEnemyPopulation;bool canEnemies=enemies!=null&&MatchRuntime.Current?.Authority==true;
            var gun=session.Player.gameObject.GetComponent<IBlasterControls>();var equipment=session.Player.gameObject.GetComponent<IEquipmentControls>();
            string[] names={"Back","Spawn 2x speed + infinite stamina","Spawn 3x jump + infinite stamina","Natural disasters","Spawn jaguar enemy (20 damage)","Clear jaguar enemies","Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),equipment?.Equipment.drawn==true?"Holster weapon":"Draw weapon","Dominant hand: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),"Spawn alien blaster","Spawn murderer sword","Spawn sheriff hammer","Spawn dual rifle / shotgun","Spawn giant boxing glove","Spawn powered iron suit"};
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            bool canSpawn=DemoMode&&CanCommand&&!session.PlayingHand&&(Demo as ISpectatorControls)?.Spectating!=true;
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].gameObject.SetActive(i<names.Length);if(i>=names.Length)continue;
                targets[i].enabled=i>=9?canSpawn:i==6?Demo?.DemoMode==true&&canEnemies&&gun!=null:i==7?equipment?.Equipment.kind>=EquipmentKind.Blaster:i==8?gun!=null:i>=4?canEnemies:i==0||i==3||canSpawn;labels[i].text=names[i];
                bool hover=targets[i].enabled&&targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;
                labels[i].color=targets[i].enabled?Color.white:CandyHudVisuals.Muted;
                if(!hover||!pressed)continue;
                if(i==0)powerupPage=false;
                if(i==3){powerupPage=false;disasterPage=true;disasterSelection=-1;}
                if(i==1||i==2){Demo.Run(i==1?DemoCommand.SpawnSpeedPowerup:DemoCommand.SpawnJumpPowerup);session.SetMenu(false);}
                if(i==4){enemies.SpawnEnemy(new EnemySpawnSettings());session.SetMenu(false);}
                if(i==5)enemies.ClearEnemies();
                if(i==6)gun.SetTestingEnabled(!gun.TestingEnabled);
                if(i==7){equipment.SetEquipped(!equipment.Equipment.drawn);session.SetMenu(false);}
                if(i==8)gun.SetLeftHanded(!gun.LeftHanded);
                if(i>=9){(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment((EquipmentKind)(i-6));session.SetMenu(false);}
                break;
            }
            help.text=canSpawn?"Both powers last 20 seconds after collection.\nTimers appear in your view. Repeat pickups refresh the timer.":"Return to your own survivor in Demo to test powerups.";
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();
        }
    }
}
