using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class SeaSpiritActor : MonoBehaviour, ISeaSpiritControls
    {
        CastleHandActor actor;
        CandyFlyingHandBridge bridge;
        SeaSpiritController controller;
        bool previousInput;
        bool motorWasEnabled;
        float selectedReach=25;
        Collider[] hiddenColliders;
        Renderer[] hiddenRenderers;
        bool[] colliderStates,rendererStates;
        public bool SeaSpiritActive => controller;
        public float SeaSpiritReach => controller?controller.reach:selectedReach;
        public float SeaSpiritOrbit => controller?controller.orbitAngle:0;
        public SeaSpiritController Controller => controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => SeaSpiritSystems.Create = root =>
        {
            if(!root||!root.GetComponent<CastleHandActor>())return null;
            return root.GetComponent<SeaSpiritActor>()??root.AddComponent<SeaSpiritActor>();
        };

        public void SetSeaSpirit(bool active)
        {
            if(active==SeaSpiritActive)return;
            actor=GetComponent<CastleHandActor>();bridge=GetComponent<CandyFlyingHandBridge>();
            if(!actor||!bridge)return;
            if(active)
            {
                previousInput=actor.InputSuspended;
                controller=gameObject.AddComponent<SeaSpiritController>();controller.reach=selectedReach;
                controller.Initialize(actor,actor.View,GetComponent<CandyTrackingSource>());
                actor.InputSuspended=true;bridge.enabled=false;NetworkActors.Local.Add(bridge);motorWasEnabled=actor.motor.enabled;actor.motor.enabled=false;
                var visual=actor.motor.transform.Find("HandVisual");
                hiddenColliders=actor.motor.GetComponentsInChildren<Collider>(true);
                hiddenRenderers=visual?visual.GetComponentsInChildren<Renderer>(true):new Renderer[0];
                colliderStates=new bool[hiddenColliders.Length];rendererStates=new bool[hiddenRenderers.Length];
                for(int i=0;i<hiddenColliders.Length;i++){colliderStates[i]=hiddenColliders[i].enabled;hiddenColliders[i].enabled=false;}
                for(int i=0;i<hiddenRenderers.Length;i++){rendererStates[i]=hiddenRenderers[i].enabled;hiddenRenderers[i].enabled=false;}
            }
            else
            {
                selectedReach=controller.reach;controller.Shutdown();Destroy(controller);controller=null;
                actor.InputSuspended=previousInput;
                actor.motor.enabled=motorWasEnabled;
                for(int i=0;hiddenColliders!=null&&i<hiddenColliders.Length;i++)if(hiddenColliders[i])hiddenColliders[i].enabled=colliderStates[i];
                for(int i=0;hiddenRenderers!=null&&i<hiddenRenderers.Length;i++)if(hiddenRenderers[i])hiddenRenderers[i].enabled=rendererStates[i];
                bridge.enabled=true;bridge.ResetTrackingRig();
            }
        }
        public void SetSeaSpiritReach(float meters)
        { selectedReach=Mathf.Clamp(meters,8,80);if(controller)controller.reach=selectedReach; }
        public void RecenterSeaSpirit(){if(controller)controller.orbitAngle=0;}
    }
}
