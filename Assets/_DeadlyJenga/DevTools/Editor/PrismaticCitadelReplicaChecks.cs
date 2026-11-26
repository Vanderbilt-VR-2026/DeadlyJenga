using System;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    internal static class PrismaticCitadelReplicaChecks
    {
        public static void Run(CastleGenerator source, GameObject prefab, Action<bool, string> check)
        {
            var snapshot = new CastleLanProtocol.Snapshot
            {
                revision = source.Revision,
                settings = source.SaveLayout(),
                origin = new CastleLanProtocol.Pose(source.transform.position, source.transform.rotation),
                originScale = source.transform.localScale,
                blocks = new CastleLanProtocol.Pose[source.Bodies.Count],
                active = new bool[source.Bodies.Count],
                actors = Array.Empty<CastleLanProtocol.Actor>()
            };
            for (int index = 0; index < source.Bodies.Count; index++)
            {
                var body = source.Bodies[index];
                snapshot.blocks[index] = new CastleLanProtocol.Pose(body.position, body.rotation);
                snapshot.active[index] = body.gameObject.activeSelf;
            }

            // Stage one displaced block and one deletion in the source snapshot.
            int displaced = snapshot.blocks.Length / 2;
            int deleted = snapshot.blocks.Length - 1;
            var initial = snapshot.blocks[displaced];
            snapshot.blocks[displaced] = new CastleLanProtocol.Pose(initial.position + new Vector3(1.2f, -.6f, .5f),
                Quaternion.Euler(10, 30, 5) * initial.rotation);
            snapshot.active[deleted] = false;
            var bytes = CastleLanProtocol.EncodeWorld(snapshot);
            var received = CastleLanProtocol.DecodeWorld(bytes);
            check(received.settings == snapshot.settings && received.revision == snapshot.revision &&
                received.blocks.Length == snapshot.blocks.Length && !received.active[deleted],
                $"World protocol round-trip carried {received.blocks.Length} body slots in {bytes.Length} bytes");

            IWorldQuery originalWorld = Services.World;
            var instance = Object.Instantiate(prefab);
            try
            {
                var replica = instance.GetComponent<CastleGenerator>();
                var origin = new Pose(received.origin.position, received.origin.rotation);
                check(!replica.LoadLayout(received.settings, received.blocks.Length - 1, origin, received.originScale),
                    "Replica rejects a mismatched body count");
                if (!replica.LoadLayout(received.settings, received.blocks.Length, origin, received.originScale))
                    throw new InvalidOperationException("Prismatic Citadel replica rejected the decoded host layout.");

                var expected = CastleLayout.Build(source.settings);
                int layoutMismatches = 0;
                for (int index = 0; index < replica.Bodies.Count; index++)
                {
                    var body = replica.Bodies[index];
                    var placement = expected[index];
                    if (body.name != source.Bodies[index].name || body.transform.localScale != placement.BlockSize(source.settings)
                        || !SamePose(body, new CastleLanProtocol.Pose(replica.transform.TransformPoint(placement.Position),
                            replica.transform.rotation * placement.Rotation))) layoutMismatches++;
                }
                check(layoutMismatches == 0, $"All {replica.Bodies.Count} replica indices match authored poses, names and dimensions");

                replica.SetReplica(true);
                for (int index = 0; index < replica.Bodies.Count; index++)
                {
                    var body = replica.Bodies[index];
                    body.position = received.blocks[index].position;
                    body.rotation = received.blocks[index].rotation;
                    body.gameObject.SetActive(received.active[index]);
                }
                Physics.SyncTransforms();
                int snapshotMismatches = 0;
                for (int index = 0; index < replica.Bodies.Count; index++)
                {
                    var body = replica.Bodies[index];
                    if (!body.isKinematic || body.gameObject.activeSelf != snapshot.active[index]
                        || !SamePose(body, snapshot.blocks[index])) snapshotMismatches++;
                }
                check(snapshotMismatches == 0,
                    $"Replica applied all authoritative poses and active bits, including displaced index {displaced} and deleted index {deleted}");
            }
            finally
            {
                instance.SetActive(false);
                Services.World = originalWorld;
                Object.Destroy(instance);
            }
        }

        static bool SamePose(Rigidbody body, CastleLanProtocol.Pose pose) =>
            (body.position - pose.position).sqrMagnitude < .000001f && Quaternion.Angle(body.rotation, pose.rotation) < .05f;
    }
}
