using System.Collections.Generic;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(2100)]
    internal sealed class LobbyLiveMap : MonoBehaviour
    {
        const float BaseWidth = 1.9f;
        const float MapWidth = 1.72f;
        const float SampleInterval = 1f / 20f;
        const float WatchRange = 9;
        const float MarkerSize = .044f;
        // The plinth: a square frustum from the floor up to the map, with rounded corners
        // and a slope that curves over into the flat top.
        internal const float PlinthHeight = .91f;
        internal const float PlinthBase = 2.56f;
        internal const float PlinthTop = 2.24f;
        const float PlinthRoundOver = .16f;
        const float PlinthCorner = .2f;

        sealed class ReplicaMesh
        {
            public Renderer source, copy;
            public bool moves;
        }

        sealed class Participant
        {
            public Transform group, visual, marker;
            public MeshRenderer markerRenderer;
            public readonly List<ReplicaMesh> meshes = new();
            public readonly Dictionary<Transform, Transform> bones = new();
        }

        readonly List<ReplicaMesh> terrain = new();
        readonly Dictionary<string, Participant> participants = new();
        readonly HashSet<string> seen = new();
        readonly List<string> departed = new();
        readonly Plane[] planes = new Plane[6];
        MaterialPropertyBlock color;
        MaterialPropertyBlock appearance;
        ISessionInfo viewer;
        ILobbyMatchVisitControls visit;
        Transform miniature;
        TextMesh status;
        Mesh cube, sphere, plinthMesh;
        Material surface, border, markers, lettering;
        IWorldQuery map;
        IMatchRuntime runtime;
        int revision;
        double nextSample, nextScenerySample;
        float scale;
        Vector3 origin;
        Bounds footprint;

        internal static void Attach(Transform lobby, ISessionInfo viewer)
        {
            var floor = new GameObject("Showtime Plaza live match miniature");
            floor.transform.SetParent(lobby, false);
            floor.transform.localPosition = MayhemMarina.CinemaTable;
            var display = floor.AddComponent<LobbyLiveMap>();
            display.Build(viewer);
        }

        void Build(ISessionInfo session)
        {
            color = new MaterialPropertyBlock();
            appearance = new MaterialPropertyBlock();
            viewer = session;
            visit = session as ILobbyMatchVisitControls;
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            border = MarinaMesh.MaterialFor(MarinaFinish.Wood);
            surface = new Material(unlit) { color = new Color(.035f, .32f, .42f) };
            markers = new Material(unlit);

            plinthMesh = PlinthMesh();
            MakeMesh("Live map plinth", plinthMesh, border);

            var rim = MakeMesh("Table map base", cube, border);
            rim.transform.localPosition = Vector3.up * .021f;
            rim.transform.localScale = new Vector3(BaseWidth, .042f, BaseWidth);
            var inset = MakeMesh("Miniature water", cube, surface);
            inset.transform.localPosition = Vector3.up * .038f;
            inset.transform.localScale = new Vector3(1.79f, .021f, 1.79f);

            var sign = new GameObject("Match map status");
            sign.transform.SetParent(transform, false);
            sign.transform.SetLocalPositionAndRotation(new Vector3(0, .050f, 0), Quaternion.Euler(90, 0, 0));
            status = sign.AddComponent<TextMesh>();
            status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.fontSize = 58;
            status.fontStyle = FontStyle.Bold;
            status.anchor = TextAnchor.MiddleCenter;
            status.alignment = TextAlignment.Center;
            status.characterSize = .032f;
            status.color = new Color(.78f, 1, .97f);
            status.text = "NO LIVE MATCH";
            var shader = Resources.Load<Shader>("VillageLettering");
            if(shader)
            {
                lettering = new Material(shader) { name = "Marina tabletop lettering" };
                status.GetComponent<MeshRenderer>().sharedMaterial = lettering;
                sign.AddComponent<ChristmasTownText>();
            }
            else status.GetComponent<MeshRenderer>().sharedMaterial = status.font.material;

            miniature = new GameObject("Visual-only miniature").transform;
            miniature.SetParent(transform, false);
            miniature.localPosition = Vector3.up * .051f;
            miniature.gameObject.SetActive(false);
        }

        // Builds the plinth from stacked rings. Each ring is a rounded square; the side profile
        // runs straight up the slope, then bends over (a curve) into the flat top at y = 0.
        static Mesh PlinthMesh()
        {
            const int cornerSteps = 6, bendSteps = 6;
            float bottom = PlinthBase * .5f, edge = PlinthTop * .5f, y0 = -PlinthHeight;
            // The bend starts on the slope below the top edge and ends on the top face, inside the edge.
            var slope = new Vector2(edge - bottom, PlinthHeight).normalized;
            var bendStart = new Vector2(edge, 0) - slope * PlinthRoundOver;
            var bendEnd = new Vector2(edge - PlinthRoundOver, 0);
            var profile = new List<Vector2> { new(bottom, y0) };
            for (int i = 0; i <= bendSteps; i++)
            {
                float t = i / (float)bendSteps;
                profile.Add((1 - t) * (1 - t) * bendStart + 2 * (1 - t) * t * new Vector2(edge, 0) + t * t * bendEnd);
            }

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            int ringSize = 4 * (cornerSteps + 1);
            foreach (var point in profile)
            {
                float half = point.x, corner = Mathf.Min(PlinthCorner, half * .9f), run = 0;
                for (int c = 0; c < 4; c++)
                {
                    var center = new Vector2(c == 0 || c == 3 ? half - corner : corner - half,
                        c < 2 ? half - corner : corner - half);
                    for (int s = 0; s <= cornerSteps; s++)
                    {
                        float angle = (c + s / (float)cornerSteps) * Mathf.PI * .5f;
                        var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * corner;
                        if (vertices.Count % ringSize > 0)
                        {
                            var last = vertices[vertices.Count - 1];
                            run += Vector2.Distance(p, new Vector2(last.x, last.z));
                        }
                        vertices.Add(new Vector3(p.x, point.y, p.y));
                        uvs.Add(new Vector2(run, point.y));
                    }
                }
            }
            for (int r = 0; r + 1 < profile.Count; r++)
                for (int i = 0; i < ringSize; i++)
                {
                    int a = r * ringSize + i, b = r * ringSize + (i + 1) % ringSize;
                    triangles.AddRange(new[] { a, a + ringSize, b, b, a + ringSize, b + ringSize });
                }
            int top = (profile.Count - 1) * ringSize, middle = vertices.Count;
            vertices.Add(Vector3.zero);
            uvs.Add(Vector2.zero);
            for (int i = 0; i < ringSize; i++)
                triangles.AddRange(new[] { middle, top + (i + 1) % ringSize, top + i });

            var mesh = new Mesh { name = "Live map plinth" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        MeshRenderer MakeMesh(string name, Mesh mesh, Material material)
        {
            var node = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            node.transform.SetParent(transform, false);
            node.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Configure(renderer);
            return renderer;
        }

        void LateUpdate()
        {
            var current = MatchRuntime.Current;
            var currentMap = Services.World;
            var eye = viewer?.ActiveCamera;
            bool live = visit?.VisitingLobby == true && current?.Frame?.phase == RoundPhase.Playing && currentMap != null;
            if (!live || !eye || !eye.isActiveAndEnabled)
            {
                if (map != null) ResetReplica();
                miniature.gameObject.SetActive(false);
                status.gameObject.SetActive(true);
                status.text = "NO LIVE MATCH";
                return;
            }

            bool visible = (eye.transform.position - transform.position).sqrMagnitude <= WatchRange * WatchRange;
            if (visible)
            {
                GeometryUtility.CalculateFrustumPlanes(eye, planes);
                visible = GeometryUtility.TestPlanesAABB(planes,
                    new Bounds(transform.position + Vector3.up * .65f, new Vector3(2, 1.5f, 2)));
            }
            if (!visible)
            {
                miniature.gameObject.SetActive(false);
                status.gameObject.SetActive(false);
                return;
            }

            var synced = currentMap as ISyncedWorld;
            if (map != currentMap || runtime != current || revision != (synced?.Revision ?? 0))
                MakeReplica(currentMap, current, synced);
            miniature.gameObject.SetActive(map != null);
            status.gameObject.SetActive(map == null);
            if (map == null)
            {
                status.text = "MAP UNAVAILABLE";
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            if (now < nextSample) return;
            nextSample = now + SampleInterval;
            bool sampleScenery = now >= nextScenerySample;
            if (sampleScenery) nextScenerySample = now + .5;
            foreach (var mesh in terrain)
                if (mesh.moves || sampleScenery) UpdateMesh(mesh, true);
            UpdatePlayers(current);
        }

        void MakeReplica(IWorldQuery world, IMatchRuntime match, ISyncedWorld synced)
        {
            ResetReplica();
            var root = synced?.Root ?? (world as Component)?.transform;
            footprint = world.PlayableBounds;
            float size = Mathf.Max(footprint.size.x, footprint.size.z);
            if (!root || size <= .01f || float.IsNaN(size) || float.IsInfinity(size)) return;
            map = world;
            runtime = match;
            revision = synced?.Revision ?? 0;
            var water = world.WaterHeight(footprint.center);
            if (float.IsNaN(water) || float.IsInfinity(water)) water = footprint.min.y;
            var sand = DeadlyJenga.CandyArt.CandyEnvironment.ForMap(world as Component)?.sandRenderer as MeshRenderer;
            if (sand && sand.enabled && sand.bounds.Intersects(footprint) &&
                Mathf.Max(sand.bounds.size.x, sand.bounds.size.z) <= size * 2.25f)
                footprint.Encapsulate(sand.bounds);
            origin = new Vector3(footprint.center.x, water, footprint.center.z);
            footprint.Expand(new Vector3(size * .2f, 0, size * .2f));
            float fittedWidth = Mathf.Max(footprint.size.x, footprint.size.z);
            scale = Mathf.Min(MapWidth / fittedWidth, 1.3f / Mathf.Max(1, footprint.max.y - water));
            miniature.localScale = Vector3.one * scale;

            var collected = new HashSet<MeshRenderer>();
            void Add(MeshRenderer renderer)
            {
                if (!renderer || !renderer.enabled || !collected.Add(renderer)) return;
                var bounds = renderer.bounds;
                if (bounds.size.x > footprint.size.x || bounds.size.z > footprint.size.z ||
                    !WithinFootprint(bounds.center) || bounds.max.y < origin.y - .2f) return;
                if (CopyMesh(renderer, miniature, null) is not { } mesh) return;
                mesh.moves = renderer.GetComponentInParent<Rigidbody>();
                terrain.Add(mesh);
            }

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) Add(renderer);
            foreach (var body in world.Blocks)
                if (body) foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>()) Add(renderer);
            Add(sand);
        }

        bool WithinFootprint(Vector3 point) => Mathf.Abs(point.x - origin.x) <= footprint.extents.x &&
            Mathf.Abs(point.z - origin.z) <= footprint.extents.z;

        void UpdatePlayers(IMatchRuntime match)
        {
            seen.Clear();
            foreach (var player in match.Frame.players)
            {
                if (player == null || string.IsNullOrEmpty(player.id) || !player.alive ||
                    player.dying || player.role == "spectator") continue;
                var actor = match.FindActor(player.id);
                var visual = actor?.Visual;
                var position = actor == null ? player.position : actor.transform.position;
                if (!WithinFootprint(position) || position.y < origin.y - .8f || !seen.Add(player.id)) continue;

                if (!participants.TryGetValue(player.id, out var entry) || entry.visual != visual)
                {
                    if (entry != null) Remove(entry.group.gameObject);
                    participants[player.id] = entry = MakeParticipant(player.id, visual);
                }

                foreach (var bone in entry.bones)
                    if (bone.Key) Place(bone.Value, bone.Key);
                foreach (var mesh in entry.meshes) UpdateMesh(mesh, false);
                float markerHeight = actor == null ? 1.7f : Mathf.Max(.6f, actor.ViewPose.position.y - position.y);
                var marker = position + Vector3.up * markerHeight;
                entry.marker.localPosition = marker - origin + Vector3.up * (MarkerSize * .65f / scale);
                color.SetColor("_BaseColor", Color.Lerp(player.color, Color.white, .18f));
                entry.markerRenderer.SetPropertyBlock(color);
            }

            departed.Clear();
            foreach (var entry in participants)
                if (!seen.Contains(entry.Key)) departed.Add(entry.Key);
            foreach (var id in departed)
            {
                Remove(participants[id].group.gameObject);
                participants.Remove(id);
            }
        }

        Participant MakeParticipant(string id, Transform visual)
        {
            var entry = new Participant { visual = visual };
            entry.group = new GameObject("Miniature player " + id).transform;
            entry.group.SetParent(miniature, false);
            if (visual)
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    if (CandyScorePortraits.VisibleInAvatar(renderer.transform, visual) &&
                        CopyMesh(renderer, entry.group, entry.bones) is { } mesh) entry.meshes.Add(mesh);

            entry.markerRenderer = MakeMesh("Player color marker", sphere, markers);
            entry.marker = entry.markerRenderer.transform;
            entry.marker.SetParent(entry.group, false);
            entry.marker.localScale = Vector3.one * (MarkerSize / scale);
            return entry;
        }

        ReplicaMesh CopyMesh(Renderer source, Transform parent, Dictionary<Transform, Transform> bones)
        {
            if (source.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return null;
            var skin = source as SkinnedMeshRenderer;
            bool skinned = skin && skin.sharedMesh && bones != null;
            Mesh mesh = skinned ? skin.sharedMesh : source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter)
                ? filter.sharedMesh : null;
            if (!mesh) return null;

            var replica = new GameObject(source.name).transform;
            replica.SetParent(parent, false);
            Renderer copy;
            if (skinned)
            {
                Transform Bone(Transform original)
                {
                    if (!original) return null;
                    if (bones.TryGetValue(original, out var existing)) return existing;
                    var node = new GameObject(original.name).transform;
                    node.SetParent(parent, false);
                    bones.Add(original, node);
                    Place(node, original);
                    return node;
                }

                var target = replica.gameObject.AddComponent<SkinnedMeshRenderer>();
                target.sharedMesh = mesh;
                target.rootBone = Bone(skin.rootBone);
                target.bones = System.Array.ConvertAll(skin.bones, Bone);
                target.localBounds = skin.localBounds;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    target.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                copy = target;
            }
            else
            {
                replica.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                copy = replica.gameObject.AddComponent<MeshRenderer>();
            }

            copy.sharedMaterials = source.sharedMaterials;
            Configure(copy);
            var result = new ReplicaMesh { source = source, copy = copy };
            source.GetPropertyBlock(appearance);
            copy.SetPropertyBlock(appearance);
            UpdateMesh(result, bones == null);
            return result;
        }

        void UpdateMesh(ReplicaMesh mesh, bool mapMesh)
        {
            bool show = mesh.source && mesh.source.enabled && mesh.source.gameObject.activeInHierarchy;
            if (show && mapMesh)
            {
                var bounds = mesh.source.bounds;
                // The source can hide its individual renderer when it draws the same mesh in a batch.
                show = WithinFootprint(bounds.center) && bounds.max.y >= origin.y - .2f;
            }
            if (mesh.copy.enabled != show) mesh.copy.enabled = show;
            if (show) Place(mesh.copy.transform, mesh.source.transform);
        }

        void Place(Transform target, Transform source)
        {
            var position = source.position - origin;
            var rotation = source.rotation;
            var size = source.lossyScale;
            if (target.localPosition != position || target.localRotation != rotation)
                target.SetLocalPositionAndRotation(position, rotation);
            if (target.localScale != size) target.localScale = size;
        }

        static void Configure(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void ResetReplica()
        {
            for (int i = 0; i < miniature.childCount; i++) Remove(miniature.GetChild(i).gameObject);
            terrain.Clear();
            participants.Clear();
            map = null;
            runtime = null;
            nextSample = nextScenerySample = 0;
        }

        static void Remove(GameObject node)
        {
            node.SetActive(false);
            Destroy(node);
        }

        void OnDestroy()
        {
            Destroy(border);
            Destroy(plinthMesh);
            Destroy(surface);
            Destroy(markers);
            if(lettering)Destroy(lettering);
        }
    }
}
