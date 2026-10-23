using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CastleBlock : MonoBehaviour, IClimbable, IPushable, IBlockOwnership
    {
        public const int SurvivorLayer = Layers.Survivor, BlockLayer = Layers.Block, SurfaceLayer = Layers.Surface;
        public CastleBlockAppearance appearance;
        public Transform visualRoot;
        public Material sectionMaterial;
        GameObject currentVisual, currentPrefab;
        CastleBlockAppearance currentAppearance;
        Material currentMaterial;
        BoxCollider surface;
        Rigidbody surfaceBody;
        BoxCollider sourceBox;
        Vector3 surfacePosition, surfaceScale, surfaceCenter, surfaceSize;
        Quaternion surfaceRotation;
        bool surfaceSynced;
        static bool collisionLayersConfigured;
        Rigidbody body;
        MagneticBlock magnet;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { collisionLayersConfigured = false; }

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
            surface.enabled = traversalEnabled;
            Climbables.Register(surface, this);
            surface.excludeLayers = ~(1 << SurvivorLayer);
            surface.layerOverridePriority = 10;
            SyncSurface();
        }
        void OnEnable() { if (surface) surface.gameObject.SetActive(true); }
        void OnDisable() { if (surface) surface.gameObject.SetActive(false); }
        void OnDestroy() { if (surface) {Climbables.Unregister(surface);Destroy(surface.gameObject);} }
        // IClimbable
        bool traversalEnabled = true;
        public bool CanClimb => isActiveAndEnabled && traversalEnabled;
        public void SetTraversalEnabled(bool enabled)
        { traversalEnabled = enabled; if (surface) surface.enabled = enabled; }
        public float Grip => 1;
        public Transform Anchor => transform;
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        public Transform Visual => visualRoot;

        // IPushable
        public void Push(Vector3 force, GameObject source)
        {
            if (!magnet) magnet = GetComponent<MagneticBlock>();
            if (!magnet || !magnet.TryPush(force)) Body.AddForce(force, ForceMode.VelocityChange);
            GameEvents.RaisePushed(gameObject, force);
        }

        public int OwnerTeam { get; private set; } = -1;
        public void SetOwner(int team, Color color) { OwnerTeam = team; }

        public void SyncSurface()
        {
            if (!surface) return;
            if (!sourceBox) sourceBox = GetComponent<BoxCollider>();
            var center = sourceBox.center; var size = sourceBox.size;
            if (surfaceSynced && !transform.hasChanged && surfaceCenter.Equals(center) && surfaceSize.Equals(size)) return;
            var position = transform.position; var rotation = transform.rotation;
            var scale = transform.lossyScale;
            // Leave resting proxies untouched. Rewriting them dirties the physics broadphase.
            if (!surfaceSynced || !surfacePosition.Equals(position) || !surfaceRotation.Equals(rotation))
            { surfaceBody.position = position; surfaceBody.rotation = rotation; }
            if (!surfaceSynced || !surfaceScale.Equals(scale)) surface.transform.localScale = scale;
            if (!surfaceSynced || !surfaceCenter.Equals(center)) surface.center = center;
            if (!surfaceSynced || !surfaceSize.Equals(size)) surface.size = size;
            surfacePosition = position; surfaceRotation = rotation; surfaceScale = scale;
            surfaceCenter = center; surfaceSize = size; surfaceSynced = true;
            transform.hasChanged = false;
        }
        void FixedUpdate() { if (transform.hasChanged) SyncSurface(); }
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
