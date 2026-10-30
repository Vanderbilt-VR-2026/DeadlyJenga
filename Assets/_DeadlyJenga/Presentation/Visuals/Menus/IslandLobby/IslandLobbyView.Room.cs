using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        ILobbyDetails Details=>lobby as ILobbyDetails;
        Text roomCount,requestLabel;
        int requestSelection;
        string RequestId=>Details!=null&&Details.HostRequests.Count>0?Details.HostRequests[requestSelection%Details.HostRequests.Count]:null;
        void BuildRoomStation()
        {
            var l=lobby;hostStation=Terminal("HOST",new Vector3(0,StationY,StationZ),0,new Vector2(800,900),CandyHudVisuals.Mint);
            roomCount=Label(hostStation,"0 CONNECTED",300,32,800,40);roomCount.fontStyle=FontStyle.Bold;
            hostCode=Label(hostStation,"",245,36,720,60);
            Add(hostStation,"host",()=>"CREATE JOIN CODE",150,l.Host,()=>l.CanHost,0,700,105,()=>!l.Connected);
            Add(hostStation,"region",()=>"REGION: "+l.Region.ToUpperInvariant()+"  ·  CHANGE",65,l.NextRegion,()=>l.CanHost,0,650,50,()=>!l.Connected);
            Add(hostStation,"invite-all",()=>"INVITE NEARBY",-25,l.InviteNearby,()=>l.CanInvite,0,700,100);
            Add(hostStation,"request-host",()=>"REQUEST HOST",-125,()=>Details?.RequestHost(),()=>l.Connected&&!l.IsHost&&!l.Busy,0,700,65,()=>l.Connected&&!l.IsHost);
            Add(hostStation,"shared-controls",()=>"DEMO CONTROLS: "+(l.SharedControls?"EVERYONE":"HOST ONLY"),-125,l.ToggleSharedControls,()=>l.CanConfigure&&l.DemoSelected,0,700,65,()=>l.IsHost&&l.Connected);
            Add(hostStation,"abandon-host",()=>l.Busy?"CANCEL CONNECTION":l.IsHost?"LEAVE / CLOSE ROOM":"LEAVE ISLAND",-220,l.Leave,()=>l.CanLeave,0,700,65,()=>l.CanLeave);
            requestLabel=Label(hostStation,"",-292,22,700,48);
            Add(hostStation,"request-next",()=>"NEXT REQUEST",-292,()=>requestSelection++,()=>RequestId!=null,250,180,45,()=>l.IsHost&&(Details?.HostRequests.Count??0)>1);
            Add(hostStation,"request-accept",()=>"ACCEPT",-345,()=>Details?.ReviewHostRequest(RequestId,true),()=>RequestId!=null, -180,320,55,()=>l.IsHost&&RequestId!=null);
            Add(hostStation,"request-deny",()=>"DENY",-345,()=>Details?.ReviewHostRequest(RequestId,false),()=>RequestId!=null,180,320,55,()=>l.IsHost&&RequestId!=null);
            hostHint=Label(hostStation,"",-405,19,720,56);hostHint.color=CandyHudVisuals.Muted;
        }
        void UpdateRoomStation()
        {
            roomCount.text=lobby.Busy?"CONNECTING":lobby.Connected?(Details?.ConnectedCount??1)+" CONNECTED":"PRIVATE ISLAND";
            if(lobby.IsHost&&RequestId!=null){var who=Details.Participants.FirstOrDefault(p=>p.id==RequestId);requestLabel.text=(who?.displayName??"A player")+" requests host";}else requestLabel.text="";
        }
    }
}
