using System.Collections.Generic;
using UnityEngine;
using F = DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    // A Hall of Fame piston head: a round, rank-coloured drum on top of a winners' step that pivots
    // 60 degrees back and forth (MayhemMarina.FamePivot). Three spotlights pop up out of its rim when
    // the lobby opens and stay aimed at the statue standing on it.
    public sealed class MarinaFamePiston : MonoBehaviour
    {
        const float Radius = .47f, LampRing = .4f, PopHeight = .13f, PopTime = 1.1f;
        static readonly Vector3 StatueChest = new(0, 1.45f, 0);

        int step;
        Quaternion rest;
        float startTime;
        readonly List<Transform> lamps = new();
        readonly List<Light> lights = new();
        readonly List<MarinaMesh> meshes = new();

        public static MarinaFamePiston Build(Transform parent, int step, Vector3 top, Quaternion facing, F finish)
        {
            var root = new GameObject("Hall of Fame piston " + (step + 1)).transform;
            root.SetParent(parent, false);
            root.localPosition = top;
            root.localRotation = facing;
            var piston = root.gameObject.AddComponent<MarinaFamePiston>();
            piston.step = step;
            piston.rest = facing;

            float h = MayhemMarina.FameStepCap;
            var drum = new MarinaMesh();
            drum.Disk(Vector3.zero, Radius, h, F.Ivory, false, 40);
            drum.Disk(new Vector3(0, h - .035f, 0), Radius + .01f, .035f, finish, false, 40);
            drum.ArcSlab(new Vector3(0, .045f, 0), Vector2.one * Radius, Vector2.one * (Radius + .012f), .02f, 0, 360, F.Glow, false, 40);
            drum.Box(new Vector3(0, h + .002f, -Radius + .08f), new Vector3(.12f, .006f, .05f), F.Glow, .002f);
            drum.Flush(root, 0);
            piston.meshes.Add(drum);

            for (int i = 0; i < 3; i++)
            {
                float a = (60f + i * 120f) * Mathf.Deg2Rad;
                var spot = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * LampRing;
                var lamp = new GameObject("Pop-up light " + (i + 1)).transform;
                lamp.SetParent(root, false);
                lamp.localPosition = spot + Vector3.up * h;
                var aim = Quaternion.LookRotation(StatueChest - lamp.localPosition);

                var body = new MarinaMesh();
                body.Disk(Vector3.zero, .05f, .015f, F.Metal, false, 16);
                body.Beam(Vector3.zero, Vector3.up * .08f, .014f, F.Metal);
                var head = Vector3.up * .1f;
                body.Box(head, new Vector3(.09f, .07f, .09f), F.DarkWood, .02f, false, aim);
                body.Box(head + aim * Vector3.forward * .047f, new Vector3(.07f, .05f, .01f), F.Glow, .01f, false, aim);
                body.Flush(lamp, 0);
                piston.meshes.Add(body);

                var light = new GameObject("Spotlight").AddComponent<Light>();
                light.transform.SetParent(lamp, false);
                light.transform.localPosition = head;
                light.transform.localRotation = aim;
                light.type = LightType.Spot;
                light.spotAngle = 58f;
                light.innerSpotAngle = 30f;
                light.range = 3.6f;
                light.intensity = 0f;
                light.color = new Color(1f, .93f, .82f);
                light.shadows = LightShadows.None;
                piston.lamps.Add(lamp);
                piston.lights.Add(light);
            }
            return piston;
        }

        void OnEnable() => startTime = Time.time;

        void Update()
        {
            transform.localRotation = rest * Quaternion.Euler(0, MayhemMarina.FamePivot(step, Time.time), 0);
            // Pop up once: lamps rise out of the rim and fade on over about a second.
            float pop = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - startTime - step * .25f) / PopTime));
            float h = MayhemMarina.FameStepCap;
            for (int i = 0; i < lamps.Count; i++)
            {
                var p = lamps[i].localPosition;
                lamps[i].localPosition = new Vector3(p.x, h - PopHeight * (1 - pop), p.z);
                lights[i].intensity = 2.4f * pop;
            }
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) mesh.Dispose();
        }
    }
}
