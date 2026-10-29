using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        bool randomPowerupPage;int weightOffset;
        void PointRandomPowerups(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            var runtime=MatchRuntime.Current;var controls=runtime as IPowerupSpawnControls;if(controls==null){randomPowerupPage=false;return;}
            var settings=controls.SpawnSettings;heading.text="RANDOM BLOCK POWERUPS\nAll heights · Host settings";
            float total=0;foreach(var type in settings.types)total+=type.weight;
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            for(int i=0;i<buttons.Length;i++)
            {
                int weightIndex=weightOffset+i-6;bool exists=i<6||i==13||i==14||i>=6&&i<=12&&weightIndex<settings.types.Length;buttons[i].gameObject.SetActive(exists);if(!exists)continue;
                labels[i].text=i switch{0=>"Back",1=>"Random spawns: "+(settings.enabled?"ON":"OFF"),2=>"Spawn interval: "+settings.interval.ToString("F0")+"s (cycle)",3=>"Maximum active: "+settings.maximum+" (cycle)",4=>"Pickup lifetime: "+settings.lifetime.ToString("F0")+"s (cycle)",5=>"Timing variation: "+settings.jitter.ToString("P0")+" (cycle)",13=>"Previous types",14=>"More types",_=>DeadlyJenga.Presentation.CandyEquipmentView.Name(settings.types[weightIndex].kind)+": "+(total>0?settings.types[weightIndex].weight/total:0).ToString("P0")+" (cycle weight)"};
                targets[i].enabled=i==0||runtime.Authority;bool hover=targets[i].enabled&&targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;
                if(!pressed||!hover)continue;
                if(i==0){randomPowerupPage=false;return;}
                if(i==1)settings.enabled=!settings.enabled;
                if(i==2)settings.interval=Cycle(settings.interval,new[]{2f,5,10,15,30,60,120});
                if(i==3)settings.maximum=(int)Cycle(settings.maximum,new[]{0f,2,4,8,12,16,24,32});
                if(i==4)settings.lifetime=Cycle(settings.lifetime,new[]{15f,30,60,90,180,300});
                if(i==5)settings.jitter=Cycle(settings.jitter,new[]{0f,.1f,.25f,.5f,.75f});
                if(i==13){weightOffset=Mathf.Max(0,weightOffset-7);break;}
                if(i==14){if(weightOffset+7<settings.types.Length)weightOffset+=7;break;}
                if(i>=6&&i<=12)settings.types[weightIndex].weight=Cycle(settings.types[weightIndex].weight,new[]{0f,5,10,20,40,80,100});
                controls.ConfigurePowerupSpawns(settings);break;
            }
            help.text=$"Random pickups {controls.RandomPickupCount}/{settings.maximum} · next attempt {controls.NextSpawnIn:F0}s\nZero weight disables a type. The desktop panel provides finer sliders.";
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();
        }
        static float Cycle(float value,float[] values){foreach(float next in values)if(next>value+.001f)return next;return values[0];}
    }
}
