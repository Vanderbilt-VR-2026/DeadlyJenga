using System;
using System.IO;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Opens up one recorded actor frame so it can be blended, moved, mirrored or re-dressed,
    // then packs it again. Used for punch-in joins, comps and clones.
    public sealed class ActorFrame
    {
        public bool visible;
        public float health;
        public string equipment = "";
        public bool isHand;

        // Survivor
        public CandyAvatarFrame body;
        public int color;
        public AvatarAppearance look;
        byte[] rawSurvivor;               // kept when the frame holds something this class does not rebuild (enemies)

        // Hand
        public bool left;
        public float size;
        public int handLook;
        public CandyHandPose target;
        public Pose[] bones = new Pose[0];
        byte[] handTail = new byte[0];    // sea-spirit data after the hand, copied as it is

        public Vector3 Position => isHand ? target.position : body.feet;

        public static ActorFrame Read(string typeId, byte[] frame)
        {
            var result = new ActorFrame { isHand = typeId == HandTrack.Id };
            if (frame == null || frame.Length < 5) return result;
            using (var r = new BinaryReader(new MemoryStream(frame)))
            {
                result.visible = r.ReadBoolean();
                byte[] state = r.ReadBytes(r.ReadInt32());
                result.health = r.ReadSingle();
                result.equipment = r.ReadString();
                if (!result.visible || state.Length == 0) return result;
                using (var s = new BinaryReader(new MemoryStream(state)))
                {
                    if (result.isHand)
                    {
                        CharacterWire.ReadHand(s, out result.left, out result.size, out result.target, out result.bones, out result.handLook);
                        result.handTail = s.ReadBytes((int)(s.BaseStream.Length - s.BaseStream.Position));
                    }
                    else
                    {
                        CharacterWire.ReadSurvivor(s, out result.body, out result.color, out result.look, out var enemy, out _);
                        if (enemy != null) result.rawSurvivor = state;
                    }
                }
            }
            return result;
        }

        public byte[] Write()
        {
            if (!visible) return ActorTrack.Pack(false, null, health, equipment);
            if (rawSurvivor != null) return ActorTrack.Pack(true, rawSurvivor, health, equipment);
            using (var memory = new MemoryStream())
            using (var w = new BinaryWriter(memory))
            {
                if (isHand) { CharacterWire.WriteHand(w, left, size, target, bones, handLook); w.Write(handTail); }
                else CharacterWire.WriteSurvivor(w, body, color, look);
                w.Flush();
                return ActorTrack.Pack(true, memory.ToArray(), health, equipment);
            }
        }

        // ---- Blending (punch-in and comp joins) ----

        // weight 0 = all "from", 1 = all "to".
        public static ActorFrame Blend(ActorFrame from, ActorFrame to, float weight)
        {
            if (!from.visible || !to.visible || from.isHand != to.isHand) return weight < .5f ? from : to;
            var result = to;   // flags, outfit and fingers come from the newer frame
            if (to.isHand)
            {
                result.target.position = Vector3.Lerp(from.target.position, to.target.position, weight);
                result.target.rotation = Mix(from.target.rotation, to.target.rotation, weight);
                if (from.bones.Length == to.bones.Length)
                {
                    result.bones = new Pose[to.bones.Length];
                    for (int i = 0; i < to.bones.Length; i++)
                        result.bones[i] = new Pose(Vector3.Lerp(from.bones[i].position, to.bones[i].position, weight),
                            Mix(from.bones[i].rotation, to.bones[i].rotation, weight));
                }
                return result;
            }
            result.body.feet = Vector3.Lerp(from.body.feet, to.body.feet, weight);
            result.body.head = Vector3.Lerp(from.body.head, to.body.head, weight);
            result.body.headRotation = Mix(from.body.headRotation, to.body.headRotation, weight);
            result.body.left = BlendHand(from.body.left, to.body.left, weight);
            result.body.right = BlendHand(from.body.right, to.body.right, weight);
            return result;
        }

        static CandyHandPose BlendHand(CandyHandPose from, CandyHandPose to, float weight)
        {
            to.position = Vector3.Lerp(from.position, to.position, weight);
            to.rotation = Mix(from.rotation, to.rotation, weight);
            return to;
        }

        // A blend of two rotations that is always a real rotation. Recorded rotations come back from the wire
        // slightly off unit length, and a plain Slerp of them could come out near zero (seen on the pilot's hand:
        // the network reader refused it and the hand stopped playing). Falls back to the newer rotation.
        static Quaternion Mix(Quaternion from, Quaternion to, float weight)
        {
            float Length(Quaternion q) => Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            Quaternion Unit(Quaternion q, Quaternion fallback) { float l = Length(q); return float.IsNaN(l) || l < .001f ? fallback : new Quaternion(q.x / l, q.y / l, q.z / l, q.w / l); }
            to = Unit(to, Quaternion.identity);
            from = Unit(from, to);
            return Unit(Quaternion.Slerp(from, to, weight), to);
        }

        // ---- Moving a whole performance (clones) ----

        // Turn by "turn" around "pivot", then shift by "move". With mirror, left and right are swapped first.
        public void Transform(Vector3 pivot, Quaternion turn, Vector3 move, bool mirror, Vector3 mirrorNormal)
        {
            if (!visible) return;
            Vector3 P(Vector3 p)
            {
                if (mirror) p = pivot + Vector3.Reflect(p - pivot, mirrorNormal);
                return pivot + turn * (p - pivot) + move;
            }
            Quaternion R(Quaternion q)
            {
                if (mirror) q = Quaternion.LookRotation(Vector3.Reflect(q * Vector3.forward, mirrorNormal), Vector3.Reflect(q * Vector3.up, mirrorNormal));
                return turn * q;
            }
            if (isHand)
            {
                target.position = P(target.position); target.rotation = R(target.rotation);
                for (int i = 0; i < bones.Length; i++) bones[i] = new Pose(P(bones[i].position), R(bones[i].rotation));
                if (mirror) { left = !left; target.skeleton = Flip(target.skeleton); }
                return;
            }
            body.feet = P(body.feet); body.head = P(body.head); body.headRotation = R(body.headRotation);
            body.left.position = P(body.left.position); body.left.rotation = R(body.left.rotation);
            body.right.position = P(body.right.position); body.right.rotation = R(body.right.rotation);
            if (mirror)
            {
                // The mirrored left hand is now on the right side of the body: swap them and flip their fingers.
                var swap = body.left; body.left = body.right; body.right = swap;
                body.left.skeleton = Flip(body.left.skeleton); body.right.skeleton = Flip(body.right.skeleton);
            }
        }

        static CandyHandSkeleton Flip(CandyHandSkeleton skeleton)
        {
            var flipped = new CandyHandSkeleton();
            for (int i = 0; i < 20; i++) { var joint = skeleton.Joint(i); joint.x = -joint.x; flipped.SetJoint(i, joint); }
            return flipped;
        }
    }
}
