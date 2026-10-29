using Unity.Profiling;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.AI;

namespace DeadlyJenga.CastleBots
{
    // Shared walking corridors. The character motor, not NavMeshAgent, moves the body.
    public sealed class BotWalkingRoutes : System.IDisposable
    {
        static readonly ProfilerMarker DJWalkingSourcesMarker = new ProfilerMarker("DJ.WalkingSources");
        readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        readonly List<NavMeshBuildMarkup> markup = new List<NavMeshBuildMarkup>();
        NavMeshPath path;
        NavMeshData data;
        NavMeshDataInstance instance;
        AsyncOperation building;
        IWorldQuery world;
        NavMeshBuildSettings settings;
        float nextScan;
        int signature;
        public int Revision { get; private set; }
        public string Status { get; private set; } = "Waiting for geometry";

        public void Refresh(CastleBotBrain bot)
        {
            using var measurement = DJWalkingSourcesMarker.Auto();
            if (bot == null || Services.World == null || Time.time < nextScan) return;
            nextScan = Time.time + 1;
            if (!ReferenceEquals(world, Services.World)) { Dispose(); world = Services.World; signature = 0; }
            if (building != null && !building.isDone) return;
            if (building != null) { building = null; Revision++; Status = "Walking corridors ready"; }
            if (NavMesh.GetSettingsCount() == 0) { Status = "No navigation agent settings"; return; }
            settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = bot.Motor.Capsule.radius + .025f;
            settings.agentHeight = bot.Motor.StandingHeight;
            settings.agentClimb = .12f; settings.agentSlope = 45; settings.minRegionArea = .1f;
            settings.overrideVoxelSize = true; settings.voxelSize = Mathf.Max(.06f, settings.agentRadius / 2);
            settings.overrideTileSize = true; settings.tileSize = 128;
            var bounds = world.PlayableBounds; bounds.Expand(8);
            sources.Clear();
            NavMeshBuilder.CollectSources(bounds, CastleBotNavigation.QueryMask, NavMeshCollectGeometry.PhysicsColliders, 0, markup, sources);
            int hash = Mathf.RoundToInt(settings.agentRadius*1000)*31+Mathf.RoundToInt(settings.agentHeight*1000)^BotGeometrySamples.MeshRevision;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                var source = sources[i]; var collider = source.component as Collider;
                if (!collider || collider.isTrigger) { sources.RemoveAt(i); continue; }
                bool surface = Climbables.TryGet(collider, out var climbable);
                // Characters obstruct one another locally; they are not walking geometry to bake.
                if (!surface && (collider is CharacterController || collider.GetComponentInParent<IPlayableCharacter>()!=null || collider.GetComponentInParent<IMovementState>()!=null || collider.GetComponentInParent<IFlyingState>()!=null || collider.GetComponentInParent<IModeActor>()!=null || collider.GetComponentInParent<DeadlyJenga.CandyPhysicsHands.CandyPhysicalHand>())) { sources.RemoveAt(i); continue; }
                var body = surface ? climbable.Body : collider.attachedRigidbody;
                if (body && !body.isKinematic && (body.linearVelocity.sqrMagnitude > .16f || body.angularVelocity.sqrMagnitude > .16f)) { sources.RemoveAt(i); continue; }
                unchecked
                {
                    int item=collider.GetInstanceID();
                    var p = collider.bounds.center;var angles=collider.transform.eulerAngles;
                    item = item * 31 + Mathf.RoundToInt(p.x * 5); item = item * 31 + Mathf.RoundToInt(p.y * 5); item = item * 31 + Mathf.RoundToInt(p.z * 5);
                    item = item * 31 + Mathf.RoundToInt(angles.x);
                    item = item * 31 + Mathf.RoundToInt(angles.y);
                    item = item * 31 + Mathf.RoundToInt(angles.z);
                    item=item*31+(int)source.shape;item=item*31+source.size.GetHashCode();
                    var scale=collider.transform.lossyScale;
                    item=item*31+Mathf.RoundToInt(scale.x*1000);item=item*31+Mathf.RoundToInt(scale.y*1000);item=item*31+Mathf.RoundToInt(scale.z*1000);
                    item=item*31+(source.sourceObject?source.sourceObject.GetInstanceID():0);
                    if(source.sourceObject is Mesh mesh){item=item*31+mesh.bounds.GetHashCode();item=item*31+mesh.vertexCount;}
                    hash ^= item;
                }
            }
            sources.Sort((a,b)=>a.component.GetInstanceID().CompareTo(b.component.GetInstanceID()));
            if (sources.Count == 0) { Status = "No stable walking geometry"; return; }
            if (data && hash == signature) return;
            signature = hash;
            if (!data) { data = new NavMeshData(settings.agentTypeID); instance = NavMesh.AddNavMeshData(data); }
            building = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
            Status = "Updating walking corridors";
        }

        public bool TryWaypoint(CastleBotBrain bot, Vector3 destination, out CastleBotLanding landing)
        {
            landing = default;
            if(path==null)path=new NavMeshPath();
            if (!data || Revision == 0 || !bot.Motor.Grounded) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            var start = bot.transform.position;
            if (!NavMesh.SamplePosition(start, out var from, .5f, filter) || Mathf.Abs(from.position.y - start.y) > .3f) return false;
            if (!NavMesh.SamplePosition(destination, out var to, 2, filter)) return false;
            if (!NavMesh.CalculatePath(from.position, to.position, filter, path) || path.status == NavMeshPathStatus.PathInvalid) return false;
            var corners = path.corners;
            if(path.status==NavMeshPathStatus.PathPartial)
            {
                if(!bot.Motor.HasWalkableIsland||!bot.Motor.StandingSurface||bot.Motor.StandingSurface.gameObject.layer!=0||corners.Length<2)return false;
                float before=Vector3.ProjectOnPlane(destination-start,Vector3.up).magnitude;
                float after=Vector3.ProjectOnPlane(destination-corners[corners.Length-1],Vector3.up).magnitude;
                if(after>=before-.5f)return false;
            }
            for (int i = corners.Length - 1; i >= 1; i--)
            {
                var point = corners[i];
                var delta = point - start;
                if (delta.magnitude > 4) point = start + delta.normalized * 4;
                if (Vector3.Distance(point, start) < .18f) continue;
                if (!CastleBotNavigation.Landing(bot.Motor, point, out var top) || !CastleBotNavigation.Walkable(bot.Motor, start, top.point)) continue;
                landing = new CastleBotLanding { surface = top.transform, body = top.rigidbody, local = top.transform.InverseTransformPoint(top.point), route = CastleBotRoute.Walk };
                return true;
            }
            return false;
        }
        public void Dispose()
        {
            if (data) NavMeshBuilder.Cancel(data);
            if (instance.valid) instance.Remove();
            if (data) Object.Destroy(data);
            data = null; building = null; Revision = 0; world = null;
        }
    }
}
