using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Characters
{
    // The three shoreline routes live on the Shrine Town prefab. Only these actors use this water motor.
    public sealed class ShrineSamuraiPatrol : MonoBehaviour
    {
        [System.Serializable]
        sealed class Route
        {
            public Transform island;
            // Object, not GameObject: Unity 6.3 can store a model reference as its Prefab asset.
            public Object model;
            public float phase;
        }

        [SerializeField] Route[] routes;
        [SerializeField] Material armor;
        Guard[] guards;
        IWorldQuery ownMap;

        void Start()
        {
            ownMap = GetComponent<IWorldQuery>();
            guards = new Guard[routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (!route.model) { Debug.LogWarning("Shrine Town swimmer has no model assigned.", this); continue; }
                var root = new GameObject(route.model.name).transform;
                root.SetParent(transform, false);

                var made = Instantiate(route.model, root);
                var model = made as GameObject;
                if (!model && made is Component part) model = part.gameObject;
                if (!model)
                {
                    Debug.LogWarning("Shrine Town swimmer model is not a GameObject: " + route.model.name, this);
                    if (made) Destroy(made);
                    Destroy(root.gameObject);
                    continue;
                }
                var player = model.GetComponentInChildren<Animation>();
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (!player || !player.clip || !skin)
                {
                    Debug.LogError("Incomplete Shrine Town swimmer: " + route.model.name, this);
                    Destroy(root.gameObject);
                    continue;
                }
                skin.sharedMaterial = armor;
                guards[i] = new Guard(root, route, player, skin);
                skin.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (guards == null || ownMap == null || !ReferenceEquals(ownMap, Services.World)) return;
            foreach (var guard in guards) guard?.Step(Services.World, Time.timeSinceLevelLoad);
        }

        sealed class Guard
        {
            const float Cycle = 23;
            readonly Transform root;
            readonly Route route;
            readonly SkinnedMeshRenderer skin;
            readonly Animation player;
            readonly PhysicsScene physics;
            readonly Quaternion facing;
            readonly Vector3 localStart, localEnd;
            readonly AnimationState walk, swim, tread;
            bool positioned;
            float immersion;

            public Guard(Transform root, Route route, Animation player, SkinnedMeshRenderer skin)
            {
                this.root = root;
                this.route = route;
                this.skin = skin;
                this.player = player;
                physics = root.gameObject.scene.GetPhysicsScene();
                player.playAutomatically = false;
                player.Stop();
                var take = player.clip;
                // The Blender take contains an in-place stride, surface stroke and tread at 24 fps.
                AnimationState Segment(string name, int first, int last)
                {
                    float endFrame = take.frameRate * take.length;
                    player.AddClip(take, name, Mathf.RoundToInt(first * endFrame / 118),
                        Mathf.RoundToInt(last * endFrame / 118));
                    var state = player[name];
                    state.wrapMode = WrapMode.Loop;
                    state.enabled = true;
                    state.speed = 0;
                    state.weight = 0;
                    return state;
                }
                walk = Segment("Shore walk", 0, 28);
                swim = Segment("Surface swim", 29, 69);
                tread = Segment("Tread water", 70, 118);
                Transform head = System.Array.Find(skin.bones, b => b.name == "Head");
                Transform hips = System.Array.Find(skin.bones, b => b.name == "Hips");
                walk.weight = 1;
                player.Sample();
                float torso = Vector3.Distance(head.position, hips.position);
                root.localScale = Vector3.one * (.68f / Mathf.Max(.001f, torso));
                walk.weight = 0;
                swim.weight = 1;
                player.Sample();
                Vector3 direction = root.InverseTransformDirection(head.position - hips.position);
                direction.y = 0;
                facing = Quaternion.Inverse(Quaternion.LookRotation(direction.normalized));
                // Bake the lane into map-local space so the map can be moved or rebuilt.
                var island = root.parent.InverseTransformPoint(route.island.position);
                var inward = new Vector3(-island.x, 0, -island.z).normalized;
                var radii = route.island.localScale;
                float shore = 1 / Mathf.Sqrt(Mathf.Pow(inward.x / radii.x, 2) + Mathf.Pow(inward.z / radii.z, 2));
                localStart = island + inward * (.48f * shore);
                localEnd = island + inward * (shore + 7);
            }

            public void Step(IWorldQuery world, float time)
            {
                if (!positioned) skin.enabled = true;
                float t = Mathf.Repeat(time + route.phase, Cycle);
                const float leg = 10;
                float travel = t < leg ? t / leg : t < 13 ? 1 : 1 - (t - 13) / leg;
                var point = root.parent.TransformPoint(Vector3.Lerp(localStart, localEnd, travel));
                float water = world.WaterHeight(point);
                var origin = new Vector3(point.x, water + 4, point.z);
                bool supported = physics.Raycast(origin, Vector3.down, out var hit, 7,
                    (1 << Layers.Surface) | (1 << Layers.Block) | (1 << 0),
                    QueryTriggerInteraction.Ignore);
                float bottom = supported ? hit.point.y : water - 2;
                float wet = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.09f, .72f, water - bottom));
                // The swim rig is authored around the waterline, the walk rig around its soles.
                float height = Mathf.Lerp(bottom, water, wet);
                point.y = positioned ? Mathf.MoveTowards(root.position.y, height, 3 * Time.deltaTime) : height;
                immersion = positioned ? Mathf.MoveTowards(immersion, wet, 2 * Time.deltaTime) : wet;
                positioned = true;
                float rest = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(9.7f, 10.4f, t)) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12.6f, 13.3f, t)));
                Vector3 outwards = root.parent.TransformDirection(localEnd-localStart);
                float turn = 180 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(10, 13, t)) +
                    180 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22.2f, 23, t));
                root.SetPositionAndRotation(point, Quaternion.AngleAxis(turn, Vector3.up) *
                    Quaternion.LookRotation(outwards) * facing);
                walk.weight = 1 - immersion;
                swim.weight = immersion * (1 - rest);
                tread.weight = immersion * rest;
                walk.time = Mathf.Repeat(time + route.phase, walk.length);
                swim.time = Mathf.Repeat(time + route.phase, swim.length);
                tread.time = Mathf.Repeat(time + route.phase, tread.length);
                player.Sample();
            }
        }
    }
}
