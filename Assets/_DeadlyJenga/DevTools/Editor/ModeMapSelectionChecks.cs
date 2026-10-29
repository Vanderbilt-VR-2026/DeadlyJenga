using System;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.DevTools.Editor
{
    public static class ModeMapSelectionChecks
    {
        public static void Run(Action<bool,string> check)
        {
            var lobby=IslandLobby.Current;var room=lobby.Room;var catalog=room.Catalog;var details=(ILobbyDetails)lobby;
            var originalMaps=catalog.maps;int map=room.MapIndex,mode=room.ModeIndex;var setup=details.Setup;
            var masks=originalMaps.Select(m=>m.supportedModes).ToArray();
            try
            {
                check(originalMaps.Length>1,"Map compatibility fixture has multiple real catalog maps");
                foreach(var option in details.Modes.Where(m=>m.available))
                {
                    check(lobby.SelectMode(option.id)&&details.Maps.Count==originalMaps.Length,"All maps remain available for "+option.title+" while compatibility masks are empty");
                }
                lobby.SelectMode("ctf");var recommended=originalMaps.FirstOrDefault(m=>m.recommendedMode=="survival")??originalMaps[0];
                check(lobby.SelectMap(recommended.id)&&details.SelectedModeId=="ctf","Choosing a map preserves the already selected game mode");
                for(int i=0;i<originalMaps.Length;i++)originalMaps[i].supportedModes=new[]{i==0?"ctf":"hill"};
                check(lobby.SelectMode("ctf")&&room.MapIndex==0&&details.Maps.Count==1,"A restricted mode selects its compatible map and filters the map list");
                check(!lobby.SelectMap(originalMaps[1].id)&&room.MapIndex==0,"Presentation command cannot select an incompatible map");
                room.SelectMap(1);check(room.MapIndex==0,"Direct session command also rejects an incompatible map");
                var apply=typeof(IslandRoomConnection).GetMethod("ApplyLobbyAction",BindingFlags.Instance|BindingFlags.NonPublic);
                apply.Invoke(room,new object[]{room.LocalLobbyId,new IslandRoomConnection.LobbyAction{verb="map",index=1}});
                check(room.MapIndex==0,"Authority handler rejects a forged incompatible map action");
                check(lobby.SelectMode("hill")&&room.MapIndex==1&&details.Maps.Count==originalMaps.Length-1,"Changing modes replaces an incompatible previous map with the first allowed map");
                check(!lobby.SelectMode("mystery")&&details.SelectedModeId=="hill","A mode with no compatible maps cannot leave the lobby in an invalid setup");
                string version=catalog.NetworkVersion;originalMaps[0].supportedModes=new[]{"ctf","hill"};
                check(catalog.NetworkVersion!=version,"Compatibility metadata participates in the lobby network version");
            }
            finally
            {
                for(int i=0;i<originalMaps.Length;i++)originalMaps[i].supportedModes=masks[i];
                room.ModeIndex=mode;room.MapIndex=map;details.ConfigureSetup(setup);
            }
        }
    }
}
