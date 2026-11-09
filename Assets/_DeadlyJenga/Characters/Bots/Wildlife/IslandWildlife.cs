using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Wildlife
{
    [DisallowMultipleComponent]
    public sealed class IslandWildlife : MonoBehaviour, IWildlifeControls
    {
        internal const int GroundMask=1|(1<<Layers.Block)|(1<<Layers.Surface);
        static readonly string[] species={"otter","fox","tortoise","jaguar","penguin","ray","crab","frog","boar","monkey","seal","beetle"};
        readonly List<IslandAnimal> animals=new List<IslandAnimal>();
        ISessionInfo session;
        IWorldQuery world;
        int serial;
        public IReadOnlyList<string> Species => species;
        public int Count { get { animals.RemoveAll(a=>!a);return animals.Count; } }
        public bool Available => session!=null&&session.Multiplayer?.CanLeave!=true;
        public string Status { get;private set; }="Local wildlife preview. Wild animals wander; companions follow you.";
        public IWorldQuery World => world;
        public ISessionInfo Session => session;
        internal float BodyScale => session is ILobbyControls?.35f:1f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            GameEvents.SessionStarted+=Attach;GameEvents.LobbyStarted+=Attach;
        }
        static void Attach(GameObject root)
        {
            if(!root||root.GetComponent<IslandWildlife>())return;
            var population=root.AddComponent<IslandWildlife>();population.session=root.GetComponent<ISessionInfo>();population.world=Services.World;
        }
        public GameObject Spawn(string id,bool tame)
        {
            if(!Available){Status="Wildlife preview is local only; leave multiplayer to test animals.";return null;}
            if(System.Array.IndexOf(species,id)<0){Status="Unknown animal species.";return null;}
            if(Count>=24){Status="Wildlife limit reached (24). Clear some animals first.";return null;}
            var player=session.Player;
            if(player==null||!player.gameObject){Status="A player is needed to place wildlife safely.";return null;}
            Vector3 origin = player.transform.position;
            var clearance = SpawnClearance(id, tame);
            Vector3 point = default;
            if (!tame && animals.Count == 0) Physics.SyncTransforms();
            bool found = !tame && TryWildSpawn(id, origin, clearance, out point);
            if (!found) found = TryNearbySpawn(player.transform, clearance, !tame, out point);
            if (!found)
            {
                Status = tame ? "No nearby supported ground. Stand on a wider platform or the island."
                    : "No open ground for this animal on the island.";
                return null;
            }
            var root=new GameObject("Island wildlife - "+id);root.transform.SetParent(transform,true);root.transform.position=point;
            var animal=root.AddComponent<IslandAnimal>();animal.Initialize(this,id,++serial);
            if(tame)animal.SetCompanion(player.transform);
            animals.Add(animal);Status=(tame?"Companion: ":"Wild: ")+id+". "+Count+" / 24 animals.";return root;
        }
        (float radius, float lower, float upper) SpawnClearance(string id, bool tame)
        {
            float size = BodyScale;
            if (size >= 1)
                return tame ? (.28f, .36f, .90f) : (Mathf.Max(.28f, IslandAnimal.BodyRadius(id)), .44f, 1.0f);
            float radius = IslandAnimal.BodyRadius(id) * size;
            float height = Mathf.Max(IslandAnimal.BodyHeight(id) * size, radius * 2, 1.2f * size);
            float lift = Mathf.Max(.005f, .05f * size);
            return (radius, radius + lift, Mathf.Max(radius, height - radius) + lift);
        }
        bool TryWildSpawn(string id, Vector3 player, (float radius, float lower, float upper) clearance, out Vector3 point)
        {
            point = default;
            if (world == null) return false;

            var bounds = world.PlayableBounds;
            float radiusX = Mathf.Min(bounds.extents.x, world.Radius);
            float radiusZ = Mathf.Min(bounds.extents.z, world.Radius);
            if (radiusX < 1 || radiusZ < 1) return false;
            float top = Mathf.Max(bounds.max.y, world.TopHeight) + .5f;
            float depth = top - bounds.min.y + .5f;
            float spread = Mathf.Clamp(Mathf.Min(radiusX, radiusZ) * .32f, 2.5f, 12);
            float coastWeight = id switch
            {
                "crab" or "seal" or "otter" or "penguin" or "ray" => 1.1f,
                "frog" or "tortoise" => .45f,
                _ => -.75f
            };
            float best = float.NegativeInfinity;
            const int samples = 48;
            // The golden angle covers the map without visible rings or shared spawn points.
            for (int i = 0; i < samples; i++)
            {
                float angle = (serial * .63f + i) * 2.399963f;
                float radius = Mathf.Sqrt((i + .5f) / samples) * .97f;
                var sample = bounds.center + new Vector3(Mathf.Cos(angle) * radius * radiusX, 0, Mathf.Sin(angle) * radius * radiusZ);
                if (!TryGroundHit(sample, top, depth, out var hit) || hit.rigidbody ||
                    hit.collider.gameObject.layer == Layers.Block || !IsDry(hit.point)) continue;

                var floor = hit.point;
                float spacing = spread;
                bool occupied = false;
                foreach (var animal in animals)
                {
                    if (!animal) continue;
                    float distance = FlatDistance(floor, animal.transform.position);
                    if (distance < clearance.radius + IslandAnimal.BodyRadius(animal.Species) * BodyScale + .35f)
                    {
                        occupied = true;
                        break;
                    }
                    spacing = Mathf.Min(spacing, distance);
                }
                if (occupied) continue;

                float score = spacing / spread * 1.7f + Mathf.Clamp01(FlatDistance(floor, player) / spread) * .9f;
                score += hit.normal.y * .55f + Mathf.Sin(i * 4.37f + serial * 1.21f) * .13f;
                score += IsNearShore(floor) ? coastWeight : 0;
                score -= Mathf.Clamp((floor.y - world.Center.y) * .13f, 0, 1.5f);
                if (score <= best || !HasClearance(floor, clearance) || !HasRoomToRoam(floor, clearance.radius)) continue;
                best = score;
                point = floor;
            }
            return !float.IsNegativeInfinity(best);
        }
        bool IsNearShore(Vector3 floor)
        {
            float water = world.WaterHeight(floor);
            if (floor.y - water > 6) return false;
            float reach = Mathf.Clamp(world.Radius * .14f, 1.8f, 5);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                var sample = floor + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * reach;
                if (!TryGround(sample, floor.y + 1, floor.y - water + 2, out var neighbor) || !IsDry(neighbor)) return true;
            }
            return false;
        }
        bool HasRoomToRoam(Vector3 floor, float radius)
        {
            int supported = 0;
            float stride = Mathf.Max(.4f, radius * 2);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                var sample = floor + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * stride;
                if (TryGround(sample, floor.y + .35f, .7f, out var neighbor) && IsDry(neighbor) && ++supported >= 2) return true;
            }
            return false;
        }
        bool TryNearbySpawn(Transform player, (float radius, float lower, float upper) clearance, bool wild, out Vector3 point)
        {
            var origin = player.position;
            var forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < 24; i++)
            {
                float angle = (serial * 137.5f + i * 47) * Mathf.Deg2Rad;
                float radius = 2.2f + (i / 8) * 1.4f;
                var sample = origin + (right * Mathf.Sin(angle) + forward * Mathf.Cos(angle)) * radius;
                if (TryGround(sample, origin.y + 1.8f, 8, out point) &&
                    (!wild || IsDry(point)) && HasClearance(point, clearance)) return true;
            }
            point = default;
            return false;
        }
        bool IsDry(Vector3 point) => world == null || point.y >= world.WaterHeight(point) + .02f;
        static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        static bool HasClearance(Vector3 point, (float radius, float lower, float upper) clearance)
            => !Physics.CheckCapsule(point + Vector3.up * clearance.lower, point + Vector3.up * clearance.upper,
                clearance.radius, GroundMask, QueryTriggerInteraction.Ignore);

        static bool TryGroundHit(Vector3 point, float top, float depth, out RaycastHit hit)
            => Physics.Raycast(new Vector3(point.x, top, point.z), Vector3.down, out hit, depth, GroundMask,
                QueryTriggerInteraction.Ignore) && hit.normal.y > .65f;

        public bool TryGround(Vector3 point,float top,float depth,out Vector3 floor)
        {
            bool supported = TryGroundHit(point, top, depth, out var hit);
            floor = supported ? hit.point : point;
            return supported;
        }
        public void Clear()
        {
            foreach(var animal in animals)if(animal)Destroy(animal.gameObject);
            animals.Clear();Status="Wildlife cleared.";
        }
        void Update()
        {
            if(ReferenceEquals(Services.Session,session))world=Services.World;
            // Retained lobby animals sleep while the original lobby player is parked.
            bool present=!(session is ILobbyControls)||(session?.Player!=null&&session.Player.gameObject&&session.Player.gameObject.activeInHierarchy);
            bool active=present&&Available;
            for(int i=animals.Count-1;i>=0;i--)
            {
                var animal=animals[i];if(!animal){animals.RemoveAt(i);continue;}
                animal.gameObject.SetActive(active);
            }
        }
    }
}
