using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CastleBlock : MonoBehaviour
    {
        public const int SurvivorLayer = 8, BlockLayer = 9, SurfaceLayer = 10;
        public CastleBlockAppearance appearance;
        public Transform visualRoot;
        public Material sectionMaterial;
        GameObject currentVisual, currentPrefab;
        CastleBlockAppearance currentAppearance;
        Material currentMaterial;
        BoxCollider surface;
        Rigidbody surfaceBody;
        static bool collisionLayersConfigured;
        static readonly System.Collections.Generic.Dictionary<Collider,CastleBlock> SurfaceOwners=new System.Collections.Generic.Dictionary<Collider,CastleBlock>();
        public static bool TryGetSurfaceOwner(Collider collider,out CastleBlock owner)=>SurfaceOwners.TryGetValue(collider,out owner);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { collisionLayersConfigured = false;SurfaceOwners.Clear(); }

        void Awake()
        {
            if (!Application.isPlaying) return;
            // CharacterController sweeps use the collision matrix as well as collider overrides.
            if (!collisionLayersConfigured)
            {
                Physics.IgnoreLayerCollision(SurvivorLayer, BlockLayer, true);
                for (int layer = 0; layer < 32; layer++)
                    Physics.IgnoreLayerCollision(SurfaceLayer, layer, layer != SurvivorLayer);
                collisionLayersConfigured = true;
            }
            gameObject.layer = BlockLayer;
            GetComponent<BoxCollider>().excludeLayers = (1 << SurvivorLayer) | (1 << SurfaceLayer);
            // An independent kinematic copy provides one-way player collision. It can never
            // transfer character forces to the dynamic crate; the hand hits the real body.
            var proxy = new GameObject(name + " [survivor surface]");
            SceneManager.MoveGameObjectToScene(proxy, gameObject.scene);
            proxy.layer = SurfaceLayer;
            surfaceBody = proxy.AddComponent<Rigidbody>();
            surfaceBody.isKinematic = true; surfaceBody.useGravity = false;
            surface = proxy.AddComponent<BoxCollider>();
            SurfaceOwners[surface]=this;
            surface.excludeLayers = ~(1 << SurvivorLayer);
            surface.layerOverridePriority = 10;
            SyncSurface();
        }
        void OnEnable() { if (surface) surface.gameObject.SetActive(true); }
        void OnDisable() { if (surface) surface.gameObject.SetActive(false); }
        void OnDestroy() { if (surface) {SurfaceOwners.Remove(surface);Destroy(surface.gameObject);} }
        public void SyncSurface()
        {
            if (!surface) return;
            var box = GetComponent<BoxCollider>();
            surfaceBody.position = transform.position; surfaceBody.rotation = transform.rotation;
            surface.transform.localScale = transform.lossyScale;
            surface.center = box.center; surface.size = box.size;
        }
        void FixedUpdate() { SyncSurface(); }
        void Update() { SyncSurface(); RefreshAppearance(); }
        public void RefreshAppearance()
        {
            if (!visualRoot || !appearance) return;
            var prefab = appearance.visualPrefab;
            if (currentAppearance != appearance || currentPrefab != prefab)
            {
                foreach (Transform child in visualRoot)
                {
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
                }
                currentVisual = prefab ? Instantiate(prefab, visualRoot) : null;
                if (currentVisual)
                {
                    currentVisual.transform.localPosition = Vector3.zero;
                    currentVisual.transform.localRotation = Quaternion.identity;
                    // Imported artwork must not change the collider or mass of the crate.
                    foreach (var col in currentVisual.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                    foreach (var rb in currentVisual.GetComponentsInChildren<Rigidbody>(true))
                    { rb.isKinematic = true; rb.detectCollisions = false; }
                }
                currentAppearance = appearance; currentPrefab = prefab; currentMaterial = null;
            }
            var material = appearance.materialOverride ? appearance.materialOverride : sectionMaterial;
            if (currentVisual && material && currentMaterial != material)
            {
                foreach (var renderer in currentVisual.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
                currentMaterial = material;
            }
        }
    }
}
