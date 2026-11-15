using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed partial class HomeMenuView
    {
        GameObject bluetoothPage;
        Text bluetoothStatus;
        readonly Text[] bluetoothLabels=new Text[4];
        readonly string[] bluetoothIds=new string[4];
        int bluetoothPageIndex;
        void BuildBluetooth()
        {
            bluetoothPage=Node("Bluetooth rooms",content,Vector2.zero,new Vector2(640,680)).gameObject;
            var root=bluetoothPage.transform;
            ButtonAt(root,"BACK",new(-215,225),new(150,45),()=>ShowPage(joinPage));
            ButtonAt(root,"FIND ROOMS",new(110,225),new(350,45),()=>{bluetoothPageIndex=0;NearbyRoomUi.Controls?.RefreshNearby();});
            bluetoothStatus=Label(root,"",new(0,130),new(590,120),23,Color.white);
            for(int i=0;i<4;i++){int slot=i;bluetoothLabels[i]=ButtonAt(root,"",new(0,35-i*62),new(590,54),()=>{if(bluetoothIds[slot]!=null)NearbyRoomUi.Controls?.RequestJoin(bluetoothIds[slot]);}).GetComponentInChildren<Text>();bluetoothLabels[i].supportRichText=false;}
            ButtonAt(root,"PREVIOUS",new(-153,-235),new(280,48),()=>bluetoothPageIndex=Mathf.Max(0,bluetoothPageIndex-1));
            ButtonAt(root,"NEXT",new(153,-235),new(280,48),()=>bluetoothPageIndex++);
            ButtonAt(root,"CANCEL REQUEST",new(0,-300),new(590,48),()=>NearbyRoomUi.Controls?.CancelJoinRequest());
        }
        void ShowBluetooth(){ShowPage(bluetoothPage);NearbyRoomUi.Controls?.RefreshNearby();}
        void RefreshBluetooth()
        {
            if(!bluetoothPage||!bluetoothPage.activeSelf)return;
            var c=NearbyRoomUi.Controls;int count=c?.NearbyRooms.Count??0;
            bluetoothPageIndex=Mathf.Clamp(bluetoothPageIndex,0,Mathf.Max(0,(count-1)/4));
            bluetoothStatus.text=c==null?"Nearby rooms are starting…":c.NearbyStatus+"\n"+c.JoinRequestStatus;
            for(int i=0;i<4;i++)
            {
                int index=bluetoothPageIndex*4+i;bluetoothIds[i]=null;
                if(index>=count){bluetoothLabels[i].text=i==0?"No rooms yet. Enable Bluetooth on both headsets.":"—";continue;}
                var room=c.NearbyRooms[index];bluetoothIds[i]=room.Id;
                bluetoothLabels[i].text=room.HostName+"'s Room · "+room.Players+"/"+room.Capacity+" · Request";
            }
        }
    }
}
