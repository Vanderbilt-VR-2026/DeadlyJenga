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
            ResetLobbyArrivalReceive();
            nextHello=nextSend=0;lastServer=Time.unscaledTime;
        }
        static bool IsLobbyScene(SceneRef scene)=>scene.IsIndex&&
            System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(scene.AsIndex))=="Lobby";

        bool LoadRoomScene(int index,string destination,System.Action onFailure=null)
        {
            var runner=Runner;
            void Fail(string error)
            {
                if(!this||Runner!=runner)return;
                SwitchingMap=false;transitioning=false;pendingGameMap=null;
                Status="Could not load "+destination+": "+error;
                onFailure?.Invoke();
            }
            try
            {
                transitioning=true;
                var op=runner.LoadScene(SceneRef.FromIndex(index),LoadSceneMode.Single);
                if(!op.IsValid){Fail("scene request was rejected");return false;}
                op.AddOnCompleted(completed=>{if(completed.Error!=null)Fail(completed.Error.Message);});
                return !op.IsDone||op.Error==null;
            }
            catch(System.Exception error){Fail(error.Message);return false;}
        }

        public bool ReturnMatchToLobby()
        {
            if(Connected&&!IsHost||Busy||Departing||SwitchingMap||RoomSceneBusy)return false;
            // Resolve the enabled Lobby scene by name without a project asset path.
            for(int i=0;i<SceneManager.sceneCountInBuildSettings;i++)
            {
                string path=SceneUtility.GetScenePathByBuildIndex(i);
                if(System.IO.Path.GetFileNameWithoutExtension(path)!="Lobby")continue;
                if(Connected)
                {
                    var scenes=Runner.SceneInfo;
                    if(scenes.SceneCount>0&&scenes.Scenes[scenes.SceneCount-1]==SceneRef.FromIndex(i))return true;
                }
                else if(SceneManager.GetActiveScene().buildIndex==i)return true;
                return BeginSceneDeparture(i,Connected,"lobby");
            }
            Debug.LogError("Lobby must be enabled in the build before returning a room.",this);return false;
        }
    }
}
