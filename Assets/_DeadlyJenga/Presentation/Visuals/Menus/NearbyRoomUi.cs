using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public static class NearbyRoomUi
    {
        static MonoBehaviour provider;
        static float nextLookup;
        public static INearbyRoomControls Controls
        {
            get
            {
                if(provider)return provider as INearbyRoomControls;
                if(Time.unscaledTime<nextLookup)return null;
                nextLookup=Time.unscaledTime+1;
                provider=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b=>b is INearbyRoomControls);
                return provider as INearbyRoomControls;
            }
        }
        public static string Notice
        {
            get
            {
                var requests=Controls?.JoinRequests;
                if(requests==null||requests.Count==0)return "";
                return requests[0].PlayerName+" wants to join"+(requests.Count>1?" · +"+(requests.Count-1):"")+"\nOpen menu → Room → Nearby requests";
            }
        }
        public static void OpenProfile()
        {
            Object.FindFirstObjectByType<DeadlyJenga.CastlePrototype.CastleWorldMenu>()?.OpenNearbyProfile();
        }
        static bool expanded;
        static string draft;
        static int page;
        public static void DrawDesktop()
        {
            var c=Controls;if(c==null)return;
            if(GUILayout.Button("Nearby Bluetooth · "+c.JoinRequests.Count+" request(s)",GUILayout.Height(34)))expanded=!expanded;
            if(!expanded)return;
            GUILayout.Label(c.NearbyStatus+"\n"+c.JoinRequestStatus);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(c.NearbyEnabled?"Turn nearby off":"Enable nearby"))c.NearbyEnabled=!c.NearbyEnabled;
            if(GUILayout.Button("Find rooms"))c.RefreshNearby();
            if(GUILayout.Button("Cancel request"))c.CancelJoinRequest();
            GUILayout.EndHorizontal();
            draft=GUILayout.TextField(draft??c.PlayerName,24);
            if(GUILayout.Button("Save player name")&&!string.IsNullOrWhiteSpace(draft))c.PlayerName=draft;
            foreach(var r in c.JoinRequests.Take(3))
            {
                GUILayout.BeginHorizontal();GUILayout.Label(r.PlayerName+" wants to join · "+Mathf.CeilToInt(r.SecondsRemaining)+"s");
                if(GUILayout.Button("Approve"))c.RespondToJoin(r.Id,true);
                if(GUILayout.Button("Deny"))c.RespondToJoin(r.Id,false);GUILayout.EndHorizontal();
            }
            page=Mathf.Clamp(page,0,Mathf.Max(0,(c.NearbyRooms.Count-1)/3));
            foreach(var room in c.NearbyRooms.Skip(page*3).Take(3))
                if(GUILayout.Button("Request: "+room.HostName+"'s Room · "+room.Players+"/"+room.Capacity))c.RequestJoin(room.Id);
            GUILayout.BeginHorizontal();if(GUILayout.Button("Previous"))page=Mathf.Max(0,page-1);if(GUILayout.Button("Next"))page++;GUILayout.EndHorizontal();
        }
    }
}
