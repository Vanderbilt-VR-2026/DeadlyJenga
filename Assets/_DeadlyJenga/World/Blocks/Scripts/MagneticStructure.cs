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
        public bool Replica { get; private set; }
        public int Recomputations { get; private set; }
        public event Action Changed;

        public void Add(MagneticBlock block)
        {
            block.Structure = this; block.Index = Blocks.Count;
            Blocks.Add(block); neighbors.Add(new List<int>());
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
            block.Body.isKinematic = pinned;
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
                if (!block.Supported) block.Broken = true;
                SetBody(block);
            }
        }
    }
}
