using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Wildlife
{
    // Ambient wildlife belongs to this island instance. The walking loops stay on its flat crown.
    public sealed class ShrinePandaGrove : MonoBehaviour
    {
        [SerializeField] Material coat;
        static readonly string[] Builds = { "Original", "Willow", "Boulder", "Dumpling", "Cub" };
        static readonly Vector2[] Centers = { new(-5.9f,-2.6f), new(-2.8f,.05f), new(.15f,-3.8f), new(3.5f,-.45f), new(6.4f,-3.25f) };
        readonly Resident[] residents = new Resident[5];
        Transform population;
        float shoreHeight;

        void Start()
        {
            shoreHeight = (Services.World?.WaterHeight(transform.position) ?? transform.position.y + .08f)
                + transform.lossyScale.y * .53f;
            population = new GameObject("Bamboo island pandas").transform;
            population.SetParent(transform.parent.parent,false);
            population.position = new Vector3(transform.position.x, shoreHeight, transform.position.z);
            population.rotation = transform.rotation;
            for (int i = 0; i < Builds.Length; i++)
            {
                string path = "ShrinePandas/" + Builds[i];
                var prefab = Resources.Load<GameObject>(path);
                if (!prefab) { Debug.LogError("Missing grove panda: " + Builds[i],this); continue; }
                var model = Instantiate(prefab,population);
                model.name = Builds[i];
                var animation = model.GetComponentInChildren<Animation>();
                var clip = animation ? animation.clip : null;
                if (!clip)
                    foreach (var take in Resources.LoadAll<AnimationClip>(path))
                        if (!take.name.StartsWith("__preview__")) { clip = take; break; }
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (!clip || !skin || !coat)
                {
                    Debug.LogError("Incomplete grove panda asset: " + Builds[i],model);
                    Destroy(model);
                    continue;
                }
                if (animation) { animation.Stop(); animation.enabled = false; }
                skin.sharedMaterial = coat;
                residents[i] = new Resident(model,clip,skin,i);
            }
        }

        void Update()
        {
            foreach (var resident in residents) resident?.Step(Time.deltaTime);
        }

        void OnEnable() { if (population) population.gameObject.SetActive(true); }
        void OnDisable() { if (population) population.gameObject.SetActive(false); }

        void OnDestroy()
        {
            if (population) Destroy(population.gameObject);
        }

        sealed class Resident
        {
            enum Activity { Walk, Sit, Eat, Stand }
            readonly GameObject model;
            readonly AnimationClip clip;
            readonly int index;
            readonly float radius, speed;
            readonly Quaternion facingCorrection;
            Activity activity;
            float angle, elapsed, duration, stride;
            int stops;

            public Resident(GameObject model, AnimationClip clip, SkinnedMeshRenderer skin, int index)
            {
                this.model = model;
                this.clip = clip;
                this.index = index;
                radius = index == 4 ? .82f : 1.20f;
                speed = index == 4 ? .42f : .55f + .055f * index;
                angle = index * 2.39996f;
                duration = 4.1f + index * 1.9f;
                Sample(0);
                // Use the imported walking pose to account for FBX forward-axis conversion.
                Transform head = null;
                foreach (var bone in skin.bones)
                {
                    if (bone.name == "Head") head = bone;
                }
                Vector3 forward = Vector3.forward;
                if (head)
                {
                    var local = model.transform.InverseTransformPoint(head.position);
                    local.x = 0;
                    local.y = 0;
                    if (local.sqrMagnitude > .001f) forward = local.normalized;
                }
                facingCorrection = Quaternion.Inverse(Quaternion.LookRotation(forward));
                Place();
            }

            void Place()
            {
                var center = Centers[index];
                var direction = index % 2 == 0 ? 1 : -1;
                model.transform.localPosition = new Vector3(center.x + radius*Mathf.Cos(angle),0,center.y + radius*Mathf.Sin(angle));
                var tangent = direction * new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle));
                model.transform.localRotation = Quaternion.LookRotation(tangent) * facingCorrection;
            }

            void Sample(float frame) => clip.SampleAnimation(model,clip.length * Mathf.Clamp01(frame / 96f));

            public void Step(float dt)
            {
                elapsed += dt;
                if (activity == Activity.Walk)
                {
                    angle += (index % 2 == 0 ? 1 : -1) * speed * dt / radius;
                    float previous = stride;
                    stride = Mathf.Repeat(stride + dt * speed / (index == 4 ? .39f : .72f),1);
                    Sample(stride * 24);
                    Place();
                    // Begin lowering at the matching gait contact, avoiding a pose snap.
                    if (elapsed >= duration && stride < previous) Begin(Activity.Sit,1.1f);
                    return;
                }
                if (activity == Activity.Sit)
                {
                    Sample(24 + 24*Mathf.Min(1,elapsed/duration));
                    if (elapsed >= duration) Begin(Activity.Eat,4 + (index + stops) % 4);
                }
                else if (activity == Activity.Eat)
                {
                    Sample(48 + 24*Mathf.Repeat(elapsed,1));
                    if (elapsed >= duration) Begin(Activity.Stand,1.15f);
                }
                else
                {
                    Sample(72 + 24*Mathf.Min(1,elapsed/duration));
                    if (elapsed >= duration)
                    {
                        stops++;
                        stride = 0;
                        Begin(Activity.Walk,6 + (index*3 + stops*5) % 8);
                    }
                }
            }

            void Begin(Activity next,float seconds)
            {
                activity = next;
                duration = seconds;
                elapsed = 0;
            }
        }
    }
}
