using System.Collections.Generic;
using System.IO;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyArsenalEffects
    {
        sealed class StormStroke
        {
            public MeshFilter filter;
            public MeshRenderer renderer;
            public Vector3 start, end;
            public float born, travel;
            public bool hit;
        }

        static readonly string[] StormStages =
        {
            "Storm Axe blade current", "Storm Axe release", "Storm Axe airborne tearing slash", "Storm Axe electrical contact"
        };

        readonly List<StormStroke> stormStrokes = new();
        readonly Stack<StormStroke> spentStormStrokes = new();
        MaterialPropertyBlock stormTint;
        Mesh[] stormMeshes;

        bool LoadStormMeshes()
        {
            if (stormMeshes != null) return true;
            var asset = Resources.Load<TextAsset>("StormAxeEffects");
            if (!asset) return false;
            using var reader = new BinaryReader(new MemoryStream(asset.bytes, false));
            if (reader.ReadUInt32() != 0x58464153) return false;
            var ready = new Mesh[StormStages.Length];
            for (int stage = 0; stage < ready.Length; stage++)
            {
                int count = reader.ReadInt32();
                int indexCount = reader.ReadInt32();
                if (count <= 0 || count > ushort.MaxValue || indexCount <= 0 || indexCount > 400000 || indexCount % 3 != 0)
                {
                    foreach (var mesh in ready) if (mesh) Destroy(mesh);
                    return false;
                }
                var positions = new Vector3[count];
                var colors = new Color[count];
                var uv = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    colors[i] = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    uv[i] = Vector2.one * .5f;
                }
                var indices = new int[indexCount];
                for (int i = 0; i < indices.Length; i++) indices[i] = reader.ReadUInt16();
                var model = new Mesh { name = StormStages[stage], vertices = positions, colors = colors, uv = uv };
                model.SetTriangles(indices, 0);
                model.UploadMeshData(true);
                ready[stage] = model;
            }
            stormMeshes = ready;
            return true;
        }

        StormStroke NewStormStroke(Transform parent)
        {
            var node = new GameObject("Storm Axe energy", typeof(MeshFilter), typeof(MeshRenderer));
            node.transform.SetParent(parent, false);
            var renderer = node.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ordnanceRibbon;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new StormStroke { filter = node.GetComponent<MeshFilter>(), renderer = renderer };
        }

        void UpdateStormCharge(WeaponEffect effect, EquipmentState state)
        {
            bool visible = effect.model && effect.model.gameObject.activeInHierarchy && state.drawn &&
                CandyWeaponImpacts.VisibleToAnyCamera(effect.model.position, 42);
            if (!visible)
            {
                if (effect.stormCharge != null) effect.stormCharge.renderer.enabled = false;
                return;
            }
            if (!LoadStormMeshes()) return;
            if (effect.stormCharge == null || !effect.stormCharge.filter)
            {
                effect.stormCharge = NewStormStroke(effect.model);
                effect.stormCharge.filter.sharedMesh = stormMeshes[0];
                effect.stormCharge.filter.name = StormStages[0];
            }
            var charge = effect.stormCharge.renderer;
            charge.enabled = true;
            float amount = Mathf.Lerp(.49f, 1, state.charge) * (.83f + .17f * Mathf.Sin(Time.time * 28));
            SetStormOpacity(charge, amount);
        }

        void PresentStormShot(EquipmentActionFrame action)
        {
            if (action.remaining < .55f || action.endpoints == null || action.endpoints.Length == 0 ||
                !CandyWeaponImpacts.VisibleToAnyCamera(action.position, 55) || !LoadStormMeshes()) return;
            var travel = action.endpoints[0] - action.position;
            if (travel.sqrMagnitude < .0001f) return;
            if (stormStrokes.Count >= 36) RetireStormStroke(0);
            var stroke = spentStormStrokes.Count > 0 ? spentStormStrokes.Pop() : NewStormStroke(transform);
            stroke.start = action.position;
            stroke.end = action.endpoints[0];
            stroke.born = Time.time;
            stroke.travel = action.shotgun ? Mathf.Clamp(travel.magnitude / 76, .11f, .32f) : .13f;
            stroke.hit = travel.magnitude < (action.shotgun ? 35 : EquipmentSpecifications.Get(EquipmentKind.LaserAxe).range) - .04f;
            stroke.filter.transform.SetPositionAndRotation(stroke.start, Quaternion.LookRotation(travel));
            stroke.filter.gameObject.SetActive(true);
            stormStrokes.Add(stroke);
            SetStormStage(stroke, 1, 1);
        }

        void UpdateStormStrokes()
        {
            for (int i = stormStrokes.Count - 1; i >= 0; i--)
            {
                var stroke = stormStrokes[i];
                float age = Time.time - stroke.born;
                float progress = age / stroke.travel;
                if (progress < 1)
                {
                    int stage = progress < .27f ? 1 : 2;
                    stroke.filter.transform.position = Vector3.LerpUnclamped(stroke.start, stroke.end, progress);
                    SetStormStage(stroke, stage, 1 - .24f * progress);
                    continue;
                }
                float contactAge = (age - stroke.travel) / .26f;
                if (!stroke.hit || contactAge >= 1)
                {
                    RetireStormStroke(i);
                    continue;
                }
                stroke.filter.transform.position = stroke.end;
                stroke.filter.transform.localScale = Vector3.one * Mathf.Lerp(1, 2.1f, contactAge);
                SetStormStage(stroke, 3, 1 - contactAge);
            }
        }

        void SetStormStage(StormStroke stroke, int stage, float alpha)
        {
            if (stroke.filter.sharedMesh != stormMeshes[stage])
            {
                stroke.filter.sharedMesh = stormMeshes[stage];
                stroke.filter.name = StormStages[stage];
            }
            SetStormOpacity(stroke.renderer, alpha);
        }

        void SetStormOpacity(MeshRenderer renderer, float alpha)
        {
            stormTint ??= new MaterialPropertyBlock();
            stormTint.SetColor("_BaseColor", new Color(1, 1, 1, alpha));
            renderer.SetPropertyBlock(stormTint);
        }

        void RetireStormStroke(int i)
        {
            var stroke = stormStrokes[i];
            stroke.filter.transform.localScale = Vector3.one;
            stroke.filter.gameObject.SetActive(false);
            spentStormStrokes.Push(stroke);
            stormStrokes.RemoveAt(i);
        }
    }
}
