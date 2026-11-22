using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public class CastleGenerator : MonoBehaviour, IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable
    {
        public CastleSettings settings = new CastleSettings();
        public GameObject blockPrefab;
        public Material cornerMaterial, wallMaterial, keepMaterial;
        [SerializeField] Material goldMaterial, aquaMaterial;
        public Transform foundation;
        public bool generateOnStart = true;
        [Tooltip("Keep this map empty when rebuilding or synchronizing its layout.")]
        public bool environmentOnly;
        [Tooltip("Optional magnetic draft layout. Leave empty for the existing castle.")]
        public MagneticDraftMap magneticMap;
        public float mass = 40;
        [Tooltip("Experimental shared groups for naturally sleeping blocks. Disabled by default: current Editor measurements regress overall frame time.")]
        public bool groupRestingBlocks;
        [Min(1)] public int physicsGroupThreshold = 1500;
        MagneticStructure tycoonSupports;
        public CastlePhysicsGroups PhysicsGroups { get; private set; }
        [Header("Debris cleanup")]
        public bool sinkDisplacedDebris = true, shatterHeavyImpacts = true;
        public bool visualFastDebris = true;
        [Min(.1f)] public float debrisSinkSpeed = 2;
        [Min(8)] public float shatterImpactSpeed = 18;
        [Min(12)] public float visualDebrisSpeed = 20;
        [Min(20)] public float visualDebrisAcceleration = 180;
        public CastleDebris Debris { get; private set; }
        public readonly List<Rigidbody> Bodies = new List<Rigidbody>();
        [SerializeField, HideInInspector] Transform generatedRoot;
        public float LastBuildMilliseconds { get; private set; }
        public int Revision { get; private set; }

        [Header("World")]
        public float waterHeight = .08f;
        [Tooltip("Anything that can be eliminated is eliminated below this height.")]
        public float oceanKillHeight = -6;
        [Tooltip("Optional playable area. Use an enabled trigger BoxCollider. Otherwise the castle layout supplies the area.")]
        public BoxCollider playableArea;
        readonly Collider[] fallen = new Collider[256];
        readonly Collider[] curatedSpawnClearance = new Collider[24];
        static readonly Vector3[] CuratedSpawns = {
            new(-36,.48f,-12),new(36,.48f,12),new(-36,.48f,12),new(36,.48f,-12),
            new(-36,.48f,-26),new(36,.48f,26),new(0,.48f,-28),new(0,.48f,28)
        };
        int curatedSpawnCursor;
        bool CuratedCtf => !magneticMap && !environmentOnly && settings.layoutPreset == "ctf";
        float nextKillCheck;
        int debrisCursor;
        readonly Dictionary<Rigidbody, (float linear, float angular)> dryDamping = new();
        const float WaterLinearDrag = 3.2f, WaterAngularDrag = 2.2f;
        const float EntrySpeed = .85f, EntryDepth = .025f, RetireDepth = 3f;
        const int WaterGroundMask = ~((1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface));

        void UpdateBlockWaterContact()
        {
            if (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority) return;

            foreach (var body in Bodies)
            {
                if (!body) continue;
                bool wet = dryDamping.ContainsKey(body);
                if (!body.gameObject.activeInHierarchy || body.isKinematic)
                {
                    if (wet) RestoreDryDamping(body);
                    continue;
                }
                if (!wet && body.IsSleeping()) continue;

                var shape = body.GetComponent<Collider>();
                if (!shape || !shape.enabled || shape.isTrigger)
                {
                    if (wet) RestoreDryDamping(body);
                    continue;
                }
                var bounds = shape.bounds;
                float water = WaterHeight(bounds.center);
                float gap = bounds.min.y - water;
                var velocity = body.linearVelocity;
                float step = Time.fixedDeltaTime;
                float gravity = body.useGravity ? Physics.gravity.y : 0;
                if (gap > Mathf.Max(0, -(velocity.y + gravity * step) * step))
                {
                    if (wet) RestoreDryDamping(body);
                    continue;
                }

                // Solid ground at the water line takes the impact instead of water.
                if (gap >= -.35f || wet)
                {
                    float rayStart = Mathf.Max(water + .05f, bounds.min.y + .05f);
                    var aboveSurface = new Vector3(bounds.center.x, rayStart, bounds.center.z);
                    if (Physics.Raycast(aboveSurface, Vector3.down, out var hit, rayStart - water + .3f,
                            WaterGroundMask, QueryTriggerInteraction.Ignore) && !hit.rigidbody)
                    {
                        if (wet) RestoreDryDamping(body);
                        continue;
                    }
                }

                if (!wet)
                {
                    if (gap > 0)
                    {
                        // Resolve the surface crossing next step, even for a thin, fast block.
                        float crossingSpeed = -(gap + EntryDepth) / step;
                        if (velocity.y < crossingSpeed)
                        {
                            velocity.y = crossingSpeed;
                            body.linearVelocity = velocity;
                        }
                        continue;
                    }

                    dryDamping.Add(body, (body.linearDamping, body.angularDamping));
                    body.linearDamping = WaterLinearDrag;
                    body.angularDamping = WaterAngularDrag;
                    if (velocity.y < -EntrySpeed)
                    {
                        velocity.y = -EntrySpeed;
                        body.linearVelocity = velocity;
                    }
                }

                if (bounds.max.y <= water - RetireDepth)
                {
                    RestoreDryDamping(body);
                    body.gameObject.SetActive(false);
                }
            }
        }

        void RestoreDryDamping(Rigidbody body)
        {
            if (!dryDamping.Remove(body, out var dry)) return;
            body.linearDamping = dry.linear;
            body.angularDamping = dry.angular;
        }

        void ResetBlockWaterContact()
        {
            foreach (var entry in dryDamping)
            {
                if (!entry.Key) continue;
                entry.Key.linearDamping = entry.Value.linear;
                entry.Key.angularDamping = entry.Value.angular;
            }
            dryDamping.Clear();
        }

        void Start() { if (generateOnStart) Rebuild(); }
        void OnEnable() { if(foundation&&foundation.gameObject!=gameObject)foundation.gameObject.SetActive(true);Services.World = this; }
        void OnDisable()
        {
            ResetBlockWaterContact();
            if (foundation && foundation.gameObject != gameObject) foundation.gameObject.SetActive(false);
            if (ReferenceEquals(Services.World, this)) Services.World = null;
        }

        // IWorldQuery
        public float WaterHeight(Vector3 position) => waterHeight;
        public bool IsSheltered(Vector3 position) =>
            Physics.Raycast(position + Vector3.up * .1f, Vector3.up, 500, 1 << Layers.Block, QueryTriggerInteraction.Ignore);

        public IReadOnlyList<Rigidbody> Blocks => Bodies;
        public Vector3 Center => transform.position;
        public float TopHeight => magneticMap ? transform.TransformPoint(new Vector3(0, 28, 0)).y : environmentOnly ? SafeSpawnPoint().y : CuratedCtf ? transform.TransformPoint(Vector3.up * ModeMapLayout.CuratedCtfTop).y : transform.position.y + CastleLayout.HeightCourses(settings) * settings.blockSize.y;
        public float Radius => magneticMap ? 18 * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) : CuratedCtf ? ModeMapLayout.Size(settings).magnitude * .5f * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) : CastleLayout.Footprint(settings) * .5f;
        public float StepHeight => CuratedCtf ? ModeMapLayout.CuratedCtfGroundY : settings.blockSize.y;
        public Bounds PlayableBounds
        {
            get
            {
                if (playableArea)
                    return playableArea.isTrigger ? playableArea.bounds : new Bounds();
                var footprint = magneticMap ? Vector2.one*40 : CastleLayout.FootprintSize(settings)+Vector2.one*4;
                float height = magneticMap ? 30 : CuratedCtf ? ModeMapLayout.CuratedCtfTop : CastleLayout.HeightCourses(settings) * settings.blockSize.y;
                var bounds = new Bounds(transform.TransformPoint(Vector3.zero), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                for (int y = 0; y <= 1; y++)
                for (int z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(transform.TransformPoint(new Vector3(x * footprint.x * .5f, y * height, z * footprint.y * .5f)));
                return bounds;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var area = PlayableBounds;
            Gizmos.DrawWireCube(area.center, area.size);
        }

        // ISyncedWorld (Revision is declared above)
        public Transform Root => transform;
        const string CuratedCtfPrefix = "castle-ctf-v2:";
        public string SaveLayout()
        {
            if (magneticMap) return magneticMap.Save();
            string json = JsonUtility.ToJson(settings);
            return settings.layoutPreset == "ctf" ? CuratedCtfPrefix + json : json;
        }
        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            if (magneticMap)
            {
                if (!MagneticDraftMap.TryRead(layout, expectedBlocks, out var state)) return false;
                transform.SetPositionAndRotation(origin.position, origin.rotation);
                transform.localScale = scale;
                Rebuild(); magneticMap.Structure.Restore(state.broken);
                return true;
            }
            bool curated = layout != null && layout.StartsWith(CuratedCtfPrefix, System.StringComparison.Ordinal);
            CastleSettings incoming;
            try
            {
                incoming = JsonUtility.FromJson<CastleSettings>(curated ? layout.Substring(CuratedCtfPrefix.Length) : layout);
                if (incoming != null) CastleLayout.Validate(incoming);
            }
            catch (System.ArgumentException) { return false; }
            if (incoming == null || (incoming.layoutPreset == "ctf") != curated ||
                (environmentOnly ? 0 : CastleLayout.Build(incoming).Count) != expectedBlocks) return false;
            settings = incoming;
            transform.SetPositionAndRotation(origin.position, origin.rotation);
            transform.localScale = scale;
            Rebuild();
            return true;
        }
        public void SetReplica(bool replica)
        {
            if (replica) ResetBlockWaterContact();
            if (magneticMap && magneticMap.Structure) { magneticMap.Structure.SetReplica(replica); return; }
            if (PhysicsGroups) PhysicsGroups.SetReplica(replica);
            foreach (var body in Bodies)
            {
                if (!body) continue;
                if (replica) body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = replica;
                if (!replica && blockPrefab) body.collisionDetectionMode = blockPrefab.GetComponent<Rigidbody>().collisionDetectionMode;
                body.interpolation = replica ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            }
            if (Debris) Debris.SetReplica(replica);
            if (tycoonSupports) tycoonSupports.SetReplica(replica);
        }

        // IMapFootprint: the bottom course of the castle, for the sand around it
        public int FootprintVersion
        {
            get
            {
                if (settings == null) return 0;
                int hash = 17;
                void Hash(float value) { hash = hash * 31 + value.GetHashCode(); }
                Hash(settings.blockSize.x); Hash(settings.blockSize.z); Hash(settings.modulesPerSide);
                Hash((int)settings.structureVariant); Hash(settings.moduleGap); Hash(settings.singleTower ? 1 : 0); Hash(CastleLayout.Footprint(settings));
                hash=hash*31+(settings.layoutPreset??"").GetHashCode();
                return hash;
            }
        }
        public float FootprintHalfSize => settings != null ? CastleLayout.Footprint(settings) * .5f : 0;
        public void GetGroundFootprints(List<Footprint> into)
        {
            if (settings == null) return;
            foreach (var placement in CastleLayout.Build(settings))
                if (Mathf.Abs(placement.Position.y-placement.BlockSize(settings).y*.5f)<.01f)
                    into.Add(new Footprint
                    {
                        center = new Vector2(placement.Position.x, placement.Position.z),
                        halfSize = new Vector2(placement.BlockSize(settings).x, placement.BlockSize(settings).z) * .5f,
                        rotation = placement.Rotation
                    });
        }

        // Top of the highest upright block, or the foundation if none are left.
        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();
            if (CuratedCtf)
            {
                var physics = gameObject.scene.GetPhysicsScene();
                for (int n = 0; n < CuratedSpawns.Length; n++)
                {
                    var start = transform.TransformPoint(CuratedSpawns[curatedSpawnCursor++ % CuratedSpawns.Length]);
                    if (!physics.Raycast(start + Vector3.up, Vector3.down, out var hit, 3,
                        (1 << Layers.Block) | (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore) || hit.normal.y < .8f) continue;
                    var feet = hit.point + Vector3.up * .08f;
                    if (physics.OverlapCapsule(feet + Vector3.up * .2f, feet + Vector3.up * .95f, .18f,
                        curatedSpawnClearance, (1 << Layers.Block) | (1 << Layers.Surface) | (1 << Layers.Survivor) | 1,
                        QueryTriggerInteraction.Ignore) == 0) return feet;
                }
                throw new System.InvalidOperationException("No supported clear courtyard spawn remains.");
            }
            Collider best = null; float highest = float.NegativeInfinity;
            foreach (var body in Bodies)
            {
                if (!body || !body.gameObject.activeInHierarchy || Vector3.Dot(body.transform.up, Vector3.up) < .95f) continue;
                var col = body.GetComponent<Collider>();
                if (!col) col = body.GetComponentInChildren<Collider>();
                if (!col || !col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy) continue;
                if (col.bounds.max.y > highest) { highest = col.bounds.max.y; best = col; }
            }
            if (best && settings.layoutPreset == ShrineTownLayout.Preset)
            {
                // The block box ends at the deck. Find the exposed roof under the spawn,
                // including its pitch, without placing the survivor on the narrow spire.
                var bounds = best.bounds;
                float ceiling = bounds.max.y;
                foreach (var part in best.GetComponentsInChildren<Collider>())
                    if (part.enabled && !part.isTrigger) ceiling = Mathf.Max(ceiling, part.bounds.max.y);
                var start = new Vector3(bounds.center.x + bounds.extents.x*.25f, ceiling+1,
                    bounds.center.z + bounds.extents.z*.25f);
                if (gameObject.scene.GetPhysicsScene().Raycast(start,Vector3.down,out var roofHit,
                    ceiling-bounds.min.y+2,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore))
                    return roofHit.point + Vector3.up*.12f;
            }
            return best ? new Vector3(best.bounds.center.x, best.bounds.max.y + .08f, best.bounds.center.z)
                : (foundation ? foundation.position : transform.position) + Vector3.up * .6f;
        }

        // The ocean: eliminate anything that falls below the kill height.
        void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            UpdateBlockWaterContact();
            if (Time.time < nextKillCheck) return;
            nextKillCheck = Time.time + .1f;
            var center = new Vector3(transform.position.x, oceanKillHeight - 500, transform.position.z);
            int mask = ~((1 << Layers.Block) | (1 << Layers.Surface));
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(2000, 500, 2000), fallen, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = fallen[i].GetComponentInParent<IDamageable>();
                if (target != null && target.Health > 0) target.Eliminate(gameObject);
            }
        }

        public void Rebuild()
        {
            Revision++;
            curatedSpawnCursor = 0;
            CastleLayout.Validate(settings);
            var layout = environmentOnly || magneticMap ? new List<CastlePlacement>() : CastleLayout.Build(settings);
            if (!environmentOnly && !magneticMap && (!blockPrefab || !blockPrefab.GetComponent<Rigidbody>() || !blockPrefab.GetComponent<BoxCollider>()))
                throw new System.InvalidOperationException("Assign a unit cube prefab with Rigidbody and BoxCollider.");
            float started = Time.realtimeSinceStartup;
            ResetBlockWaterContact();
            if (generatedRoot)
            {
                generatedRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generatedRoot.gameObject); else DestroyImmediate(generatedRoot.gameObject);
            }
            PhysicsGroups=null;Debris=null;tycoonSupports=null;
            Bodies.Clear();debrisCursor=0;
            generatedRoot = new GameObject("Generated Castle Blocks").transform;
            generatedRoot.SetParent(transform, false);
            if (magneticMap)
            {
                magneticMap.Build(generatedRoot, Bodies, () => Revision++);
                Physics.SyncTransforms();
                LastBuildMilliseconds = (Time.realtimeSinceStartup - started) * 1000;
                return;
            }
            if (foundation)
            {
                var size = CastleLayout.FootprintSize(settings)+Vector2.one*4;
                foundation.localScale = new Vector3(size.x, 1, size.y);
                if (settings.layoutPreset == ShrineTownLayout.Preset)
                {
                    ShrineTownLayout.ShapeFoundation(foundation);
                    ShrineTownLayout.BuildStreetColliders(generatedRoot,transform.InverseTransformPoint(Vector3.up*waterHeight).y);
                }
            }
            foreach (var p in layout)
            {
                var go = Instantiate(blockPrefab, generatedRoot);
                go.name = $"{p.Section}_Course{p.Course:00}_Block{Bodies.Count:000}";
                go.transform.localPosition = p.Position;
                go.transform.localRotation = p.Rotation;
                go.transform.localScale = p.BlockSize(settings);
                var body = go.GetComponent<Rigidbody>();
                // Teleport the physics pose too: interpolated bodies otherwise retain the
                // prefab's origin until the first physics step, including during top spawn.
                body.position = go.transform.position; body.rotation = go.transform.rotation;
                var block = go.GetComponent<CastleBlock>();
                if (block)
                {
                    block.sectionMaterial = p.Section switch
                    {
                        CastleSection.Corner => cornerMaterial,
                        CastleSection.Keep => keepMaterial,
                        CastleSection.Gold => goldMaterial ? goldMaterial : wallMaterial,
                        CastleSection.Aqua => aquaMaterial ? aquaMaterial : cornerMaterial,
                        _ => wallMaterial
                    };
                    if(p.DeckProfile.Finish == TycoonDeckFinish.Plain || !block.visualRoot)
                    {
                        block.RefreshAppearance();
                    }
                    else
                    {
                        foreach(Transform child in block.visualRoot) child.gameObject.SetActive(false);
                        block.appearance=null;
                        var mesh=new GameObject("Molded deck");
                        mesh.transform.SetParent(block.visualRoot,false);
                        mesh.AddComponent<TycoonDeckVisual>().Build(p.DeckProfile,block.sectionMaterial,aquaMaterial,p.BlockSize(settings));
                    }
                    block.SyncSurface();
                }
                var size=p.BlockSize(settings);
                if (settings.layoutPreset == ShrineTownLayout.Preset)
                {
                    ShrineTownLayout.AddRoofColliders(body,size);
                    if (block) ShrineTownLayout.AddRoofTraversal(block,size);
                }
                float volumeRatio=size.x*size.y*size.z/(settings.blockSize.x*settings.blockSize.y*settings.blockSize.z);
                // Temple decks represent timber framing with a hollow tiled roof.
                if (settings.layoutPreset == ShrineTownLayout.Preset && p.Section == CastleSection.Corner) volumeRatio *= .24f;
                body.mass = Mathf.Max(1, mass*volumeRatio);
                // Ninety loose courses need more contact iterations than the standard castle.
                bool tallKeep=settings.structureVariant==CastleStructure.FourKeeps&&p.Section==CastleSection.Keep;
                body.solverIterations = settings.cathedral ? 80 : tallKeep ? 60 : 20;
                body.solverVelocityIterations = settings.cathedral ? 20 : tallKeep ? 16 : 8;
                body.sleepThreshold = .005f;
                Bodies.Add(body);
            }
            if (Application.isPlaying && settings.layoutPreset == ShrineTownLayout.Preset)
                ShrineTownLayout.JoinTimberFrames(layout, Bodies);
            Physics.SyncTransforms();
            if (Application.isPlaying && settings.layoutPreset == TycoonLayout.Preset)
                tycoonSupports = TycoonSupports.Build(generatedRoot, Bodies);
            if (Application.isPlaying && !tycoonSupports && groupRestingBlocks && Bodies.Count>=physicsGroupThreshold)
            {
                PhysicsGroups=generatedRoot.gameObject.AddComponent<CastlePhysicsGroups>();
                PhysicsGroups.Initialize(Bodies);
            }
            if(Application.isPlaying && Bodies.Count>0 && sinkDisplacedDebris)
            {
                Debris=generatedRoot.gameObject.AddComponent<CastleDebris>();
                Debris.SinkDebris=sinkDisplacedDebris;Debris.ShatterOnImpact=false;
                Debris.SinkSpeed=debrisSinkSpeed;Debris.ImpactSpeed=shatterImpactSpeed;
                Debris.VisualFastDebris=visualFastDebris;Debris.VisualSpeed=visualDebrisSpeed;Debris.AccelerationThreshold=visualDebrisAcceleration;
                Debris.Initialize(Bodies,PhysicsGroups);
            }
            // Let gravity settle the tower naturally: do not hide instability by sleeping it at spawn.
            LastBuildMilliseconds = (Time.realtimeSinceStartup - started) * 1000;
        }
    }
}
