using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;

namespace DeadlyJenga.CharacterNetwork
{
    // Someone else's flying hand on this machine. On the host it is a real physics hand that
    // touches the castle, and its actual bones are sent on to everyone else. Elsewhere it just
    // shows the host's palm and finger poses.
    public sealed class HandNetworkView : MonoBehaviour, INetworkActorView, ISeaSpiritView, IFlyingState
    {
        public CandyPhysicalHand Hand { get; private set; }
        public Vector3 Velocity=>Hand&&Hand.bodies.Length>0?Hand.bodies[0].linearVelocity:Vector3.zero;
        public Transform Anchor=>seaActive&&bust?bust:Hand&&Hand.bodies.Length>0?Hand.bodies[0].transform:transform;
        public bool Interactive=>Hand&&Hand.IsInteractive&&!suspended;
        public IReadOnlyList<Rigidbody> ContactBodies=>Hand?Hand.bodies:null;
        NetworkContext context;
        CandyHandSettings handSettings;
        CandyHandPose target;
        Pose[] bones = Array.Empty<Pose>();
        bool left, active, hasTarget, suspended;
        int look, lastTaunt;
        float size;
        HandNetworkView seaLeft,seaRight;
        Transform bust,leftShoulder,rightShoulder;
        bool seaActive;
        bool? forcedLeft;
        public bool Active=>seaActive&&active&&!suspended;
        public Transform BustAnchor=>bust;
        public Transform LeftShoulder=>leftShoulder;
        public Transform RightShoulder=>rightShoulder;
        public Transform LeftHandAnchor=>seaLeft&&seaLeft.Hand?seaLeft.Hand.bodies[0].transform:null;
        public Transform RightHandAnchor=>seaRight&&seaRight.Hand?seaRight.Hand.bodies[0].transform:null;
        public CandyPhysicalHand LeftPhysicalHand=>seaLeft?seaLeft.Hand:null;
        public CandyPhysicalHand RightPhysicalHand=>seaRight?seaRight.Hand:null;


        public HandNetworkView Init(NetworkContext networkContext) { context = networkContext; return this; }

        CandyFlyingHandBridge Template => context.localHand ? context.localHand.GetComponent<CandyFlyingHandBridge>() : null;

        public string NetworkKind => NetworkKinds.Hand;
        public bool NetworkActive => seaActive ? active : Hand ? Hand.gameObject.activeInHierarchy && Hand.skin && Hand.skin.enabled : active;
        public Vector3 NetworkPosition => seaActive&&bust?bust.position:Hand ? Hand.bodies[0].position : target.position;
        public void WriteState(BinaryWriter w)
        {
            if(seaActive)
            {
                CharacterWire.WriteHand(w,false,size,target,(Pose[])null, look);
                CharacterWire.WriteSeaSpirit(w,new CharacterWire.SeaFrame{bust=new Pose(bust.position,bust.rotation),leftShoulder=new Pose(leftShoulder.position,leftShoulder.rotation),rightShoulder=new Pose(rightShoulder.position,rightShoulder.rotation),left=Payload(seaLeft),right=Payload(seaRight)});return;
            }
            if (Hand) CharacterWire.WriteHand(w, Hand.left, Hand.GeometrySize, Hand.Target, Hand.bodies, look);
            else CharacterWire.WriteHand(w, left, size, target, bones, look);
        }

