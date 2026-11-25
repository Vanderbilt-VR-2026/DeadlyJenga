using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // A falling sheet of water. It starts at a lip, falls straight down with gravity
    // and splashes on the first thing it hits (or the sea).
    public sealed class TycoonWaterStream : MonoBehaviour
    {
        const int Rows = 6;
        static Mesh sheet, ring;
        Transform splash;
        float phase;

        public bool Showing => gameObject.activeSelf;
        public float Drop { get; private set; }

        public static TycoonWaterStream Create(Transform parent, Material water, Material foam)
        {
            var go = new GameObject("Spilling water");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Sheet();
            go.AddComponent<MeshRenderer>().sharedMaterial = water;
            var stream = go.AddComponent<TycoonWaterStream>();
            var foamRing = new GameObject("Splash");
            foamRing.transform.SetParent(parent, false);
            foamRing.AddComponent<MeshFilter>().sharedMesh = Ring();
            foamRing.AddComponent<MeshRenderer>().sharedMaterial = foam;
            stream.splash = foamRing.transform;
            stream.phase = Random.value * 6;
            stream.Hide();
            return stream;
        }

        // origin: the lip in world space. outward: horizontal direction the water leaves in.
        public void Show(Vector3 origin, Vector3 outward, float width)
        {
            outward.y = 0;
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward;
            outward.Normalize();
            var start = origin + outward * .25f;
            float limit = 60;
            if (Services.World != null) limit = Mathf.Clamp(start.y - Services.World.WaterHeight(start), .2f, 60);
            int mask = ~((1 << Layers.Survivor) | (1 << Layers.Surface) | (1 << 2));
            bool landed = Physics.Raycast(start, Vector3.down, out var hit, limit, mask, QueryTriggerInteraction.Ignore);
            Drop = landed ? hit.distance : limit;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(outward, Vector3.up));
            transform.localScale = new Vector3(width, Drop, Mathf.Min(Drop, 2.5f));
            var foot = start + Vector3.down * Drop;
            if (!splash.gameObject.activeSelf) splash.gameObject.SetActive(true);
            float pulse = 1 + .12f * Mathf.Sin(Time.time * 9 + phase);
            splash.SetPositionAndRotation(foot + Vector3.up * .03f, Quaternion.identity);
            splash.localScale = new Vector3(width * 1.3f * pulse, 1, width * 1.3f * pulse);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            if (splash && splash.gameObject.activeSelf) splash.gameObject.SetActive(false);
        }

        void OnDestroy() { if (splash) Destroy(splash.gameObject); }

        // Unit sheet: 1 m wide, falls 1 m, bulging forward as it drops. Both sides are drawn.
        static Mesh Sheet()
        {
            if (sheet) return sheet;
            var v = new Vector3[(Rows + 1) * 2];
            for (int i = 0; i <= Rows; i++)
            {
                float t = i / (float)Rows, w = .5f + .12f * t;
                float z = .25f * Mathf.Sqrt(t) + .1f * t;
                v[i * 2] = new Vector3(-w, -t, z);
                v[i * 2 + 1] = new Vector3(w, -t, z);
            }
            var tris = new int[Rows * 12];
            for (int i = 0; i < Rows; i++)
            {
                int a = i * 2, k = i * 12;
                tris[k] = a; tris[k + 1] = a + 1; tris[k + 2] = a + 3;
                tris[k + 3] = a; tris[k + 4] = a + 3; tris[k + 5] = a + 2;
                tris[k + 6] = a; tris[k + 7] = a + 3; tris[k + 8] = a + 1;
                tris[k + 9] = a; tris[k + 10] = a + 2; tris[k + 11] = a + 3;
            }
            sheet = new Mesh { name = "Tycoon water sheet", vertices = v, triangles = tris };
            sheet.RecalculateNormals();
            sheet.RecalculateBounds();
            return sheet;
        }

        // Flat foam ring, radius .5 m.
        static Mesh Ring()
        {
            if (ring) return ring;
            const int sides = 20;
            var v = new Vector3[sides * 2];
            var tris = new int[sides * 12];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, r = .5f + .08f * Mathf.Sin(i * 3.1f);
                v[i * 2] = new Vector3(Mathf.Cos(a) * r * .55f, 0, Mathf.Sin(a) * r * .55f);
                v[i * 2 + 1] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                int n = (i + 1) % sides, k = i * 12;
                int[] quad = { i * 2, n * 2 + 1, i * 2 + 1, i * 2, n * 2, n * 2 + 1 };
                for (int j = 0; j < 6; j++) { tris[k + j] = quad[j]; tris[k + 11 - j] = quad[j]; }
            }
            ring = new Mesh { name = "Tycoon splash ring", vertices = v, triangles = tris };
            ring.RecalculateNormals();
            ring.RecalculateBounds();
            return ring;
        }
    }
}
