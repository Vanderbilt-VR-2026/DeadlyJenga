#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CrossProcessSmoke
    {
        static bool JunctionTest => Environment.GetCommandLineArgs().Contains("--dj-sphinx-junction");

        IEnumerator CheckJunctionPeer(SphinxJunctionMap map, CastlePlaySession session, bool host)
        {
            var shoe = map.Blocks.Single(b => b.name == "Timber trestle bent 10");
            var span = map.Blocks.Single(b => b.name == "Railway span 10");
            var engine = map.Blocks.Single(b => b.name == "Copper Canyon locomotive");
            var grab = shoe.GetComponent<IGrabbable>();
            Require(shoe.isKinematic != host && engine.isKinematic, "World authority and rail drive have the expected owners");
            var engineStart = engine.position;
            var initialDeck = span.position;
            yield return Until(() => Vector3.Distance(engine.position, engineStart) > 1f,
                "Intact locomotive advances on this peer", 20);

            if (host)
            {
                yield return Until(() => File.Exists(Mark("junction-guest-arrived")), "Guest observed initial train motion");
                var grip = shoe.worldCenterOfMass;
                Require(grab.TryStartGrab(session.gameObject, grip), "Host hand acquires the east timber shoe");
                float end = Time.fixedTime + 1f;
                while (Time.fixedTime < end)
                {
                    grab.Hold(grip + map.transform.right * 10);
                    yield return new WaitForFixedUpdate();
                }
                grab.Release();
            }
            else
            {
                Require(!grab.TryStartGrab(session.gameObject, shoe.position), "Guest cannot pull the authoritative shoe locally");
                File.WriteAllText(Mark("junction-guest-arrived"), "ready");
            }
            yield return Until(() => Vector3.Distance(span.position, initialDeck) > .8f,
                "East deck moves after its support is pulled", 20);
            if (host)
            {
                yield return Until(() => File.Exists(Mark("junction-guest-collapse")), "Guest observed the resulting deck displacement");
                // Allow a complete circuit even when the shoe was pulled directly behind the engine.
                yield return new WaitForSeconds(65);
                var stopped = engine.position;
                yield return new WaitForSeconds(1.5f);
                Require(Vector3.Distance(stopped, engine.position) < .15f, "Host train has stopped after route loss");
                var pose = new DisplacedBlock { active = engine.gameObject.activeSelf, position = engine.position, rotation = engine.rotation };
                File.WriteAllText(Mark("junction-stop.tmp"), JsonUtility.ToJson(pose));
                File.Move(Mark("junction-stop.tmp"), Mark("junction-stop"));
                yield return Until(() => File.Exists(Mark("junction-guest-stop")), "Guest acknowledged the host train pose");
            }
            else
            {
                File.WriteAllText(Mark("junction-guest-collapse"), "seen");
                yield return Until(() => File.Exists(Mark("junction-stop")), "Host completed the damaged-track approach", 150);
                var pose = JsonUtility.FromJson<DisplacedBlock>(File.ReadAllText(Mark("junction-stop")));
                yield return Until(() => engine.gameObject.activeSelf == pose.active &&
                    Vector3.Distance(engine.position, pose.position) < .28f && Quaternion.Angle(engine.rotation, pose.rotation) < 3,
                    "Replicated stopped train matches the host", 15);
                File.WriteAllText(Mark("junction-guest-stop"), "verified");
            }
        }
    }
}
#endif
