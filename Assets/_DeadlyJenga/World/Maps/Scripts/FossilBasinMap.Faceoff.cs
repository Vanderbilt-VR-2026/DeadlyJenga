using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class FossilBasinMap
    {
        [Serializable] sealed class FaceoffLayout
        {
            public FaceoffBay[] bays;
            public FaceoffFlight[] flights;
            public FaceoffBone[] bones;
        }

        [Serializable] sealed class FaceoffBox
        {
            public Vector3 p, size;
        }

        [Serializable] sealed class FaceoffPost
        {
            public FaceoffBox[] blocks;
        }

        [Serializable] sealed class FaceoffBay
        {
            public string name;
            public FaceoffBox[] stones;
            public FaceoffPost[] posts;
        }

        [Serializable] sealed class FaceoffFlight
        {
            public string name;
            public Vector3 a, b;
            public float width;
            public int treads;
        }

        [Serializable] sealed class FaceoffBone
        {
            public string name;
            public Vector3 p;
            public FaceoffBox[] boxes;
        }

        sealed class FaceoffDeck
        {
            public FossilMember member;
            public FaceoffBox[] stones;
        }

        readonly List<FaceoffDeck> faceoffDecks = new();

        void BuildFaceoff()
        {
            var layout = JsonUtility.FromJson<FaceoffLayout>(Resources.Load<TextAsset>("FossilBasinFaceoff").text);
            faceoffDecks.Clear();
            foreach (var bay in layout.bays) BuildFaceoffBay(bay);
            foreach (var flight in layout.flights) BuildFaceoffFlight(flight);
            // Longer ramps bridge the main bot routes across the steepest Blender switchbacks.
            FaceoffRoute("West apron to tail cross-span", P(-7.2f,1.55f,-15.04f), P(-12.2f,5.3f,-9.825f));
            FaceoffRoute("Tail cross-span to west pier perch", P(-18.55f,5.3f,-6.7f), P(-21.9f,9.55f,-.40f));
            FaceoffRoute("West pier perch to Rex ankle", P(-24.4f,9.55f,3.4f), P(-23.3f,14.2f,7.65f));
            FaceoffRoute("East apron to main switchback", P(7.3f,2.15f,-15.9f), P(8.5f,5.25f,-12.125f));
            FaceoffRoute("Triceratops forefoot to high connector", P(7.25f,12.26f,6.5f), P(3.24f,16.24f,9.7f));
            AddFaceoffBones(layout.bones);
        }

        void BuildFaceoffBay(FaceoffBay bay)
        {
            var heads = new List<FossilMember>();
            int postIndex = 0;
            foreach (var post in bay.posts)
            {
                var baseBlock = post.blocks[0];
                var padTop = baseBlock.p.y - baseBlock.size.y * .5f - .006f;
                var pad = new GameObject(bay.name + " submerged bearing");
                pad.transform.SetParent(structure, false);
                pad.transform.localPosition = new Vector3(baseBlock.p.x, padTop - .20f, baseBlock.p.z);
                pad.AddComponent<BoxCollider>().size = new Vector3(baseBlock.size.x * 1.35f, .4f, baseBlock.size.z * 1.35f);
                // A kinematic body lets the lowest pier unit be bolted to its bearing.
                var padBody = pad.AddComponent<Rigidbody>();
                padBody.isKinematic = true;

                FossilMember lower = null;
                for (int start = 0; start < post.blocks.Length; start += 3)
                {
                    var unit = post.blocks.Skip(start).Take(3).ToArray();
                    var center = (unit[0].p + unit[unit.Length - 1].p) * .5f;
                    float bottom = unit[0].p.y - unit[0].size.y * .5f;
                    var last = unit[unit.Length - 1];
                    float top = last.p.y + last.size.y * .5f;
                    bool cap = start + unit.Length == post.blocks.Length;
                    if (cap) top = bay.stones[0].p.y - bay.stones[0].size.y * .5f;
                    var core = new Vector3(center.x, (bottom + top) * .5f, center.z);
                    var body = Piece(bay.name + " pier " + postIndex + "/" + start / 3, core, 34 + unit.Length * 7, c =>
                    {
                        c.Proxy(core, Quaternion.identity, new Vector3(last.size.x, top - bottom - .008f, last.size.z));
                        for (int i = 0; i < unit.Length; i++)
                            c.Box(unit[i].p, unit[i].size, (start + i) % 3 == 1 ? Clay : Teal, Flat, false);
                        if (cap)
                        {
                            c.Box(new Vector3(core.x, top - .23f, core.z), new Vector3(last.size.x + .24f, .44f, last.size.z + .24f), Timber, Flat, false);
                            for (int sign = -1; sign <= 1; sign += 2)
                                c.Shaft(new Vector3(core.x, top - 1.12f, core.z), new Vector3(core.x + .76f * sign, top - .16f, core.z), .105f, .105f, Timber, false, false);
                        }
                    });
                    if (lower) Socket(body, lower, new Vector3(core.x, bottom, core.z), 7200);
                    else Socket(body.Body, padBody, new Vector3(core.x, bottom, core.z), 7200);
                    lower = body;
                }
                heads.Add(lower);
                postIndex++;
            }

            int strips = Mathf.Clamp(Mathf.RoundToInt(bay.stones.Select(a => a.p.x).Distinct().Count() / 2f), 1, 4);
            float minX = bay.stones.Min(a => a.p.x), maxX = bay.stones.Max(a => a.p.x);
            for (int strip = 0; strip < strips; strip++)
            {
                int index = strip;
                var stones = bay.stones.Where(a => Mathf.Clamp((int)(Mathf.InverseLerp(minX, maxX, a.p.x) * strips), 0, strips - 1) == index).ToArray();
                if (stones.Length == 0) continue;
                var center = stones.Aggregate(Vector3.zero, (sum, stone) => sum + stone.p) / stones.Length;
                var member = Piece(bay.name + " deck " + strip, center, 15 + stones.Length * 2, c =>
                {
                    foreach (var stone in stones)
                        c.Box(stone.p, stone.size + new Vector3(.018f, 0, .018f), Color.Lerp(Coral, Clay, (stone.p.x + stone.p.z) % .19f), Flat);
                    AddFaceoffDressing(c, bay.name, stones);
                    var fore = stones.OrderBy(item => item.p.z).First();
                    var lamp = fore.p + Vector3.up * (fore.size.y * .5f);
                    c.Box(lamp + Vector3.up * .09f, new Vector3(.28f,.18f,.28f), Timber, Flat, false);
                    c.Box(lamp + Vector3.up * .24f, new Vector3(.15f,.21f,.15f), Brass, Flat, false);
                });
                decks.Add(member);
                faceoffDecks.Add(new FaceoffDeck { member = member, stones = stones });
                foreach (var head in heads.OrderBy(p => (p.transform.localPosition - center).sqrMagnitude).Take(2))
                    Socket(member, head, new Vector3(head.transform.localPosition.x, center.y - .32f, head.transform.localPosition.z), 5200);
            }
        }

        static void AddFaceoffDressing(FossilCarving carve, string bay, FaceoffBox[] tiles)
        {
            void Shelter(Vector3 at, float width, float depth)
            {
                if (!Owns(at)) return;
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var post = at + P(x * width * .46f, 0, z * depth * .46f);
                    carve.Shaft(post, post + Vector3.up * 1.62f, .05f, .05f, Timber, false, false);
                }
                for (int side = -1; side <= 1; side += 2)
                    carve.Box(at + P(side*width*.26f,1.86f,0), P(width*.57f,.055f,depth*1.06f),
                        new Color32(238,170,92,255), Quaternion.Euler(0, 0, -side * 15), false);
                carve.Box(at+Vector3.up*.59f, P(1.5f,.14f,.72f), Timber, Flat, false);
                for (int i = 0; i < 6; i++)
                    carve.Box(at + P(-.55f+i*.22f,.7f,0), P(.15f,.10f,.23f), i%2==0?Ivory:Clay, Flat, false);
            }

            bool Owns(Vector3 point) => tiles.Any(tile => Mathf.Abs(point.x-tile.p.x)<tile.size.x*.5f+.013f &&
                Mathf.Abs(point.z-tile.p.z)<tile.size.z*.5f+.013f);

            if (bay.StartsWith("A3", StringComparison.Ordinal))
            {
                Shelter(P(.8f,1.55f,-19.3f),3.7f,2.85f);
                Shelter(P(-3.15f,1.55f,-19.4f),2.4f,2);
            }
            if (bay.StartsWith("C1", StringComparison.Ordinal)) Shelter(P(-9.5f,8.4f,-10.1f),3,2.13f);
            if (bay.StartsWith("E2", StringComparison.Ordinal)) Shelter(P(-18.2f,24.2f,12.8f),3,2.5f);
            if (bay.StartsWith("E3", StringComparison.Ordinal)) Shelter(P(10.8f,17.65f,9.25f),2.35f,1.9f);
            var foot = P(23,18.35f,12.2f);
            if (!bay.StartsWith("E4", StringComparison.Ordinal) || !Owns(foot)) return;
            var peak = foot + Vector3.up * 3.5f;
            var ropeHead = foot + P(-2.8f,3.05f,0);
            for (int side = -1; side <= 1; side += 2)
                carve.Shaft(foot+P(side*.62f,0,0),peak,.13f,.13f,Timber,false,false);
            carve.Shaft(peak,ropeHead,.15f,.15f,Clay,false,false);
            carve.Shaft(ropeHead,ropeHead-Vector3.up*1.55f,.024f,.024f,Timber,false,false);
            carve.Box(ropeHead-Vector3.up*1.76f,P(.66f,.60f,.59f),Brass,Flat,false);
        }

        FossilMember FindFaceoffSeat(Vector3 point, float tolerance, FossilMember except = null)
        {
            FossilMember best = null;
            float distance = tolerance * tolerance;
            foreach (var deck in faceoffDecks)
            {
                if (deck.member == except) continue;
                foreach (var tile in deck.stones)
                {
                    var d = point - tile.p;
                    float dx = Mathf.Max(0, Mathf.Abs(d.x) - tile.size.x * .5f);
                    float dz = Mathf.Max(0, Mathf.Abs(d.z) - tile.size.z * .5f);
                    float dy = d.y - tile.size.y * .5f;
                    float cost = dx * dx + dz * dz + dy * dy * 4;
                    if (cost >= distance) continue;
                    distance = cost;
                    best = deck.member;
                }
            }
            return best;
        }

        void FaceoffRoute(string name, Vector3 a, Vector3 b)
        {
            int treads = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(a.y-b.y) / StepHeight));
            BuildFaceoffFlight(new FaceoffFlight { name = name, a = a, b = b, width = 2.08f, treads = treads });
        }

        void BuildFaceoffFlight(FaceoffFlight flight)
        {
            Vector3 a = flight.a, b = flight.b;
            var run = Vector3.ProjectOnPlane(b - a, Vector3.up);
            float length = run.magnitude;
            if (length < .10f || Mathf.Abs(a.y - b.y) > length * 1.9f) return;
            var rotation = Quaternion.LookRotation(run);
            var low = FindFaceoffSeat(a, 2.1f);
            var high = FindFaceoffSeat(b, 2.1f, low);
            if (!low || !high) return;
            var center = (a + b) * .5f;
            var pitch = Quaternion.LookRotation(b - a);
            float step = length / flight.treads;
            var forward = run / length;
            var member = Piece("Teal flight " + flight.name, center, 24, c =>
            {
                // The stringer gives the survivor and bot one continuous surface.
                c.Box(center - Vector3.up * .14f, new Vector3(flight.width, .28f, (b-a).magnitude+.06f), Teal, pitch);
                for (int i = 0; i < flight.treads; i++)
                {
                    var p = Vector3.Lerp(a, b, (i + .5f) / flight.treads);
                    c.Box(p - Vector3.up * .13f, new Vector3(flight.width,.26f,step+.028f), Teal, rotation, false);
                    c.Box(p - forward * step * .38f, new Vector3(flight.width,.038f,.043f), Brass, rotation, false);
                }
            });
            Socket(member, low, a, 3900);
            Socket(member, high, b, 3900);
        }

        void AddFaceoffBones(FaceoffBone[] bones)
        {
            var bytes = Resources.Load<TextAsset>("FossilBasinFaceoffMeshes").bytes;
            using var compressed = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            using var reader = new BinaryReader(compressed);
            if (reader.ReadInt32() != bones.Length) throw new InvalidDataException("Fossil Basin mesh and layout counts differ");
            var parts = new Dictionary<string, FossilMember>(StringComparer.Ordinal);
            var feetAt = new Dictionary<FossilMember, Vector3>();
            foreach (var bone in bones)
            {
                var member = Piece(bone.name, bone.p, 5 + bone.boxes.Length, c =>
                {
                    foreach (var shape in bone.boxes)
                        c.Proxy(shape.p, Quaternion.identity, shape.size);
                });
                member.fossil = bone.name.StartsWith("Rex", StringComparison.Ordinal) ? 2 :
                    bone.name.StartsWith("Triceratops", StringComparison.Ordinal) ? 3 : 1;
                FillFaceoffMesh(member.mesh, reader);
                parts.Add(bone.name, member);
                if (bone.name.Contains(" tarsals"))
                {
                    var shoe = bone.boxes.OrderBy(box => box.p.y - box.size.y * .5f).First();
                    feetAt.Add(member, shoe.p - Vector3.up * shoe.size.y * .5f);
                }
            }
            foreach (string kind in new[] { "Rex", "Stegosaurus", "Triceratops" })
                ConnectFaceoffSkeleton(kind, parts, feetAt);
        }

        static void FillFaceoffMesh(Mesh mesh, BinaryReader reader)
        {
            int vertexCount = reader.ReadInt32(), indexCount = reader.ReadInt32();
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var colors = new Color32[vertexCount];
            var indices = new int[indexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                colors[i] = Ivory;
            }
            for (int i = 0; i < indexCount; i++) indices[i] = reader.ReadInt32();
            if (vertexCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
        }

        void ConnectFaceoffSkeleton(string kind, Dictionary<string, FossilMember> parts, Dictionary<FossilMember, Vector3> feetAt)
        {
            FossilMember Get(string label) => parts[kind + " " + label];
            void Join(FossilMember a, FossilMember b, float force = 7000)
            {
                Socket(a, b, (a.transform.localPosition + b.transform.localPosition)*.5f, force);
            }
            for (int i = 1; i < 8; i++) Join(Get("vertebra and ribs " + i), Get("vertebra and ribs " + (i-1)));
            var first = Get("vertebra and ribs 0");
            var last = Get("vertebra and ribs 7");
            var tail = first;
            for (int i = 0; parts.TryGetValue(kind + " tail centrum " + i, out var next); i++)
            {
                // The tail hangs far out, so its joints carry a big bending load.
                Join(next, tail, 9600);
                tail = next;
            }
            var neck = Get("cervical vertebrae");
            var skull = Get(kind == "Rex" ? "skull" : "perforated skull");
            Join(neck, last);
            Join(skull, neck);
            Join(Get("mandible"), skull, 3300);
            var cradle = Get("excavation rib cradle");
            Join(Get("vertebra and ribs 4"), cradle, 12100);
            var cradleSeat = FindFaceoffSeat(cradle.transform.localPosition - Vector3.up * 1.5f, 7f);
            if (cradleSeat) Join(cradle, cradleSeat, 9600);
            foreach (var arm in parts.Where(pair => pair.Key.StartsWith(kind+" small ", StringComparison.Ordinal)))
                Join(arm.Value, last, 3600);

            var uppers = parts.Where(pair => pair.Key.StartsWith(kind+" upper leg", StringComparison.Ordinal)).Select(pair => pair.Value).ToList();
            var shins = parts.Where(pair => pair.Key.StartsWith(kind+" lower leg", StringComparison.Ordinal)).Select(pair => pair.Value).ToList();
            foreach (var foot in parts.Where(pair => pair.Key.StartsWith(kind+" tarsals", StringComparison.Ordinal)).Select(pair => pair.Value))
            {
                var shin = shins.OrderBy(item => (item.transform.localPosition-foot.transform.localPosition).sqrMagnitude).First();
                shins.Remove(shin);
                var thigh = uppers.OrderBy(item => (item.transform.localPosition-shin.transform.localPosition).sqrMagnitude).First();
                uppers.Remove(thigh);
                Join(foot, shin);
                Join(shin, thigh);
                bool rear = (thigh.transform.localPosition-first.transform.localPosition).sqrMagnitude <
                    (thigh.transform.localPosition-last.transform.localPosition).sqrMagnitude;
                Join(thigh, rear ? first : last, 10500);
                var seat = FindFaceoffSeat(feetAt[foot], 4.1f);
                if (seat) Join(foot, seat, 9800);
            }
        }
    }
}
