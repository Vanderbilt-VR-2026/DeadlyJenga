using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPhysicsHands;

namespace DeadlyJenga.CastleBots
{
    public sealed class CastleHandBotPopulation:MonoBehaviour
    {
        public BotWorld Session {get;private set;}
        [Range(1,20)]public int maximumHands=9;
        public int Count=>hands.Count;
        public IReadOnlyList<CastleHandBotBrain> Hands=>hands;
        public int SelectedPreset {get;private set;}
        public string SelectedName=>profiles[SelectedPreset].displayName;
        public string Status {get;private set;}="";
        readonly List<CastleHandBotBrain> hands=new List<CastleHandBotBrain>();
        CastleHandBotPersonality[] profiles;
        Transform staging;
        bool ownsProfiles;
        int serial;
        public void Initialize(BotWorld session)
        {
            Session=session;profiles=Resources.LoadAll<CastleHandBotPersonality>("CastleHandBots").OrderBy(p=>p.displayName).ToArray();
            if(profiles.Length==0){ownsProfiles=true;profiles=Enumerable.Range(0,4).Select(CastleHandBotPersonality.Create).ToArray();}
            SelectedPreset=Mathf.Max(0,System.Array.FindIndex(profiles,p=>p.displayName=="Playful"));
            staging=new GameObject("Inactive AI hand assembly").transform;staging.SetParent(transform,false);staging.gameObject.SetActive(false);
        }
        public void CyclePersonality(){SelectedPreset=(SelectedPreset+1)%profiles.Length;}
        public void SelectPreset(int index){if(profiles==null||profiles.Length==0)return;SelectedPreset=Mathf.Clamp(index,0,profiles.Length-1);}
        public CastleHandBotBrain SpawnHand()
        {
            if(Count>=Mathf.Clamp(maximumHands,1,20)||Count>=Mathf.Max(0,Session.Bots.targetPopulation-Session.Bots.humanPlayers)){Status="AI hand limit reached";return null;}
            var actor=Instantiate(Session.handPrefab,staging);
            if(!actor.GetComponent<CandyFlyingHandBridge>()){Destroy(actor.gameObject);Status="Hand prefab needs the shared physical-hand bridge";return null;}
            actor.name="AI Giant Hand "+(++serial)+" — "+SelectedName;
            actor.motor.externallyDriven=true;actor.motor.captureDesktopCursor=false;actor.follow.enabled=false;
            foreach(var camera in actor.GetComponentsInChildren<Camera>(true)){camera.enabled=false;camera.tag="Untagged";}
            foreach(var listener in actor.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            foreach(var driver in actor.GetComponentsInChildren<TrackedPoseDriver>(true))driver.enabled=false;
            foreach(var origin in actor.GetComponentsInChildren<Unity.XR.CoreUtils.XROrigin>(true))origin.enabled=false;
            var brain=actor.gameObject.AddComponent<CastleHandBotBrain>();actor.transform.SetParent(null,true);
            brain.Initialize(this,actor,profiles[SelectedPreset],serial);hands.Add(brain);Place(brain,serial);
            Session.Bots.ReconcileHandSlots();Status="Spawned "+actor.name;return brain;
        }
        void Place(CastleHandBotBrain brain,int number)
        {
            float y=Session.world.TopHeight-2;
            var outward=Quaternion.Euler(0,number*137.508f,0)*Vector3.forward;
            float radius=Session.world.Radius+8;
            brain.Bridge.ResetExternalRig();brain.Actor.Place(Session.world.Center+outward*radius+Vector3.up*(y-Session.world.Center.y),Quaternion.LookRotation(-outward));brain.Restart();
        }
        public void ClearHands()
        {
            foreach(var hand in hands)if(hand){hand.gameObject.SetActive(false);Destroy(hand.gameObject);}
            hands.Clear();Status="AI hands cleared";if(Session!=null&&Session.Bots)Session.Bots.ReconcileHandSlots();
        }
        public void TrimHumanCapacity()
        {
            while(hands.Count>Mathf.Max(0,Session.Bots.targetPopulation-Session.Bots.humanPlayers))
            {var hand=hands[hands.Count-1];hands.RemoveAt(hands.Count-1);if(hand){hand.gameObject.SetActive(false);Destroy(hand.gameObject);}}
        }
        public void CastleRebuilt(){for(int i=0;i<hands.Count;i++)if(hands[i])Place(hands[i],i+1);}
        public float ThreatCost(Vector3 point)
        {float cost=0;foreach(var hand in hands)if(hand&&!hand.Paused)cost+=Mathf.Max(0,7-Vector3.Distance(point,hand.ContactPosition))*.5f;return cost;}
        void Update()
        {
            hands.RemoveAll(h=>!h);
            for(int i=0;i<hands.Count;i++)if(hands[i].Actor.motor.transform.position.y<-10||Vector3.Distance(hands[i].Actor.motor.transform.position,Session.world.Center)>180)Place(hands[i],i+1);
        }
        void OnDestroy()
        {
            foreach(var hand in hands)if(hand)Destroy(hand.gameObject);
            if(ownsProfiles&&profiles!=null)foreach(var profile in profiles)if(profile)Destroy(profile);
        }
    }
}
