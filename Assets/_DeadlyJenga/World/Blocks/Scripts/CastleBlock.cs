using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CastleBlock : MonoBehaviour, IClimbable, IPushable, IBlockOwnership, IHazardMotion
    {
        public const int SurvivorLayer = Layers.Survivor, BlockLayer = Layers.Block, SurfaceLayer = Layers.Surface;
        public CastleBlockAppearance appearance;
        public Transform visualRoot;
        public Material sectionMaterial;
        GameObject currentVisual, currentPrefab;
        CastleBlockAppearance currentAppearance;
        Material currentMaterial;
        BoxCollider surface;
        CastleSurfaceGroup surfaceGroup;
        bool groupedSurface;
        BoxCollider sourceBox;
        Vector3 surfacePosition, surfaceScale, surfaceCenter, surfaceSize;
        Quaternion surfaceRotation;
        bool surfaceSynced;
        static bool collisionLayersConfigured;
        Rigidbody body;
        MagneticBlock magnet;
        CastlePhysicsGroups physicsGroups;
        CastleDebris debris;
        bool visualDebris;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { collisionLayersConfigured = false; }

        void Awake()
        {
            if (!Application.isPlaying) return;
            body = GetComponent<Rigidbody>(); sourceBox = GetComponent<BoxCollider>(); magnet = GetComponent<MagneticBlock>();
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
            // Player collision copies share kinematic bodies and never push real crates.
            var proxy = new GameObject(name + " [survivor surface]");
            SceneManager.MoveGameObjectToScene(proxy, gameObject.scene);
            proxy.layer = SurfaceLayer;
            surfaceGroup = CastleSurfaceGroup.Acquire(gameObject.scene, transform.position);
            proxy.transform.SetParent(surfaceGroup.transform, false);
            surface = proxy.AddComponent<BoxCollider>();
            surface.enabled = traversalEnabled;
            Climbables.Register(surface, this);
            surface.excludeLayers = ~(1 << SurvivorLayer);
            surface.layerOverridePriority = 10;
            SyncSurface();
        }
        void OnEnable() { if (surface) surface.gameObject.SetActive(true); }
        void OnDisable() { physicsGroups?.Disturb(body,true); if (surface) surface.gameObject.SetActive(false); }
        void OnDestroy() { if(sourceBox)Climbables.Unregister(sourceBox); if (surface) {Climbables.Unregister(surface);Destroy(surface.gameObject);} if(surfaceGroup)surfaceGroup.Release(); }
        // IClimbable
        bool traversalEnabled = true;
        public bool CanClimb =>( isActiveAndEnabled && traversalEnabled && !visualDebris)&&!(cityDebris&&cityDebris.IsVisual(Body));
        public void SetTraversalEnabled(bool enabled)
        { traversalEnabled = enabled; if (surface) surface.enabled = enabled && !visualDebris && !groupedSurface; }
        public float Grip => 1;
        public Transform Anchor => transform;
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        public Transform Visual => visualRoot;

        internal void RegisterMagnet(MagneticBlock value) { magnet = value; }
        internal void RegisterPhysicsGroup(CastlePhysicsGroups value) { physicsGroups = value; }
        internal void RegisterDebris(CastleDebris value) { debris = value; }
        internal void SetGroupedSurface(bool grouped) { groupedSurface=grouped; if(surface)surface.enabled=traversalEnabled && !visualDebris && !grouped; }
        internal void RestingImpact(Collision collision) { if(debris)debris.Impact(Body,collision); }
        internal void SetVisualDebris(bool value)
        {visualDebris=value;if(surface){surface.enabled=traversalEnabled && !value && !groupedSurface;}if(!value)SyncSurface();}
        public Vector3 HazardVelocity => cityDebris&&cityDebris.IsVisual(Body)?cityDebris.Velocity(Body):physicsGroups && physicsGroups.IsDormant(Body) ? physicsGroups.Velocity(Body) : debris?debris.Velocity(Body):Body.linearVelocity;

        // IPushable
        public void Push(Vector3 force, GameObject source)
        {
            if(cityDebris&&cityDebris.TryPush(Body,force)){GameEvents.RaisePushed(gameObject,force);return;}
            if (physicsGroups && physicsGroups.TryPush(Body,force,source)) { GameEvents.RaisePushed(gameObject,force); return; }
            physicsGroups?.Disturb(Body);
            if ((!debris || !debris.TryPush(Body,force)) && (!magnet || !magnet.TryPush(force))) Body.AddForce(force, ForceMode.VelocityChange);
            GameEvents.RaisePushed(gameObject, force);
        }

        public int OwnerTeam { get; private set; } = -1;
        public void SetOwner(int team, Color color) { OwnerTeam = team; }

        CityDebrisCleanup cityDebris;
        public void RegisterCityDebris(CityDebrisCleanup value){cityDebris=value;}
        public void UseOriginalTraversal()
        {
            if(!sourceBox)sourceBox=GetComponent<BoxCollider>();
            sourceBox.excludeLayers&=~(1<<Layers.Survivor);
            Climbables.Register(sourceBox,this);
            if(surface){surface.gameObject.SetActive(false);Climbables.Unregister(surface);Destroy(surface.gameObject);surface=null;}
            if(surfaceGroup){surfaceGroup.Release();surfaceGroup=null;}
        }
        public void SyncSurface()
        {
            if(visualDebris)return;
            if (!surface) return;
            if (!sourceBox) sourceBox = GetComponent<BoxCollider>();
            var center = sourceBox.center; var size = sourceBox.size;
            if (surfaceSynced && !transform.hasChanged && surfaceCenter.Equals(center) && surfaceSize.Equals(size)) return;
            var position = transform.position; var rotation = transform.rotation;
            var scale = transform.lossyScale;
            bool changed=!surfaceSynced || !surfacePosition.Equals(position) || !surfaceRotation.Equals(rotation) || !surfaceScale.Equals(scale) || !surfaceCenter.Equals(center) || !surfaceSize.Equals(size);
            // Leave resting proxies untouched. Rewriting them dirties the physics broadphase.
            if (!surfaceSynced || !surfacePosition.Equals(position) || !surfaceRotation.Equals(rotation))
            { surface.transform.SetPositionAndRotation(position, rotation); }
            if (!surfaceSynced || !surfaceScale.Equals(scale)) surface.transform.localScale = scale;
            if (!surfaceSynced || !surfaceCenter.Equals(center)) surface.center = center;
            if (!surfaceSynced || !surfaceSize.Equals(size)) surface.size = size;
            surfacePosition = position; surfaceRotation = rotation; surfaceScale = scale;
            surfaceCenter = center; surfaceSize = size; surfaceSynced = true;
            if(changed)CastleSurfaceGroup.Changed();
            transform.hasChanged = false;
        }
        void Update() { if (!physicsGroups || !physicsGroups.IsDormant(body) || transform.hasChanged) SyncSurface(); RefreshAppearance(); }
        void OnCollisionEnter(Collision collision)
        {
            cityDebris?.Impact(Body,collision);
            if (debris) { debris.Impact(Body,collision); if (!gameObject.activeSelf) return; }
            if (!physicsGroups || !physicsGroups.IsDormant(body) || !collision.rigidbody || collision.rigidbody.isKinematic || collision.relativeVelocity.sqrMagnitude<.01f) return;
            physicsGroups.Disturb(body);
            // The first contact was solved against a resting body. Give it that contact impulse.
            if (collision.contactCount>0) body.AddForceAtPosition(collision.impulse,collision.GetContact(0).point,ForceMode.Impulse);
        }
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
