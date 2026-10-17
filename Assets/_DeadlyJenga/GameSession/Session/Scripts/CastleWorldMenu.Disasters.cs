using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        int disasterSelection=-1;
        Slider[] disasterSliders;
        Text[] disasterCaptions;
        BoxCollider[] disasterHits;
        bool syncingDisasterSliders;
        void EnsureDisasterSliders()
        {
            if(disasterSliders!=null)return;
            disasterSliders=new Slider[8];disasterCaptions=new Text[8];disasterHits=new BoxCollider[8];
            for(int i=0;i<8;i++)
            {
                int slot=i;
                var slider=CreateDisasterSlider("Disaster setting",out disasterCaptions[i],out disasterHits[i]);
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(value=>
                {
                    if(syncingDisasterSliders)return;
                    var prefabs=DemoDisasters.Prefabs;if(disasterSelection<0||disasterSelection>=prefabs.Length)return;
                    var prefab=prefabs[disasterSelection];DemoDisasters.SetValue(session,prefab,slot,value);UpdateCaption(slot,prefab);
                });disasterSliders[i]=slider;
            }
        }
        void HideDisasterSliders(){if(disasterSliders!=null)foreach(var slider in disasterSliders)slider.gameObject.SetActive(false);}
        void UpdateCaption(int index,GameObject prefab)
        {var p=DemoDisasters.Parameters(prefab)[index];disasterCaptions[index].text=p.label+" · "+DemoDisasters.Values(session,prefab)[index].ToString(p.whole?"F0":"F1")+" "+p.unit;}
        void PointDisasterPage(Camera camera,bool pressed,bool held)
        {
            EnsureDisasterSliders();HideDisasterSliders();
            var prefabs=DemoDisasters.Prefabs;bool allowed=DemoDisasters.CanTrigger(session);
            foreach(var button in buttons)button.gameObject.SetActive(false);
            void ButtonAt(int i,string text,float y,bool enabled=true)
            {buttons[i].gameObject.SetActive(true);labels[i].text=text;targets[i].enabled=enabled;var r=(RectTransform)buttons[i].transform;r.anchoredPosition=new Vector2(0,y);r.sizeDelta=new Vector2(540,46);}
            ButtonAt(0,"Resume",277);
            if(disasterSelection>=prefabs.Length)disasterSelection=-1;
            if(disasterSelection<0)
            {
                heading.text="NATURAL DISASTERS\nChoose one to tune and launch";
                for(int i=0;i<9&&disasterOffset+i<prefabs.Length;i++)ButtonAt(i+1,DemoDisasters.Label(prefabs[disasterOffset+i])+" · settings",220-i*53);
                ButtonAt(10,"Stop all disasters",-310,allowed);ButtonAt(12,"Back to demo controls",-370);
                if(disasterOffset>0)ButtonAt(13,"Previous disasters",-420);
                else if(disasterOffset+9<prefabs.Length)ButtonAt(14,"More disasters",-420);
            }
            else
            {
                var prefab=prefabs[disasterSelection];var definitions=DemoDisasters.Parameters(prefab);var values=DemoDisasters.Values(session,prefab);
                heading.text=DemoDisasters.Label(prefab).ToUpperInvariant()+"\nSettings apply to the next launch";
                ButtonAt(1,prefab.GetComponent<IDemoHazard>()?.Toggle==true?"Toggle on / off":"Launch disaster",220,allowed);
                for(int i=0;i<definitions.Count&&i<8;i++)
                {
                    var slider=disasterSliders[i];var p=definitions[i];syncingDisasterSliders=true;slider.minValue=p.min;slider.maxValue=p.max;slider.wholeNumbers=p.whole;slider.SetValueWithoutNotify(values[i]);syncingDisasterSliders=false;
                    slider.interactable=allowed;slider.gameObject.SetActive(true);((RectTransform)slider.transform).anchoredPosition=new Vector2(0,158-i*60);UpdateCaption(i,prefab);
                }
                ButtonAt(10,"Stop all disasters",-355,allowed);ButtonAt(12,"Back to disasters",-415);
            }
            transform.position=camera.transform.position+openingForward*1.6f;Physics.SyncTransforms();
            var ray=new Ray(camera.transform.position,camera.transform.forward);
            for(int i=0;i<disasterSliders.Length;i++)if(PointWaveSlider(ray,disasterSliders[i],disasterHits[i],pressed||held)){help.text="Look along slider · hold RIGHT trigger / pinch";return;}
            help.text=DemoDisasters.Status.Length>0?DemoDisasters.Status:!string.IsNullOrEmpty(disasterStatus)?disasterStatus:allowed?"RIGHT trigger / pinch selects · Stop all clears temporary effects":"The host controls disasters unless shared controls are enabled.";
            for(int i=0;i<buttons.Length;i++)
            {
                if(!buttons[i].gameObject.activeSelf||!targets[i].enabled)continue;
                bool hover=targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:Color.white;
                if(!hover||!pressed)continue;
                if(i==0)session.SetMenu(false);
                else if(i==10)CastleDemoCommands.Run(session,CastleDemoAction.StopDisasters);
                else if(i==12){if(disasterSelection>=0)disasterSelection=-1;else disasterPage=false;}
                else if(i==13)disasterOffset=Mathf.Max(0,disasterOffset-9);
                else if(i==14)disasterOffset+=9;
                else if(disasterSelection>=0&&i==1)disasterStatus=DemoDisasters.Request(session,prefabs[disasterSelection]);
                else if(i>=1&&i<=9){disasterSelection=disasterOffset+i-1;disasterStatus="";}
                break;
            }
        }
    }
}
