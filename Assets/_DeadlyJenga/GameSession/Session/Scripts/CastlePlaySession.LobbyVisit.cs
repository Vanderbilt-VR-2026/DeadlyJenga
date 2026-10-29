using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastlePlaySession : ILobbyMatchVisitControls
    {
        IslandLobby visitLobby;bool loadingLobby;
        readonly Dictionary<AudioListener,bool> visitListeners=new Dictionary<AudioListener,bool>();
        public bool VisitingLobby=>visitLobby;
        public bool CanVisitLobby=>Ready&&!VisitingLobby&&!loadingLobby&&enabled;
        public void VisitLobby()
        {
            if(!CanVisitLobby)return;
            if(!IslandLobby.Persistent){StartCoroutine(LoadRealLobby());return;}
            EnterPersistentLobby();
        }
        IEnumerator LoadRealLobby()
        {
            loadingLobby=true;
            var existing=SceneManager.GetSceneByName("Lobby");
            if(!existing.IsValid()||!existing.isLoaded)yield return SceneManager.LoadSceneAsync("Lobby",LoadSceneMode.Additive);
            yield return null;
            loadingLobby=false;
            if(!this)yield break;
            if(!IslandLobby.Persistent){Debug.LogError("The real Lobby scene did not initialize.",this);yield break;}
            EnterPersistentLobby();
        }
        void EnterPersistentLobby()
        {
            if(!Ready||!IslandLobby.Persistent)return;
            if(Modes&&Modes.DemoSpectating)Modes.SetDemoSpectating(false);
            if(SeaSpiritActive)seaSpirit.SetSeaSpirit(false);
            SetMenu(false);
            visitLobby=IslandLobby.Persistent;
            if(!visitLobby.BeginMatchVisit(this)){visitLobby=null;return;}
            foreach(var playable in new[]{Player,Hand})if(NetworkActors.Alive(playable))
            {
                playable.InputSuspended=true;if(playable.ViewCamera)playable.ViewCamera.enabled=false;
                foreach(var listener in playable.gameObject.GetComponentsInChildren<AudioListener>(true)){visitListeners[listener]=listener.enabled;listener.enabled=false;}
            }
        }
        public void ReturnToMatch()=>CloseLobbyVisit(true);
        void CloseLobbyVisit(bool restore)
        {
            if(!visitLobby)return;
            var old=visitLobby;visitLobby=null;old.EndMatchVisit(this);
            foreach(var pair in visitListeners)if(pair.Key)pair.Key.enabled=pair.Value;
            visitListeners.Clear();
            if(!restore||!Ready)return;
            if(Player.ViewCamera)Player.ViewCamera.enabled=!PlayingHand;
            if(Hand.ViewCamera)Hand.ViewCamera.enabled=PlayingHand;
            SetMenu(false);
        }
    }
}
