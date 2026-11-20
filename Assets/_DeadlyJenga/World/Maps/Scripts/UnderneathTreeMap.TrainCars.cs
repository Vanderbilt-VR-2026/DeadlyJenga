using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        enum CarKind { Tender, Coach, GiftWagon, Caboose }

        // A tender, two coaches, a wagon of presents and a caboose behind the engine.
        // They are art only, like the engine: no colliders.
        void AddTrainCars(UnderneathTreeTrain engine, Transform parent)
        {
            var size = EngineBounds(engine.transform);
            if (size.size.z < .1f)
                return;

            var paints = engine.GetComponentsInChildren<Renderer>()
                .SelectMany(r => r.sharedMaterials).Where(m => m).ToList();
            Material Paint(string label, Color fallback) =>
                paints.FirstOrDefault(m => m.name.StartsWith(label)) ?? ColoredPaper("Train " + label, fallback, .7f);
            // The model's enamel is almost black in the room light; repaint the whole train brighter.
            var red = Brighter(Paint("Cranberry enamel", new Color(.6f, .05f, .08f)), "Toy train cranberry enamel", new Color(.82f, .07f, .1f));
            var green = Brighter(Paint("Pine enamel", new Color(.05f, .3f, .15f)), "Toy train pine enamel", new Color(.07f, .46f, .22f));
            foreach (var r in engine.GetComponentsInChildren<Renderer>())
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    if (shared[i] && shared[i].name.StartsWith("Cranberry enamel")) shared[i] = red;
                    else if (shared[i] && shared[i].name.StartsWith("Pine enamel")) shared[i] = green;
                r.sharedMaterials = shared;
            }
            var brass = Paint("Polished brass", new Color(.85f, .65f, .3f));
            var black = Paint("Blackened tinplate", new Color(.06f, .06f, .07f));
            var lamp = Paint("Lamp ivory", new Color(1f, .93f, .75f));

            var cars = new (CarKind kind, Material body, float length)[]
            {
                (CarKind.Tender, green, .45f),
                (CarKind.Coach, green, .78f),
                (CarKind.GiftWagon, red, .62f),
                (CarKind.Coach, red, .78f),
                (CarKind.Caboose, red, .55f),
            };
            float engineLength = size.size.z;
            float gap = engineLength * .07f;
            float behind = -size.min.z + gap;
            var made = new Transform[cars.Length];
            var distances = new float[cars.Length];
            for (int i = 0; i < cars.Length; i++)
            {
                float length = engineLength * cars[i].length;
                var car = Child(parent, "Train Car " + (i + 1) + " " + cars[i].kind, Vector3.zero, Quaternion.identity);
                BuildCar(car, cars[i].kind, length, size, cars[i].body, red, green, brass, black, lamp);
                made[i] = car;
                distances[i] = behind + length * .5f;
                behind += length + gap;
            }
            engine.AddCars(made, distances);
        }

        void BuildCar(Transform car, CarKind kind, float length, Bounds engine, Material body,
            Material red, Material green, Material brass, Material black, Material lamp)
        {
            var parts = new Dictionary<Material, List<CombineInstance>>();
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            void Box(Material m, Vector3 center, Vector3 scale, Mesh mesh = null, Quaternion? turn = null)
            {
                if (!parts.TryGetValue(m, out var list))
                    parts[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh ? mesh : cube,
                    transform = Matrix4x4.TRS(center, turn ?? Quaternion.identity, scale) });
            }

            float width = engine.size.x * .88f;
            float height = engine.size.y;
            float floor = engine.min.y;
            float half = length * .5f;
            float wheel = height * .13f;

            // Wheels, frame and couplings shared by every car.
            var axle = Quaternion.Euler(0f, 0f, 90f);
            foreach (float side in new[] { -1f, 1f })
            foreach (float end in new[] { -1f, 1f })
            {
                var at = new Vector3(side * (width * .5f - wheel * .3f), floor + wheel, end * (half - wheel * 1.4f));
                Box(black, at, new Vector3(wheel * 2f, wheel * .25f, wheel * 2f), cylinder, axle);
                Box(brass, at + Vector3.right * side * wheel * .27f,
                    new Vector3(wheel * .7f, wheel * .05f, wheel * .7f), cylinder, axle);
            }
            float deck = floor + wheel * 1.9f;
            Box(black, new Vector3(0f, deck, 0f), new Vector3(width * .96f, height * .08f, length));
            Box(brass, new Vector3(0f, deck, half + length * .02f), new Vector3(width * .18f, height * .06f, length * .08f));
            Box(brass, new Vector3(0f, deck, -half - length * .02f), new Vector3(width * .18f, height * .06f, length * .08f));
            float top = deck + height * .04f;

            switch (kind)
            {
                case CarKind.Tender:
                {
                    float h = height * .34f;
                    Box(body, new Vector3(0f, top + h * .5f, 0f), new Vector3(width, h, length * .94f));
                    Box(brass, new Vector3(0f, top + h, 0f), new Vector3(width * 1.02f, h * .08f, length * .96f));
                    Box(black, new Vector3(0f, top + h * 1.02f, length * .05f), new Vector3(width * .84f, h * .22f, length * .7f));
                    break;
                }
                case CarKind.Coach:
                {
                    float h = height * .5f;
                    Box(body, new Vector3(0f, top + h * .5f, 0f), new Vector3(width, h, length * .96f));
                    Box(brass, new Vector3(0f, top + h * .14f, 0f), new Vector3(width * 1.02f, h * .06f, length * .97f));
                    const int windows = 4;
                    float step = length * .8f / windows;
                    for (int w = 0; w < windows; w++)
                    {
                        float z = -length * .4f + step * (w + .5f);
                        Box(lamp, new Vector3(0f, top + h * .6f, z), new Vector3(width * 1.02f, h * .32f, step * .62f));
                    }
                    Box(black, new Vector3(0f, top + h + height * .04f, 0f), new Vector3(width * 1.08f, height * .08f, length));
                    Box(black, new Vector3(0f, top + h + height * .1f, 0f), new Vector3(width * .7f, height * .05f, length * .9f));
                    break;
                }
                case CarKind.GiftWagon:
                {
                    float rail = height * .12f;
                    foreach (float side in new[] { -1f, 1f })
                        Box(brass, new Vector3(side * width * .48f, top + rail * .5f, 0f), new Vector3(width * .04f, rail, length * .95f));
                    foreach (float end in new[] { -1f, 1f })
                        Box(body, new Vector3(0f, top + rail * .5f, end * length * .46f), new Vector3(width, rail, length * .04f));
                    // Three wrapped presents riding along.
                    var gifts = new[]
                    {
                        (green, red, new Vector3(-width * .18f, 0f, -length * .24f), new Vector3(width * .5f, height * .34f, length * .36f)),
                        (red, brass, new Vector3(width * .2f, 0f, -length * .18f), new Vector3(width * .38f, height * .24f, length * .3f)),
                        (lamp, red, new Vector3(0f, 0f, length * .22f), new Vector3(width * .6f, height * .42f, length * .38f)),
                    };
                    foreach (var (wrap, ribbon, at, box) in gifts)
                    {
                        var c = at + Vector3.up * (top + box.y * .5f);
                        Box(wrap, c, box);
                        Box(ribbon, c, new Vector3(box.x * 1.02f, box.y * 1.02f, box.z * .16f));
                        Box(ribbon, c, new Vector3(box.x * .16f, box.y * 1.02f, box.z * 1.02f));
                        Box(ribbon, c + Vector3.up * box.y * .55f, new Vector3(box.x * .3f, box.y * .12f, box.z * .3f));
                    }
                    break;
                }
                case CarKind.Caboose:
                {
                    float h = height * .46f;
                    Box(body, new Vector3(0f, top + h * .5f, -length * .03f), new Vector3(width, h, length * .82f));
                    foreach (float z in new[] { -.2f, .14f })
                        Box(lamp, new Vector3(0f, top + h * .6f, length * z), new Vector3(width * 1.02f, h * .3f, length * .16f));
                    Box(black, new Vector3(0f, top + h + height * .03f, -length * .03f), new Vector3(width * 1.08f, height * .06f, length * .9f));
                    float cupola = height * .2f;
                    Box(body, new Vector3(0f, top + h + cupola * .5f, -length * .05f), new Vector3(width * .6f, cupola, length * .32f));
                    Box(lamp, new Vector3(0f, top + h + cupola * .55f, -length * .05f), new Vector3(width * .62f, cupola * .45f, length * .2f));
                    Box(black, new Vector3(0f, top + h + cupola + height * .02f, -length * .05f), new Vector3(width * .7f, height * .04f, length * .38f));
                    // Open back porch with a brass rail.
                    Box(brass, new Vector3(0f, top + h * .35f, -length * .47f), new Vector3(width * .9f, height * .03f, length * .03f));
                    foreach (float side in new[] { -1f, 1f })
                        Box(brass, new Vector3(side * width * .44f, top + h * .2f, -length * .47f), new Vector3(width * .04f, h * .4f, length * .03f));
                    break;
                }
            }

            var visual = Child(car, "Visual", Vector3.zero, Quaternion.identity);
            foreach (var pair in parts)
                JoinRailDetail(visual, pair.Key.name, pair.Value, pair.Key);
        }

        Material Brighter(Material source, string label, Color color)
        {
            var paint = new Material(source) { name = label, color = color };
            wrapping.Add(paint);
            return paint;
        }

        // The engine's size in its parent's units, measured from its meshes.
        static Bounds EngineBounds(Transform engine)
        {
            var bounds = new Bounds();
            bool first = true;
            var toEngine = engine.worldToLocalMatrix;
            foreach (var filter in engine.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh)
                    continue;
                var box = filter.sharedMesh.bounds;
                var matrix = toEngine * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = box.center + Vector3.Scale(box.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = Vector3.Scale(matrix.MultiplyPoint3x4(p), engine.localScale);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
}
