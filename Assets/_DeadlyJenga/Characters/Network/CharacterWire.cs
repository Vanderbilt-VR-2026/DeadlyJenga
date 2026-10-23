using System;
using System.IO;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CharacterNetwork
{
    // How survivors and hands write themselves for the network. Only Characters reads this format.
    public static class CharacterWire
    {
        public static void WriteHandPose(BinaryWriter w, CandyHandPose p)
        {
            w.Write(p.tracked); w.Write(p.skeletonTracked); NetWire.Write(w, p.position); NetWire.Write(w, p.rotation);
            for (int i = 0; i < 20; i++) NetWire.Write(w, p.skeleton.Joint(i));
        }
        public static CandyHandPose ReadHandPose(BinaryReader r)
        {
            var p = new CandyHandPose { tracked = r.ReadBoolean(), skeletonTracked = r.ReadBoolean(), position = NetWire.ReadVector(r), rotation = NetWire.ReadRotation(r) };
            for (int i = 0; i < 20; i++)
            {
                var joint=NetWire.ReadVector(r);if(joint.sqrMagnitude>.09f)throw new InvalidDataException("Hand joint outside wrist range");
                p.skeleton.SetJoint(i,joint);
            }
            return p;
        }

        // Survivor: body frame plus color.
        public static void WriteSurvivor(BinaryWriter w, CandyAvatarFrame f, int color)
        {
            NetWire.Write(w, f.feet); NetWire.Write(w, f.head); NetWire.Write(w, f.headRotation); w.Write(f.scale);
            WriteHandPose(w, f.left); WriteHandPose(w, f.right); w.Write(color);
        }
        public static void ReadSurvivor(BinaryReader r, out CandyAvatarFrame frame, out int color)
        {
            frame = new CandyAvatarFrame { feet = NetWire.ReadVector(r), head = NetWire.ReadVector(r), headRotation = NetWire.ReadRotation(r), scale = NetWire.ReadNumber(r), left = ReadHandPose(r), right = ReadHandPose(r) };
            if (frame.scale < .1f || frame.scale > 2) throw new InvalidDataException("Invalid avatar scale");
            color = r.ReadInt32();
        }
        const int AppearanceMagic=0x41565031;
        public static void WriteSurvivor(BinaryWriter w,CandyAvatarFrame frame,int color,AvatarAppearance appearance,IEnemyActor enemy=null,IBlasterControls blaster=null,byte remoteBlaster=0)
        {
            WriteSurvivor(w,frame,color);w.Write(AppearanceMagic);w.Write((uint)(appearance.accessories&CandyPlayerAvatar.SupportedAccessories));
            bool npc=enemy?.IsEnemy==true;w.Write(npc);if(npc)w.Write(Mathf.Clamp(enemy.HitDamage,1,99));
            w.Write(blaster==null?(byte)(remoteBlaster&3):(byte)((blaster.Drawn?1:0)|(blaster.LeftHanded?2:0)));
            w.Write(CandyPlayerAvatar.StyleBits(appearance));
        }
        public static void ReadSurvivor(BinaryReader r,out CandyAvatarFrame frame,out int color,out AvatarAppearance appearance,out EnemySpawnSettings enemy)
        {ReadSurvivor(r,out frame,out color,out appearance,out enemy,out _);}
        public static void ReadSurvivor(BinaryReader r,out CandyAvatarFrame frame,out int color,out AvatarAppearance appearance,out EnemySpawnSettings enemy,out byte blaster)
        {
            ReadSurvivor(r,out frame,out color);appearance=default;enemy=null;blaster=0;
            if(r.BaseStream.Position==r.BaseStream.Length)return;
            if(r.ReadInt32()!=AppearanceMagic)throw new InvalidDataException("Unknown survivor appearance extension");
            uint bits=r.ReadUInt32();if((bits&~(uint)CandyPlayerAvatar.SupportedAccessories)!=0)throw new InvalidDataException("Unknown avatar attachment");
            appearance.accessories=(AvatarAccessory)bits;
            if(r.ReadBoolean()){float damage=NetWire.ReadNumber(r);if(damage<1||damage>99)throw new InvalidDataException("Invalid enemy damage");enemy=new EnemySpawnSettings{hitDamage=damage};}
            if(r.BaseStream.Position<r.BaseStream.Length){blaster=r.ReadByte();if((blaster&~3)!=0)throw new InvalidDataException("Invalid blaster flags");}
            if(r.BaseStream.Position<r.BaseStream.Length){uint style=r.ReadUInt32();if((style&~0xFFFFFu)!=0||(style&7)>6||((style>>3)&15)>12)throw new InvalidDataException("Invalid avatar style");appearance=CandyPlayerAvatar.WithStyle(appearance,style);}
            if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Trailing survivor appearance data");
        }

        // Hand: handedness, size, finger target and (when simulated) the 16 bone poses.
        public static void WriteHand(BinaryWriter w, bool left, float size, CandyHandPose target, Rigidbody[] bodies)
        {
            w.Write(left); w.Write(size); WriteHandPose(w, target);
            int count = bodies != null ? bodies.Length : 0; w.Write(count);
            for (int i = 0; i < count; i++) NetWire.Write(w, new Pose(bodies[i].position, bodies[i].rotation));
        }
        public static void WriteHand(BinaryWriter w, bool left, float size, CandyHandPose target, Pose[] bones)
        {
            w.Write(left); w.Write(size); WriteHandPose(w, target);
            int count = bones != null ? bones.Length : 0; w.Write(count);
            for (int i = 0; i < count; i++) NetWire.Write(w, bones[i]);
        }
        const int SeaMagic=0x53454131;
        public sealed class SeaFrame
        {
            public Pose bust,leftShoulder,rightShoulder;
            public byte[] left,right;
        }
        static byte[] HandBytes(CastleHandActor actor)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                actor.GetComponent<CandyFlyingHandBridge>().WriteState(writer);return stream.ToArray();
            }
        }
        public static void WriteSeaSpirit(BinaryWriter w,SeaSpiritController spirit)
        {
            WriteSeaSpirit(w,new SeaFrame{bust=new Pose(spirit.BustAnchor.position,spirit.BustAnchor.rotation),
                leftShoulder=new Pose(spirit.LeftShoulder.position,spirit.LeftShoulder.rotation),
                rightShoulder=new Pose(spirit.RightShoulder.position,spirit.RightShoulder.rotation),left=HandBytes(spirit.LeftHand),right=HandBytes(spirit.RightHand)});
        }
        public static void WriteSeaSpirit(BinaryWriter w,SeaFrame frame)
        {
            w.Write(SeaMagic);w.Write((byte)1);NetWire.Write(w,frame.bust);NetWire.Write(w,frame.leftShoulder);NetWire.Write(w,frame.rightShoulder);
            w.Write(frame.left.Length);w.Write(frame.left);w.Write(frame.right.Length);w.Write(frame.right);
        }
        public static SeaFrame ReadSeaSpirit(BinaryReader r)
        {
            if(r.BaseStream.Position==r.BaseStream.Length)return null;
            if(r.ReadInt32()!=SeaMagic||r.ReadByte()!=1)throw new InvalidDataException("Unknown hand extension");
            var frame=new SeaFrame{bust=NetWire.ReadPose(r),leftShoulder=NetWire.ReadPose(r),rightShoulder=NetWire.ReadPose(r)};
            ValidateLocation(frame.bust.position);ValidateLocation(frame.leftShoulder.position);ValidateLocation(frame.rightShoulder.position);
            frame.left=ReadPayload(r);frame.right=ReadPayload(r);return frame;
        }
        static byte[] ReadPayload(BinaryReader r)
        {
            int count=NetWire.ReadCount(r,2048);var bytes=r.ReadBytes(count);
            if(bytes.Length!=count)throw new EndOfStreamException();return bytes;
        }
        public static void ValidateLocation(Vector3 point)
        {
            if(Services.World==null)return;
            var bounds=Services.World.PlayableBounds;bounds.Expand(Mathf.Max(100,bounds.size.magnitude*4));
            if(!bounds.Contains(point))throw new InvalidDataException("Hand outside reachable map area");
        }
        public static void ReadHand(BinaryReader r, out bool left, out float size, out CandyHandPose target, out Pose[] bones)
        {
            left = r.ReadBoolean(); size = NetWire.ReadNumber(r);
            if (size < .1f || size > 100) throw new InvalidDataException("Invalid hand size");
            target = ReadHandPose(r);
            ValidateLocation(target.position);
            if(!CandyHandTransition.IsValidPose(target))throw new InvalidDataException("Invalid hand skeleton");
            for(int j=0;j<20;j++)if(target.skeleton.Joint(j).sqrMagnitude>.09f)throw new InvalidDataException("Hand joint outside wrist range");
            bones = new Pose[NetWire.ReadCount(r, 16)];
            if (bones.Length != 0 && bones.Length != 16) throw new InvalidDataException("Invalid hand bones");
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = NetWire.ReadPose(r);ValidateLocation(bones[i].position);
                if(Vector3.Distance(bones[i].position,target.position)>size+5)throw new InvalidDataException("Hand bone outside wrist reach");
            }
        }
    }
}
