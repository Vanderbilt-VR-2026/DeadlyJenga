using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        bool randomPowerupPage;int weightOffset;
        bool developerPowerupWeights;
        int developerPowerupChoice=-1,developerWeightChoice;
        void PointRandomPowerups(Camera camera,bool pressed)
        {
            scaffoldButton.gameObject.SetActive(false);HideDisasterSliders();
            var runtime=MatchRuntime.Current;var controls=runtime as IPowerupSpawnControls;
            if(controls==null){randomPowerupPage=false;return;}
            var settings=controls.SpawnSettings;
            var names=new System.Collections.Generic.List<string>();var actions=new System.Collections.Generic.List<System.Action>();
            var enabled=new System.Collections.Generic.List<bool>();var art=new System.Collections.Generic.List<CandyHudIcon>();
            var captions=new System.Collections.Generic.List<string>();var pictures=new System.Collections.Generic.List<Texture>();
            void Add(string label,System.Action action,bool available=true)
            {names.Add(label);actions.Add(action);enabled.Add(available);art.Add(CandyHudIcon.Settings);captions.Add("");pictures.Add(null);}
            heading.text="WORLD & PICKUPS / AUTOMATIC POWERUPS";
            if(developerPowerupChoice>=0)
            {
                Add("Close value choices",()=>developerPowerupChoice=-1);
                float[] values=developerPowerupChoice switch{0=>new[]{2f,5,10,15,30,60,120},1=>new[]{0f,2,4,8,12,16,24,32},2=>new[]{15f,30,60,90,180,300},3=>new[]{0f,.1f,.25f,.5f,.75f},_=>new[]{0f,5,10,20,40,80,100}};
                foreach(float option in values)
                {
                    float value=option;
                    Add(developerPowerupChoice==3?value.ToString("P0"):value.ToString("0.##"),()=>{
                        switch(developerPowerupChoice){case 0:settings.interval=value;break;case 1:settings.maximum=(int)value;break;case 2:settings.lifetime=value;break;case 3:settings.jitter=value;break;default:settings.types[developerWeightChoice].weight=value;break;}
                        controls.ConfigurePowerupSpawns(settings);developerPowerupChoice=-1;
                    },runtime.Authority);
                }
            }
            else
            {
                Add("Random spawns: "+(settings.enabled?"ON":"OFF"),()=>{settings.enabled=!settings.enabled;controls.ConfigurePowerupSpawns(settings);},runtime.Authority);
                Add("Interval: "+settings.interval.ToString("0")+" seconds  v",()=>developerPowerupChoice=0,runtime.Authority);
                Add("Maximum active: "+settings.maximum+"  v",()=>developerPowerupChoice=1,runtime.Authority);
                Add("Pickup lifetime: "+settings.lifetime.ToString("0")+" seconds  v",()=>developerPowerupChoice=2,runtime.Authority);
                Add("Timing variation: "+settings.jitter.ToString("P0")+"  v",()=>developerPowerupChoice=3,runtime.Authority);
                Add((developerPowerupWeights?"Hide":"Show")+" item weights",()=>developerPowerupWeights=!developerPowerupWeights);
                if(developerPowerupWeights)
                {
                    float total=0;foreach(var type in settings.types)total+=type.weight;
                    for(int i=weightOffset;i<Mathf.Min(settings.types.Length,weightOffset+7);i++)
                    {int index=i;var type=settings.types[i];Add(DeadlyJenga.Presentation.CandyEquipmentView.Name(type.kind)+": "+(total>0?type.weight/total:0).ToString("P0")+"  v",()=>{developerWeightChoice=index;developerPowerupChoice=4;},runtime.Authority);}
                    if(weightOffset>0)Add("Previous items",()=>weightOffset=Mathf.Max(0,weightOffset-7));
                    if(weightOffset+7<settings.types.Length)Add("Next items",()=>weightOffset+=7);
                }
            }
            help.text=$"Active pickups: {controls.RandomPickupCount}/{settings.maximum}. Zero weight disables an item.";
            RenderOrganizedMenuGrid(names,actions,enabled,art,captions,pictures,pressed);
        }
        static float Cycle(float value,float[] values){foreach(float next in values)if(next>value+.001f)return next;return values[0];}
    }
}
