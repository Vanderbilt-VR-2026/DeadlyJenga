using System;
using System.Collections.Generic;
using DeadlyJenga.CandyPlayers;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    sealed class CaramelRabbitPreview : IDisposable
    {
        readonly List<Object> assets = new List<Object>();

        readonly Transform[] ears = new Transform[2];
        Transform head;
        Quaternion headRest;

        public void Apply(CandyPlayerRig rig, bool refined = false)
        {
            float coatMask = refined ? 1f : .87f;
            var caramel = Glaze("Caramel", new Color(.84f, .44f, .14f),
                refined ? .80f : .73f, refined ? .94f : .97f, .18f, coatMask);
            var honey = Glaze("Honey caramel", new Color(.99f, .63f, .27f),
                refined ? .74f : .66f, .94f, .13f, coatMask);
            var cream = Glaze("Vanilla toffee", new Color(1f, .91f, .71f),
                refined ? .71f : .65f, .93f, .13f, coatMask);
            var dark = Glaze("Burnt sugar", refined ? new Color(.075f, .032f, .021f) : new Color(.12f, .046f, .029f),
                .74f, .94f, .04f, coatMask);

            Use(rig.bodyRenderer, caramel);
            Use(rig.headRenderer, caramel);
            Sculpt(rig.bodyRenderer, false);
            Sculpt(rig.headRenderer, true);
            foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                if (name == "Visual - rabbit cream belly inlay")
                {
                    var inlay = new Material(cream) { name = "Vanilla belly inlay" };
                    assets.Add(inlay);
                    inlay.SetTexture("_BaseMap", renderer.sharedMaterial.GetTexture("_BaseMap"));
                    inlay.SetFloat("_AlphaClip", 1f);
                    inlay.SetFloat("_Cutoff", .5f);
                    inlay.EnableKeyword("_ALPHATEST_ON");
                    inlay.SetOverrideTag("RenderType", "TransparentCutout");
                    inlay.renderQueue = (int)RenderQueue.AlphaTest;
                    Use(renderer, inlay);
                    if (renderer is SkinnedMeshRenderer fitted) Sculpt(fitted, false);
                }
                else if (name.StartsWith("Visual - Rabbit ", StringComparison.Ordinal) ||
                         name.StartsWith("Visual - rabbit ", StringComparison.Ordinal))
                    renderer.enabled = false;
            }

            head = rig.HeadAnchor;
            headRest = head.localRotation;
            head.localScale = Vector3.Scale(head.localScale, new Vector3(1.17f, 1.05f, 1.08f));
            for (int side = -1; side <= 1; side += 2)
            {
                var earCenter = new Vector3(side * .158f, side < 0 ? .368f : .394f, -.033f);
                var tilt = Quaternion.Euler(side < 0 ? -12f : 6f, side * 9f, -side * (side < 0 ? 29f : 20f));
                var radii = new Vector3(.101f, side < 0 ? .244f : .269f, .071f);
                var pivot = new GameObject("Ear hinge").transform;
                pivot.SetParent(head, false);
                pivot.localPosition = earCenter - tilt * (Vector3.up * radii.y * .77f);
                ears[side < 0 ? 0 : 1] = pivot;
                var shellCenter = earCenter - pivot.localPosition;
                Mold("Caramel ear", pivot, caramel, shellCenter, radii, tilt);
                Mold("Honey ear inset", pivot, honey,
                    shellCenter + tilt * new Vector3(0f, .018f, .057f),
                    new Vector3(.058f, radii.y * .68f, .022f), tilt);
                if (!refined)
                    Mold("Vanilla muzzle", head, cream,
                        new Vector3(side * .055f, -.087f, .195f), new Vector3(.080f, .064f, .062f));
                var eye = new Vector3(side * .088f, .052f, .193f);
                Mold("Burnt sugar eye", head, dark, eye,
                    refined ? new Vector3(.030f, .039f, .025f) : new Vector3(.028f, .036f, .023f));
                if (refined)
                    Mold("Eye catchlight", head, cream, eye + new Vector3(-.008f, .013f, .021f),
                        new Vector3(.0065f, .0075f, .0035f));
                var paw = rig.AppearanceBone(side < 0 ? 10 : 13);
                Mold("Caramel paw", paw, caramel,
                    new Vector3(0f, -.004f, .063f), new Vector3(.114f, .075f, .166f));
            }
            if (refined)
            {
                Mold("Vanilla muzzle", head, cream, new Vector3(0f, -.085f, .194f), new Vector3(.129f, .067f, .067f));
                AddSmile(head, Glaze("Toffee smile", new Color(.46f, .22f, .071f), .38f, .75f, .12f, .30f));
            }
            Mold("Burnt sugar nose", head, dark,
                new Vector3(0f, -.052f, .252f), new Vector3(.049f, .032f, .023f));
            Mold("Caramel button tail", rig.BodyAnchor, caramel,
                new Vector3(0f, .705f, -.17f), new Vector3(.097f, .100f, .103f));
        }

        public void ShowcasePose(float phase)
        {
            var lift = Mathf.Sin(Mathf.PI * Mathf.Clamp01(phase));
            head.localRotation = headRest * Quaternion.Euler(-4f * lift, 13f * lift, -12f * lift);
            ears[0].localRotation = Quaternion.Euler(0f, 0f, -14f * Flick(phase, .10f));
            ears[1].localRotation = Quaternion.Euler(6f * Flick(phase, .22f), 0f, 19f * Flick(phase, .22f));
        }

        static float Flick(float phase, float delay)
        {
            var t = Mathf.Clamp01((phase - delay) / (1f - delay));
            return Mathf.Sin(Mathf.PI * t) * Mathf.Sin(2f * Mathf.PI * t);
        }

        void AddSmile(Transform anchor, Material material)
        {
            const int segments = 18, sides = 8;
            var vertices = new Vector3[(segments + 1) * sides];
            var triangles = new int[segments * sides * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = Mathf.Lerp(-.052f, .063f, t);
                float y = -.120f + .043f * Mathf.Pow(t - .43f, 2f);
                float horizontal = x / .129f, vertical = (y + .085f) / .067f;
                float z = .194f + .067f * Mathf.Sqrt(1f - horizontal * horizontal - vertical * vertical);
                float radius = .0047f * Mathf.Sin(Mathf.PI * (.05f + .90f * t));
                for (int side = 0; side < sides; side++)
                {
                    float angle = 2f * Mathf.PI * side / sides;
                    vertices[i * sides + side] = new Vector3(x, y + Mathf.Cos(angle) * radius, z + Mathf.Sin(angle) * radius);
                    if (i == segments) continue;
                    int a = i * sides + side, b = i * sides + (side + 1) % sides, k = a * 6;
                    triangles[k] = a; triangles[k + 1] = b; triangles[k + 2] = a + sides;
                    triangles[k + 3] = b; triangles[k + 4] = b + sides; triangles[k + 5] = a + sides;
                }
            }
            var mesh = new Mesh { name = "Toffee smile", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            assets.Add(mesh);
            var part = new GameObject("Toffee smile");
            part.transform.SetParent(anchor, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        void Sculpt(SkinnedMeshRenderer renderer, bool head)
        {
            var mesh = Object.Instantiate(renderer.sharedMesh);
            mesh.name += " (caramel mold)";
            assets.Add(mesh);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                var vertex = vertices[i];
                float height = head ? (vertex.y - 1.408f) / .094f : (vertex.y - .91f) / .20f;
                float width = Mathf.Exp(-height * height);
                if (!head)
                    width *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.20f, .25f, Mathf.Abs(vertex.x)));
                float x = 1f + width * (head ? .18f : .22f);
                float z = 1f + width * (head ? .08f : .14f);
                vertices[i] = new Vector3(vertex.x * x, vertex.y, vertex.z * z);
                normals[i] = new Vector3(normals[i].x / x, normals[i].y, normals[i].z / z).normalized;
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            renderer.sharedMesh = mesh;
        }

        Material Glaze(string name, Color color, float smoothness, float coat, float shadowLift, float coatMask)
        {
            var shader = Shader.Find("Universal Render Pipeline/Complex Lit");
            if (!shader || !shader.isSupported) shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name };
            assets.Add(material);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color.linear * shadowLift);
            if (material.HasProperty("_ClearCoatMask"))
            {
                material.SetFloat("_ClearCoat", 1f);
                material.SetFloat("_ClearCoatMask", coatMask);
                material.SetFloat("_ClearCoatSmoothness", coat);
                material.EnableKeyword("_CLEARCOAT");
            }
            return material;
        }

        static void Mold(string name, Transform anchor, Material material, Vector3 center, Vector3 radii,
            Quaternion? rotation = null)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            part.transform.SetParent(anchor, false);
            part.transform.SetLocalPositionAndRotation(center, rotation ?? Quaternion.identity);
            part.transform.localScale = radii * 2f;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Use(Renderer renderer, Material material)
        {
            renderer.SetPropertyBlock(null);
            renderer.sharedMaterial = material;
        }

        public void Dispose()
        {
            foreach (var asset in assets)
                if (asset) Object.Destroy(asset);
            assets.Clear();
        }
    }
}
