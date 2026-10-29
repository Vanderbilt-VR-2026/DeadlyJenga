using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandRoomConnection
    {
        static NetworkSceneInfo LobbyStartScene(NetworkProjectConfig config)
        {
            var scenes=new NetworkSceneInfo();
            if(config.PeerMode!=NetworkProjectConfig.PeerModes.Multiple)return scenes;
            for(int i=0;i<SceneManager.sceneCountInBuildSettings;i++)
                if(System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i))=="Lobby")
                {scenes.AddSceneRef(SceneRef.FromIndex(i),LoadSceneMode.Additive);break;}
            return scenes;
        }
        internal void ResumeLobbyConnection()
        {
            CompleteDeparture();peers.Clear();socialPeers.Clear();socialServer.Clear();waitingPose=0;welcomed=false;
            nextHello=nextSend=0;lastServer=Time.unscaledTime;
        }
        public bool ReturnMatchToLobby()
        {
            if(!Connected)return false;
            if(!IsHost)return false;
            // Resolve the enabled Lobby scene by name without a project asset path.
            for(int i=0;i<SceneManager.sceneCountInBuildSettings;i++)
            {
                string path=SceneUtility.GetScenePathByBuildIndex(i);
                if(System.IO.Path.GetFileNameWithoutExtension(path)!="Lobby")continue;
                Runner.LoadScene(SceneRef.FromIndex(i),LoadSceneMode.Single);return true;
            }
            Debug.LogError("Lobby must be enabled in the build before returning a room.",this);return false;
        }
    }
}