        public void ReadState(BinaryReader r, bool isActive)
        {
            CharacterWire.ReadHand(r, out bool newLeft, out float newSize, out var newTarget, out var newBones, out look);
            var sea=CharacterWire.ReadSeaSpirit(r);
            if(sea!=null&&forcedLeft.HasValue)throw new InvalidDataException("Nested spirit payload");
            if(sea!=null)
            {
                Clear();seaActive=true;active=isActive;suspended=false;target=newTarget;size=newSize;
                SetAnchor(ref bust,"Spirit bust",sea.bust);SetAnchor(ref leftShoulder,"Left shoulder",sea.leftShoulder);SetAnchor(ref rightShoulder,"Right shoulder",sea.rightShoulder);
                ReadChild(ref seaLeft,true,sea.left,isActive);ReadChild(ref seaRight,false,sea.right,isActive);return;
            }
            ClearSea();
            var bridge = Template;
            // The host decides every hand's size and handedness.
            if (context.host && bridge) { newSize = bridge.settings.size; newLeft = forcedLeft??bridge.useLeftHand; }
            suspended = false;
            if (Hand && (left != newLeft || Mathf.Abs(size - newSize) > .01f)) Clear();
            left = newLeft; size = newSize; target = newTarget; bones = newBones; active = isActive; hasTarget = true;
            if (!Hand)
            {
                if (!bridge || !bridge.prefab) return;
                handSettings = Instantiate(bridge.settings); handSettings.size = size;
                Hand = Instantiate(bridge.prefab, transform); Hand.settings = handSettings; Hand.left = left; Hand.Build(target);Hand.ReplicatedBones=!context.host;
            }
            Hand.gameObject.SetActive(active);
            CandyHandLook.For(Hand).Apply(look & 0x0F);
            int taunt = look >> 4;
            if (taunt != lastTaunt && taunt > 0 && taunt <= Emotes.Hand.Length) GameEvents.RaiseEmoted(Hand.gameObject, Emotes.Hand[taunt - 1]);
            lastTaunt = taunt;
            Hand.SetTarget(target, context.host && active ? 1 : 0);
            if (context.host) Hand.SetInteractive(active);
        }

        public void Suspend()
        {
            suspended = true;if(seaLeft)seaLeft.Suspend();if(seaRight)seaRight.Suspend();
            if (Hand) { Hand.SetTarget(Hand.Target, 0); Hand.SetInteractive(false); }
        }

        public void Render(float dt)
        {
            if(seaActive){if(seaLeft)seaLeft.Render(dt);if(seaRight)seaRight.Render(dt);return;}
            if (!hasTarget || !active || !Hand) return;
            if (context.host) { if (!suspended) Hand.SetInteractive(true); return; }
            if (bones.Length != 16) return;
            float t = 1 - Mathf.Exp(-20 * dt);
            for (int i = 0; i < 16; i++)
            {
                var body = Hand.bodies[i]; var p = bones[i];
                body.position = Vector3.Lerp(body.position, p.position, t);
                body.rotation = Quaternion.Slerp(body.rotation, p.rotation, t);
            }
            Hand.skin.enabled = true;
        }

        void Clear()
        {
            if (Hand) { Hand.gameObject.SetActive(false); Destroy(Hand.gameObject); Hand = null; }
            if (handSettings) { Destroy(handSettings); handSettings = null; }
        }

        static byte[] Payload(HandNetworkView view)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){view.WriteState(writer);return stream.ToArray();}}
        void SetAnchor(ref Transform anchor,string label,Pose pose)
        {if(!anchor){anchor=new GameObject(label).transform;anchor.SetParent(transform,false);}anchor.SetPositionAndRotation(pose.position,pose.rotation);}
        void ReadChild(ref HandNetworkView child,bool isLeft,byte[] payload,bool enabled)
        {
            if(!child){var root=new GameObject(isLeft?"Remote left hand":"Remote right hand");root.transform.SetParent(transform,false);child=root.AddComponent<HandNetworkView>().Init(context);child.forcedLeft=isLeft;}
            using(var stream=new MemoryStream(payload))using(var reader=new BinaryReader(stream))child.ReadState(reader,enabled);
        }
        void ClearSea()
        {
            seaActive=false;
            if(seaLeft){seaLeft.gameObject.SetActive(false);Destroy(seaLeft.gameObject);seaLeft=null;}
            if(seaRight){seaRight.gameObject.SetActive(false);Destroy(seaRight.gameObject);seaRight=null;}
            if(bust){Destroy(bust.gameObject);bust=null;}if(leftShoulder){Destroy(leftShoulder.gameObject);leftShoulder=null;}if(rightShoulder){Destroy(rightShoulder.gameObject);rightShoulder=null;}
        }
        void OnDestroy() { Clear();ClearSea(); }
    }
}
