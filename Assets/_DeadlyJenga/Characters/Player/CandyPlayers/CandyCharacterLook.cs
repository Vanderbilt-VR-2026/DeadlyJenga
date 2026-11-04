using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    // Puts one character design on a candy player rig and swaps it when the design changes.
    // Head pieces are hidden from the owner's own camera (like the head itself), so hats never block the view.
    public sealed class CandyCharacterLook : MonoBehaviour
    {
        CandyPlayerRig rig;
        Transform head, torso;
        readonly List<GameObject> made = new List<GameObject>();
        readonly List<Renderer> headParts = new List<Renderer>();
        int current = int.MinValue;

        public int Design => current;

        public static CandyCharacterLook For(CandyPlayerRig rig)
        {
            var look = rig.GetComponent<CandyCharacterLook>();
            if (!look) look = rig.gameObject.AddComponent<CandyCharacterLook>();
            look.rig = rig;
            return look;
        }

        public void Apply(int design)
        {
            if (design < 0 || design >= CandyCharacterDesigns.Count) design = 0;
            if (design == current) return;
            current = design;
            foreach (var go in made) if (go) Destroy(go);
            made.Clear(); headParts.Clear();
            if (!rig) rig = GetComponent<CandyPlayerRig>();
            if (!rig) return;
            if (!head) head = Find(rig.transform, "Head");
            if (!torso) torso = Find(rig.transform, "Torso");
            var candy = rig.style ? rig.style.material : null;
            CandyCharacterDesigns.Build(design, new CandyDesignParts(head, torso, candy, made, headParts));
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found) return found;
            }
            return null;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            SetHead(ShadowCastingMode.On);
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (rig && rig.ownerCamera && camera == rig.ownerCamera) SetHead(ShadowCastingMode.ShadowsOnly);
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera) => SetHead(ShadowCastingMode.On);
        void SetHead(ShadowCastingMode mode)
        {
            foreach (var r in headParts) if (r && r.shadowCastingMode != mode) r.shadowCastingMode = mode;
        }
    }
}
