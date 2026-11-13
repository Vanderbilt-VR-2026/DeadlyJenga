using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // No joints or per-frame attraction. Revisit support only when a connection breaks.
    public sealed class MagneticStructure : MonoBehaviour
    {
        public readonly List<MagneticBlock> Blocks = new List<MagneticBlock>();
        readonly List<List<int>> neighbors = new List<List<int>>();
        readonly Queue<int> queue = new Queue<int>();
        readonly List<Pose> rest = new List<Pose>();
        readonly List<CollisionDetectionMode> collisionModes = new List<CollisionDetectionMode>();
        readonly Queue<MagneticBlock> releases = new Queue<MagneticBlock>();
        readonly List<MagneticBlock> moving = new List<MagneticBlock>();
        readonly List<Vector3> releaseVelocity = new List<Vector3>();
        const int ReleasesPerFrame = 5;
        const int MovingBudget = 48;
        int releaseFrame = -1;
        int releasedThisFrame;
        bool batchedBreakaway, pending;
        internal bool BatchesBreakaway => batchedBreakaway;
        public void UseBatchedBreakaway() => batchedBreakaway = true;

        void FixedUpdate()
        {
            if (Replica) return;
            if (pending)
            {
                pending = false;
                Recompute();
                Changed?.Invoke();
            }
            if (releases.Count > 0) ReleasePieces();
        }

        void ReleasePieces()
        {
            if (releaseFrame != Time.frameCount)
            {
                releaseFrame = Time.frameCount;
                releasedThisFrame = 0;
            }
            int awake = 0;
            for (int i = moving.Count - 1; i >= 0; i--)
            {
                var block = moving[i];
                if (!block || !block.gameObject.activeSelf) { moving.RemoveAt(i); continue; }
                if (!block.Body.isKinematic && !block.Body.IsSleeping()) awake++;
            }
            while (releases.Count > 0 && releasedThisFrame < ReleasesPerFrame && awake < MovingBudget)
            {
                var block = releases.Dequeue();
                if (!block || !block.gameObject.activeSelf || !block.Body.isKinematic) continue;
                SetBody(block);
                var impulse = releaseVelocity[block.Index];
                releaseVelocity[block.Index] = Vector3.zero;
                if (impulse.sqrMagnitude > 0) block.Body.AddForce(impulse, ForceMode.VelocityChange);
                moving.Add(block);
                releasedThisFrame++;
                awake++;
            }
        }

        internal void AddReleaseVelocity(MagneticBlock block, Vector3 velocity)
            => releaseVelocity[block.Index] = Vector3.ClampMagnitude(releaseVelocity[block.Index] + velocity, 18f);

        public bool Replica { get; private set; }
        public int Recomputations { get; private set; }
        public event Action Changed;

        public void Add(MagneticBlock block)
        {
            block.Structure = this; block.Index = Blocks.Count;
            Blocks.Add(block); neighbors.Add(new List<int>());
            if (batchedBreakaway)
            {
                releaseVelocity.Add(Vector3.zero);
                rest.Add(new Pose(block.Body.position, block.Body.rotation));
                collisionModes.Add(block.Body.collisionDetectionMode);
            }
        }
        public void Connect(int a, int b)
        {
            if (a == b || neighbors[a].Contains(b)) return;
            neighbors[a].Add(b); neighbors[b].Add(a);
        }
        public void Break(MagneticBlock block)
        {
            if (Replica || block.Structure != this || block.Broken) return;
            block.Broken = true;
            if (batchedBreakaway)
            {
                block.Supported = false;
                releases.Enqueue(block);
                pending = true;
                return;
            }
            Recompute();
            Changed?.Invoke();
        }
        public int[] SaveBroken()
        {
            var broken = new List<int>();
            foreach (var block in Blocks) if (block.Broken) broken.Add(block.Index);
            return broken.ToArray();
        }
        public void Restore(int[] broken)
        {
            if (broken != null) foreach (int i in broken) Blocks[i].Broken = true;
            Recompute();
        }
        public void SetReplica(bool replica)
        {
            if (batchedBreakaway && Replica && !replica)
            {
                // Replica poses already arrived through the world snapshot.
                for (int i = 0; i < Blocks.Count; i++)
                {
                    var block = Blocks[i];
                    var pose = rest[i];
                    if (!block.gameObject.activeSelf || (block.Body.position - pose.position).sqrMagnitude > .0144f ||
                        Quaternion.Angle(block.Body.rotation, pose.rotation) > 2)
                        block.Broken = true;
                }
                Replica = false;
                foreach (var block in Blocks) if (block.Broken)
                {
                    block.Supported = false;
                    if (block.gameObject.activeSelf)
                    {
                        SetBody(block);
                        if (!moving.Contains(block)) moving.Add(block);
                    }
                }
                Recompute();
                return;
            }
            Replica = replica;
            foreach (var block in Blocks) SetBody(block);
        }
        void SetBody(MagneticBlock block)
        {
            bool pinned = Replica || block.Supported;
            bool wasPinned = block.Body.isKinematic;
            if (pinned && !block.Body.isKinematic)
            {
                block.Body.linearVelocity = Vector3.zero;
                block.Body.angularVelocity = Vector3.zero;
            }
            if (batchedBreakaway && pinned) block.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            block.Body.isKinematic = pinned;
            if (batchedBreakaway && !pinned) block.Body.collisionDetectionMode = collisionModes[block.Index];
            block.Body.interpolation = Replica ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            if (!pinned && wasPinned) block.Body.WakeUp();
        }
        public void Recompute()
        {
            Recomputations++;
            queue.Clear();
            foreach (var block in Blocks)
            {
                block.Supported = !block.Broken && block.foundationAnchor;
                if (block.Supported) queue.Enqueue(block.Index);
            }
            while (queue.Count > 0)
            {
                int a = queue.Dequeue();
                foreach (int b in neighbors[a])
                {
                    var next = Blocks[b];
                    // A weak bridge cannot become a replacement foundation for a strong tower.
                    if (next.Broken || next.Supported || Blocks[a].strength < next.strength) continue;
                    next.Supported = true; queue.Enqueue(b);
                }
            }
            foreach (var block in Blocks)
            {
                // Detachment is permanent until rebuild; fallen pieces never snap back remotely.
                bool detached = !block.Supported && !block.Broken;
                if (!block.Supported) block.Broken = true;
                if (batchedBreakaway && detached)
                    releases.Enqueue(block);
                else if (!batchedBreakaway || block.Supported && !block.Body.isKinematic)
                    SetBody(block);
            }
        }
    }
}
