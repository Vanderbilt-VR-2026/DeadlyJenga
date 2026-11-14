using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Looks only. While the local player sinks into sand, draws sand granules over the view:
    // they creep in from the bottom and sides, then pack solid as the eyes go under.
    // One small quad in front of the camera (works in VR and on desktop).
    public sealed class CandySandEngulf : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.SessionStarted -= Attach; GameEvents.SessionStarted += Attach;
        }
        static void Attach(GameObject root)
        {
            if (root && !root.GetComponent<CandySandEngulf>()) root.AddComponent<CandySandEngulf>();
        }

        const float Distance = .3f, Size = 1.1f;
        GameObject quad; Material material; Mesh mesh;
        ISandState sand; GameObject sandOwner;
        float level;

        void OnDestroy()
        {
            if (quad) Destroy(quad);
            if (material) Destroy(material);
            if (mesh) Destroy(mesh);
        }

        void LateUpdate()
        {
            var session = Services.Session;
            var player = session?.Player;
            if (player != null && sandOwner != player.gameObject)
            {
                sandOwner = player.gameObject;
                sand = sandOwner ? sandOwner.GetComponent<ISandState>() : null;
            }
            float target = sand != null && sandOwner && sand.InSand ? Mathf.Clamp01(sand.SandDepth) : 0;
            // Rise quickly as the player sinks, clear quickly after the escape.
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * (target > level ? 2.5f : 1.8f));
            if (level <= .01f) { if (quad) quad.SetActive(false); return; }

            var eye = session?.ActiveCamera;
            if (!eye) return;
            if (!quad) Build();
            if (!quad) return;
            if (quad.transform.parent != eye.transform)
            {
                quad.transform.SetParent(eye.transform, false);
                quad.transform.localPosition = new Vector3(0, 0, Distance);
                quad.transform.localRotation = Quaternion.identity;
            }
            quad.SetActive(true);
            material.SetFloat("_Level", level);
        }

        void Build()
        {
            var shader = Resources.Load<Shader>("CandySandEngulf");
            if (!shader) return;
            mesh = new Mesh { name = "Sand engulf quad" };
            float h = Size * .5f;
            mesh.vertices = new[] { new Vector3(-h, -h), new Vector3(h, -h), new Vector3(-h, h), new Vector3(h, h) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(Size, Size, .1f));
            material = new Material(shader) { name = "Sand engulf" };
            quad = new GameObject("Sand engulf");
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = quad.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
    }
}
