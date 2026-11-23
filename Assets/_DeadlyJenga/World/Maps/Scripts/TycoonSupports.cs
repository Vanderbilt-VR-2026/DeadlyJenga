using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Interlocking modules hold their resting pose until their supports break.
    static class TycoonSupports
    {
        public static MagneticStructure Build(Transform root, IReadOnlyList<Rigidbody> bodies, IReadOnlyList<CastlePlacement> layout)
        {
            var structure = root.gameObject.AddComponent<MagneticStructure>();
            structure.UseBatchedBreakaway();
            var bounds = new Bounds[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                bounds[i] = body.GetComponent<BoxCollider>().bounds;
                // Released modules have no tall standing load to solve and some
                // molded colliders share seams at rest. Limit separation speed.
                body.solverIterations = 8;
                body.solverVelocityIterations = 3;
                body.maxDepenetrationVelocity = 3f;
                var connection = body.gameObject.AddComponent<MagneticBlock>();
                connection.strength = 3;
                connection.foundationAnchor = bounds[i].min.y <= root.position.y + .05f;
                // Stronger foundations and a spread of seam strengths prevent
                // a uniform shake from opening every connection on one tick.
                float variation = ((uint)i * 2654435761u >> 24) / 255f;
                connection.breakawayResistance = connection.foundationAnchor
                    ? Mathf.Lerp(8f, 16f, variation)
                    : Mathf.Lerp(2.5f, 10f, variation);
                structure.Add(connection);
            }

            for (int i = 0; i < bounds.Length; i++)
            for (int j = i + 1; j < bounds.Length; j++)
            {
                // Two removable planks latch to their seaward beam. Nearby boards and
                // the second bearing still collide with them after that latch releases.
                bool looseA = layout[i].DeckProfile.Finish == TycoonDeckFinish.LoosePeach;
                bool looseB = layout[j].DeckProfile.Finish == TycoonDeckFinish.LoosePeach;
                if (looseA || looseB)
                {
                    var support = layout[looseA ? j : i];
                    var plank = layout[looseA ? i : j];
                    if (support.Section != CastleSection.Keep || support.Position.z >= plank.Position.z ||
                        support.Position.y >= plank.Position.y || Mathf.Abs(support.Position.x-plank.Position.x)>6.5f)
                        continue;
                }
                var delta = bounds[i].center - bounds[j].center;
                var extent = bounds[i].extents + bounds[j].extents;
                float x = extent.x - Mathf.Abs(delta.x);
                float y = extent.y - Mathf.Abs(delta.y);
                float z = extent.z - Mathf.Abs(delta.z);
                const float seamTolerance = .48f;
                if (x < -seamTolerance || y < -seamTolerance || z < -seamTolerance) continue;
                int bearingAxes = (x > .08f ? 1 : 0) + (y > .08f ? 1 : 0) + (z > .08f ? 1 : 0);
                if (bearingAxes >= 2) structure.Connect(i, j);
            }

            structure.Recompute();
            return structure;
        }
    }
}
