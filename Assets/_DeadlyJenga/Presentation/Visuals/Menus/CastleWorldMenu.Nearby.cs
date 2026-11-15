using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        Text bluetoothSummary,profileName;
        int bluetoothPage;
        bool showJoinRequests;
        string profileDraft="";
        readonly string[] visibleBluetoothRooms=new string[3],visibleBluetoothRequests=new string[3];
        void BuildTabletNearby(RectTransform page)
        {
            TabletText(page,"NEARBY ROOMS",new(0,224),new(920,54),40);
            bluetoothSummary=TabletText(page,"",new(0,162),new(950,74),23);
            TabletControl(tabletActions,page,()=>NearbyRoomUi.Controls?.NearbyEnabled==true?"Bluetooth: ON":"Enable Bluetooth",new(-320,91),new(300,58),()=>{var c=NearbyRoomUi.Controls;if(c!=null)c.NearbyEnabled=!c.NearbyEnabled;});
            TabletControl(tabletActions,page,()=>"Find rooms",new(0,91),new(300,58),()=>{showJoinRequests=false;bluetoothPage=0;NearbyRoomUi.Controls?.RefreshNearby();});
            TabletControl(tabletActions,page,()=>"Requests ("+(NearbyRoomUi.Controls?.JoinRequests.Count??0)+")",new(320,91),new(300,58),()=>{showJoinRequests=true;bluetoothPage=0;});
            for(int i=0;i<3;i++)
            {
                int slot=i;float y=15-i*72;
                TabletControl(tabletActions,page,()=>BluetoothRowTitle(slot),new(-116,y),new(690,62),()=>{
                    var c=NearbyRoomUi.Controls;
                    if(showJoinRequests){if(visibleBluetoothRequests[slot]!=null)c?.RespondToJoin(visibleBluetoothRequests[slot],true);}
                    else if(visibleBluetoothRooms[slot]!=null)c?.RequestJoin(visibleBluetoothRooms[slot]);
                },allowed:()=>showJoinRequests?visibleBluetoothRequests[slot]!=null:visibleBluetoothRooms[slot]!=null);
                TabletControl(tabletActions,page,()=>showJoinRequests?"Deny":"Request",new(355,y),new(210,62),()=>{
                    var c=NearbyRoomUi.Controls;if(showJoinRequests){if(visibleBluetoothRequests[slot]!=null)c?.RespondToJoin(visibleBluetoothRequests[slot],false);}
                    else if(visibleBluetoothRooms[slot]!=null)c?.RequestJoin(visibleBluetoothRooms[slot]);
                },allowed:()=>showJoinRequests?visibleBluetoothRequests[slot]!=null:visibleBluetoothRooms[slot]!=null,accent:TabletCoral);
            }
            TabletControl(tabletActions,page,()=>"Previous",new(-355,-207),new(220,58),()=>{bluetoothPage=Mathf.Max(0,bluetoothPage-1);});
            TabletControl(tabletActions,page,()=>"Next",new(-115,-207),new(220,58),()=>{bluetoothPage++;});
            TabletControl(tabletActions,page,()=>"Cancel request",new(125,-207),new(220,58),()=>NearbyRoomUi.Controls?.CancelJoinRequest());
            TabletControl(tabletActions,page,()=>"My name",new(365,-207),new(220,58),OpenNearbyProfile);
        }
        string BluetoothRowTitle(int slot)
        {
            var c=NearbyRoomUi.Controls;int index=bluetoothPage*3+slot;
            visibleBluetoothRooms[slot]=visibleBluetoothRequests[slot]=null;
            if(c==null)return "Rooms are starting…";
            if(showJoinRequests)
            {
                if(index>=c.JoinRequests.Count)return slot==0?"No pending requests":"—";
                var r=c.JoinRequests[index];visibleBluetoothRequests[slot]=r.Id;
                return "Approve "+r.PlayerName+" · "+Mathf.CeilToInt(r.SecondsRemaining)+"s";
            }
            if(index>=c.NearbyRooms.Count)return slot==0?"No rooms found · select Find rooms":"—";
            var room=c.NearbyRooms[index];visibleBluetoothRooms[slot]=room.Id;
            return room.HostName+"'s Room · "+room.Players+"/"+room.Capacity;
        }
        public void OpenNearbyProfile()
        {
            profileDraft=NearbyRoomUi.Controls?.PlayerName??"";
            session.SetMenu(true);OpenPlayerTabletPage(TabletPage.Profile);
        }
        void BuildTabletProfile(RectTransform page)
        {
            TabletText(page,"YOUR PLAYER NAME",new(0,225),new(930,56),38);
            profileName=TabletText(page,"",new(0,159),new(930,60),36);
            TabletText(page,"Saved on this headset, including APK updates. Names are not unique accounts.",new(0,103),new(950,43),22);
            const string keys="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            for(int i=0;i<keys.Length;i++){string key=keys[i].ToString();TabletControl(tabletActions,page,()=>key,new(-420+i%10*94,41-i/10*58),new(84,50),()=>{if(profileDraft.Length<24)profileDraft+=key;});}
            TabletControl(tabletActions,page,()=>"Space",new(235,-133),new(175,50),()=>{if(profileDraft.Length<24)profileDraft+=" ";});
            TabletControl(tabletActions,page,()=>"Delete",new(420,-133),new(175,50),()=>{if(profileDraft.Length>0)profileDraft=profileDraft.Substring(0,profileDraft.Length-1);});
            TabletControl(tabletActions,page,()=>"Cancel",new(-244,-213),new(456,62),()=>OpenPlayerTabletPage(TabletPage.Room));
            TabletControl(tabletActions,page,()=>"Save name",new(244,-213),new(456,62),()=>{if(NearbyRoomUi.Controls is {} c)c.PlayerName=profileDraft;OpenPlayerTabletPage(TabletPage.Room);},allowed:()=>!string.IsNullOrWhiteSpace(profileDraft));
        }
        void RefreshTabletNearby()
        {
            var c=NearbyRoomUi.Controls;
            if(tabletPage==TabletPage.Profile&&profileName)profileName.text=profileDraft+"  |";
            if(tabletPage!=TabletPage.Nearby||!bluetoothSummary)return;
            int count=showJoinRequests?c?.JoinRequests.Count??0:c?.NearbyRooms.Count??0;
            bluetoothPage=Mathf.Clamp(bluetoothPage,0,Mathf.Max(0,(count-1)/3));
            for(int i=0;i<3;i++)BluetoothRowTitle(i);
            bluetoothSummary.text=c==null?"Rooms are starting…":c.NearbyStatus+"\n"+c.JoinRequestStatus;
        }
    }
}
