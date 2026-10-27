using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        Light lobbySun;
        void LobbyResidenceChanged(GameObject owner,string state)
        {
            if(owner!=gameObject||!Environment)return;
            if(state=="LobbyParked")
            {
                SetWardrobeOpen(false);
                var position=Environment.transform.position;
                Environment.applyLighting=false;
                foreach(var component in Environment.GetComponents<MonoBehaviour>())component.enabled=false;
                Environment.profile=localArt;Environment.Refresh();Environment.foundation=null;
                var baseline=Environment.GetComponent<CandyArtBaseline>();if(baseline)baseline.map=null;
                var terrain=Environment.sandRenderer.GetComponent<CandySandbar>();if(terrain)terrain.map=null;
                Environment.transform.position=position;Environment.Refresh();
                if(lobbySun)lobbySun.enabled=false;
            }
            else if(state=="LobbyResumed")
            {
                Environment.profile=localArt;Environment.sun=lobbySun;Environment.applyLighting=true;
                if(lobbySun)lobbySun.enabled=true;
                Environment.enabled=true;Environment.Refresh();
                var skins=Environment.GetComponent<CandySceneSkins>();if(skins)skins.enabled=true;
            }
        }
    }
}
