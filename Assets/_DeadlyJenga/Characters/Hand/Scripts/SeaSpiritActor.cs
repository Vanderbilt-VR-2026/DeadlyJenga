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
        bool bridgeWasEnabled;
        bool motorWasEnabled;
        float selectedReach=25;
        Collider[] hiddenColliders;
        Renderer[] hiddenRenderers;
        bool[] colliderStates,rendererStates;
        public bool SeaSpiritActive => controller&&controller.Active;
        public float SeaSpiritReach => controller?controller.reach:selectedReach;
        public float SeaSpiritOrbit => controller?controller.orbitAngle:0;
        public SeaSpiritController Controller => controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => SeaSpiritSystems.Create = root =>
        {
            if(!root||!root.GetComponent<CastleHandActor>())return null;
            var provider=root.GetComponent<SeaSpiritActor>()??root.AddComponent<SeaSpiritActor>();
            return provider.TryPrepareEntry()?provider:null;
        };

        bool TryPrepareEntry()
        {
            try
            {
                var template=GetComponent<CastleHandActor>();
                var contact=GetComponent<CandyFlyingHandBridge>();
                if(!template||!template.motor||!template.follow||!contact||contact.actor!=template||
                    !contact.prefab||!contact.settings||!template.motor.GetComponent<Rigidbody>())return false;
                var camera=template.follow.viewCamera;
                var follow=camera?camera.GetComponentInParent<FlyingHand.ThirdPersonVRFollow>():null;
                var observer=follow?follow.transform:camera?camera.transform.parent:null;
                if(!camera||!observer||!gameObject.scene.IsValid()||!Finite(selectedReach)||selectedReach<8||selectedReach>80)return false;
                var scale=observer.lossyScale;
                if(!Finite(observer.position)||!Finite(observer.rotation)||!Finite(scale)||
                    Mathf.Abs(scale.x)<.0001f||Mathf.Abs(scale.y)<.0001f||Mathf.Abs(scale.z)<.0001f||
                    !Finite(camera.transform.position)||!Finite(camera.transform.rotation))return false;
                var settings=contact.settings;
                if(!Positive(settings.size)||!Positive(settings.palmMass)||!Positive(settings.fingerMass)||
                    !Positive(settings.maxSpeed)||!Positive(settings.maxTargetSpeed)||
                    !Positive(settings.maxAngularSpeed)||!Positive(settings.maxTargetAngularSpeed))return false;
                var world=Services.World;
                if(world==null||!Finite(world.TopHeight)||!Finite(world.WaterHeight(world.Center)))return false;
                return SeaSpiritController.TryApproachRange(world.PlayableBounds,world.Center,Vector3.back,
                    world.Radius,selectedReach,world.StepHeight,out _,out _,out _);
            }
            catch(System.Exception){return false;}
        }
        static bool Positive(float value)=>Finite(value)&&value>0;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static bool Finite(Quaternion value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z)&&Finite(value.w)&&
            value.x*value.x+value.y*value.y+value.z*value.z+value.w*value.w>.0001f;

        public void SetSeaSpirit(bool active)
        {
            if(active&&SeaSpiritActive||!active&&!controller)return;
            if(active&&!TryPrepareEntry())return;
            if(active&&controller){controller.Shutdown();Destroy(controller);controller=null;}
            actor=GetComponent<CastleHandActor>();bridge=GetComponent<CandyFlyingHandBridge>();
            if(!actor||!bridge)return;
            if(active)
            {
                var candidate=gameObject.AddComponent<SeaSpiritController>();candidate.reach=selectedReach;
                try
                {
                    candidate.Initialize(actor,actor.View,GetComponent<CandyTrackingSource>());
                    if(!candidate.Active){candidate.Shutdown();Destroy(candidate);return;}
                }
                catch(System.Exception error)
                {candidate.Shutdown();Destroy(candidate);Debug.LogWarning("Sea spirit could not initialize: "+error.Message,this);return;}
                controller=candidate;
                bridgeWasEnabled=bridge.enabled;bridge.enabled=false;NetworkActors.Local.Add(bridge);
                motorWasEnabled=actor.motor.enabled;actor.motor.enabled=false;
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
                actor.motor.enabled=motorWasEnabled;
                for(int i=0;hiddenColliders!=null&&i<hiddenColliders.Length;i++)if(hiddenColliders[i])hiddenColliders[i].enabled=colliderStates[i];
                for(int i=0;hiddenRenderers!=null&&i<hiddenRenderers.Length;i++)if(hiddenRenderers[i])hiddenRenderers[i].enabled=rendererStates[i];
                bridge.enabled=bridgeWasEnabled;if(bridgeWasEnabled)bridge.ResetTrackingRig();
            }
        }
        public void SetSeaSpiritReach(float meters)
        { if(float.IsNaN(meters)||float.IsInfinity(meters))return;selectedReach=Mathf.Clamp(meters,8,80);if(controller)controller.reach=selectedReach; }
        public void RecenterSeaSpirit(){if(controller)controller.orbitAngle=0;}
    }
}
