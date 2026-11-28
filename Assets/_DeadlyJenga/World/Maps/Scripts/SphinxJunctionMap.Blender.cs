using UnityEngine;
using System.Collections.Generic;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class SphinxJunctionMap
    {
        readonly List<(Collider First, Collider Second)> authoredSeams = new();
        readonly HashSet<Rigidbody> bakedMembers = new();

        Rigidbody AddBakedMember(string name)
        {
            var center = structure.BoundsOf(name).center;
            Begin(name, center.x, center.y, center.z);
            var body = End();
            bakedMembers.Add(body);
            return body;
        }

        void BuildBakedSphinx()
        {
            // The long front paws reach past the original plaza edge. Each extension has
            // its own visible pier, and can fail independently of the main island.
            foreach (int side in new[] { -1, 1 })
            {
                Begin("Forepaw extension driven pier " + side, side * 6.2f, 0, -16.61f);
                Box(0, -.65f, 0, 2.8f, 4.3f, 2.58f, Shade);
                Box(0, .48f, 0, 2.94f, .35f, 2.7f, Teal, false);
                Box(0, 1.18f, 0, 3.65f, .64f, 2.94f, Honey);
                End(true);
                Begin("Forepaw extension paving " + side, side * 6.2f, 2, -16.61f);
                Box(0, 0, 0, 5.15f, 1, 3.18f, Sand);
                End();
            }

            var west = AddBakedMember("Sphinx shoulder bearing -1");
            var east = AddBakedMember("Sphinx shoulder bearing 1");
            var chest = AddBakedMember("Sphinx chest lintel");
            Fasten(chest, west);
            Fasten(chest, east);

            Rigidbody tailBearing = null;
            foreach (var name in structure.Names)
            {
                if (name.StartsWith("Sphinx torso bay", System.StringComparison.Ordinal) ||
                    name.StartsWith("Foreleg", System.StringComparison.Ordinal) ||
                    name.StartsWith("Hind ", System.StringComparison.Ordinal))
                {
                    var member = AddBakedMember(name);
                    if (name == "Sphinx torso bay 13") tailBearing = member;
                }
                else if (name.StartsWith("Tail", System.StringComparison.Ordinal))
                {
                    var segment = AddBakedMember(name);
                    if (tailBearing) Fasten(segment, tailBearing);
                    tailBearing = segment;
                }
            }
            AddBakedMember("Sphinx left forepaw");
            AddBakedMember("Sphinx right forepaw");
            AddBakedMember("Sphinx west stairs");
            AddBakedMember("Sphinx east stairs");

            var crown = structure.BoundsOf("Sphinx carved crown");
            Begin("Sphinx carved crown", crown.center.x, crown.center.y, crown.center.z);
            Vector3 size = crown.size;
            float baseHeight = 11.58f;
            float height = crown.max.y - baseHeight;
            model.BearingBox(Vector3.up * (baseHeight + height * .5f - crown.center.y),
                new Vector3(size.x * .72f, height, size.z * .72f));
            var head = End();
            bakedMembers.Add(head);
            Fasten(head, chest);
        }

        // The whole town, pyramid and sphinx come from the Blender export, member by member, with the
        // same bearing colliders the Blender stand and pull tests use. Only the railway is built in code.
        const string TownMarker = "Pyramid podium and hall floor";
        readonly HashSet<Rigidbody> anchored = new();

        void BuildBakedTown()
        {
            var byName = new Dictionary<string, Rigidbody>();
            foreach (var name in structure.Names)
            {
                if (name.StartsWith("Rail bridge ", System.StringComparison.Ordinal)) continue;   // placed by Railway()
                var bounds = structure.SolidBoundsOf(name);
                Begin(name, bounds.center.x, bounds.center.y, bounds.center.z);
                // Members driven into the sand bed are piles. The podium and hall floor are the foundation.
                bool foundation = name == TownMarker;
                var body = End(!foundation && bounds.min.y < .32f);
                bakedMembers.Add(body);
                byName[name] = body;
                if (foundation) anchored.Add(body);
                if (name.StartsWith("Plaza paving ", System.StringComparison.Ordinal) && name.EndsWith(",-2", System.StringComparison.Ordinal))
                    plazas.Add(body);
            }
            void Pin(string member, string bearing)
            {
                if (byName.TryGetValue(member, out var a) && byName.TryGetValue(bearing, out var b) && a != b) Fasten(a, b);
            }
            Mortar(byName, .054f, "Great pyramid | course", "Great pyramid | front railway", "Great pyramid | rear railway",
                "Great pyramid | teal band");
            Mortar(byName, .27f, "Sphinx", "Tail", "Hind", "Foreleg");   // looser sculpt joints (15 cm Blender)
            Pin("Plaza paving 0,0", "Driven sandstone pier 0,0");
            Pin("Sphinx chest lintel", "Sphinx shoulder bearing -1");
            Pin("Sphinx chest lintel", "Sphinx shoulder bearing 1");
            Pin("Sphinx carved crown", "Sphinx chest lintel");
            string previous = "Sphinx torso bay 13";
            foreach (var name in structure.Names)
            {
                if (!name.StartsWith("Tail", System.StringComparison.Ordinal)) continue;
                Pin(name, previous);
                previous = name;
            }
        }

        // Masonry: every stone of the pyramid shell (or piece of the carved sphinx) is fastened to each stone it
        // touches, the lighter stone's joint carrying the bond. The hollow stepped shell then stands as one
        // body until a pull, an impact or a disaster breaks the joints (matching the Blender stand test).
        void Mortar(Dictionary<string, Rigidbody> byName, float reach, params string[] prefixes)
        {
            var stones = new List<(Rigidbody body, Bounds box)>();
            foreach (var pair in byName)
                foreach (var prefix in prefixes)
                    if (pair.Key.StartsWith(prefix, System.StringComparison.Ordinal))
                    {
                        var box = structure.SolidBoundsOf(pair.Key);
                        box.Expand(reach);   // touching within reach/2 on every side
                        stones.Add((pair.Value, box));
                        break;
                    }
            stones.Sort((a, b) => a.box.min.x.CompareTo(b.box.min.x));
            for (int i = 0; i < stones.Count; i++)
                for (int j = i + 1; j < stones.Count && stones[j].box.min.x <= stones[i].box.max.x; j++)
                {
                    if (!stones[i].box.Intersects(stones[j].box)) continue;
                    var (light, heavy) = stones[i].body.mass <= stones[j].body.mass
                        ? (stones[i].body, stones[j].body) : (stones[j].body, stones[i].body);
                    var joint = light.gameObject.AddComponent<FixedJoint>();
                    joint.connectedBody = heavy;
                    joint.breakForce = light.mass * 190;
                    joint.breakTorque = light.mass * 260;
                }
        }

        void BuildBakedLandmarkAdditions()
        {
            var shop = AddBakedMember("Tack shop frame");
            Fasten(AddBakedMember("Tack shop roof"), shop);
            AddBakedMember("Copper Canyon ticket room");
            AddBakedMember("Pyramid broad exterior stair");
        }

        void ExcludeAuthoredOverlaps()
        {
            // Sculpted silhouette sections can overlap in Blender. Exclude only those
            // exact collider pairs at the authored pose, then restore their contacts
            // when the pieces pull apart. Players still collide with every member.
            var colliders = built.GetComponentsInChildren<Collider>();
            bool town = structure.Has(TownMarker);
            var boundsByCollider = new Bounds[colliders.Length];
            var bodyByCollider = new Rigidbody[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                boundsByCollider[i] = colliders[i].bounds;
                bodyByCollider[i] = colliders[i].attachedRigidbody;
            }
            for (int i = 0; i < colliders.Length; i++)
            {
                var first = colliders[i];
                var firstBody = bodyByCollider[i];
                var bounds = boundsByCollider[i];
                for (int j = 0; j < i; j++)
                {
                    var second = colliders[j];
                    var secondBody = bodyByCollider[j];
                    if (firstBody == secondBody || !bounds.Intersects(boundsByCollider[j]) ||
                        !bakedMembers.Contains(firstBody) && !bakedMembers.Contains(secondBody)) continue;
                    if (!Penetration(first, second, out float depth) || depth < .021f) continue;
                    // A piece resting on top of another keeps its bearing contact, whatever the overlap.
                    if (town && (bounds.min.y > boundsByCollider[j].max.y - .14f || boundsByCollider[j].min.y > bounds.max.y - .14f)) continue;
                    Physics.IgnoreCollision(first, second);
                    authoredSeams.Add((first, second));
                }
            }
        }

        static bool Penetration(Collider first, Collider second, out float depth)
        {
            return Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                second, second.transform.position, second.transform.rotation, out _, out depth);
        }

        void RestoreSeparatedContacts()
        {
            for (int i = authoredSeams.Count - 1; i >= 0; i--)
            {
                var (first, second) = authoredSeams[i];
                if (first && second && first.gameObject.activeInHierarchy && second.gameObject.activeInHierarchy)
                {
                    if (Penetration(first, second, out _)) continue;
                    Physics.IgnoreCollision(first, second, false);
                }
                authoredSeams.RemoveAt(i);
            }
        }

        float RailRise(float x, float z)
        {
            if (structure == null) return 0;
            float approach = Mathf.SmoothStep(0, 1, Mathf.Clamp01((x + 16f) / 10f));
            float exit = Mathf.SmoothStep(0, 1, Mathf.Clamp01((11.1f - x) / 9.6f));
            float distance = (z - 10.4f) / 9.2f;
            float squared = distance * distance;
            return .49f * approach * exit * Mathf.Exp(-squared * squared);
        }
    }
}
