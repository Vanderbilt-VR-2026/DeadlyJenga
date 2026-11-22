#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Explicit desktop peer fixture: move a real local controller and observe its remote avatar.
    [DefaultExecutionOrder(10000)]
    public sealed class CrossProcessMotionEvidence : MonoBehaviour
    {
        string output, role, otherRole, disaster;
        CastlePlaySession session;
        CastleFusionSession sync;
        CastleFusionSession.LivePoseTraffic traffic;
        StreamWriter samples;
        CharacterController controller;
        SurvivorNetworkView peer;
        Camera observer;
        RenderTexture renderTarget;
        Texture2D pixels;
        RenderPipeline.StandardRequest renderRequest;
        Vector3 direction;
        double started = -1, lastSample, nextPng, nextReadinessCheck;
        int images, requestedBots = -1;
        bool ready, capture, hazardRequested, completed;
        float drive;
        string Mark(string peerRole, string state) => Path.Combine(output, peerRole + "-motion-" + state);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            var folder = args.FirstOrDefault(arg => arg.StartsWith("--dj-smoke-dir=", StringComparison.Ordinal));
            var roleArg = args.FirstOrDefault(arg => arg.StartsWith("--dj-smoke=", StringComparison.Ordinal));
            if (!args.Contains("--dj-motion-evidence") || folder == null || roleArg == null) return;
            var root = new GameObject("Explicit peer motion evidence");
            DontDestroyOnLoad(root);
            var test = root.AddComponent<CrossProcessMotionEvidence>();
            test.output = Path.GetFullPath(folder.Substring("--dj-smoke-dir=".Length));
            test.role = roleArg.Substring("--dj-smoke=".Length);
            test.otherRole = test.role == "host" ? "guest" : "host";
            test.capture = args.Contains("--dj-motion-capture");
            var hazardArg = args.FirstOrDefault(arg => arg.StartsWith("--dj-motion-disaster=", StringComparison.Ordinal));
            test.disaster = hazardArg?.Substring("--dj-motion-disaster=".Length);
            var botsArg = args.FirstOrDefault(arg => arg.StartsWith("--dj-match-bots=", StringComparison.Ordinal));
            if (botsArg != null && int.TryParse(botsArg.Substring("--dj-match-bots=".Length), out int bots))
                test.requestedBots = bots;
        }

        void Update()
        {
            if (completed || !controller || started < 0 || !session || session.PlayingHand ||
                !controller.enabled || session.Network.HumanCount != 2) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            drive = elapsed >= 1 && elapsed < 3.5 ? 1.4f : elapsed >= 3.5 && elapsed < 6 ? -1.4f : 0;
            if (drive != 0) controller.Move(direction * (Mathf.Min(Time.unscaledDeltaTime, .08f) * drive));
        }

        bool BindParticipants()
        {
            session = Services.Session as CastlePlaySession;
            if (!session || session.PlayingHand || !NetworkActors.Alive(session.Player) ||
                !session.Network || session.Network.WaitingForWorld || session.Network.HumanCount != 2) return false;
            sync = session.GetComponent<CastleFusionSession>();
            if (!sync) return false;
            controller = session.Player.gameObject.GetComponent<CharacterController>();
            if (!controller || !controller.enabled) return false;
            var views = FindObjectsByType<SurvivorNetworkView>(FindObjectsSortMode.None);
            peer = views.FirstOrDefault(view => view.Alive && !view.IsBot && view.Visual);
            if (!peer) return false;
            int observedBots = sync.IsHost ? session.SurvivorBots.Count : views.Count(view => view.IsBot && !view.IsEnemy);
            if (requestedBots >= 0 && observedBots != requestedBots) return false;
            traffic = sync.IsHost ? sync.PeerPoseTraffic.SingleOrDefault() : sync.LocalPoseTraffic;
            return traffic != null;
        }

        void LateUpdate()
        {
            if (completed) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!ready)
            {
                if (now < nextReadinessCheck) return;
                nextReadinessCheck = now + .2;
                if (!BindParticipants()) return;
                Directory.CreateDirectory(output);
                File.WriteAllText(Mark(role, "ready"), "Local and remote survivors ready");
                ready = true;
            }

            if (started < 0)
            {
                if (now < nextReadinessCheck) return;
                nextReadinessCheck = now + .2;
                if (!File.Exists(Mark(otherRole, "ready")) || !BindParticipants()) return;
                started = lastSample = nextPng = now;
                direction = controller.transform.right;
                if (capture && role == "guest") SetUpObserver();
                samples = new StreamWriter(Path.Combine(output, role + "-motion.csv"), false,
                    new System.Text.UTF8Encoding(false), 65536);
                samples.WriteLine("utc_ms,elapsed_s,frame,wall_frame_s,unity_delta_s,drive,local_x,local_y,local_z,rx,ry,rz,head_x,head_y,head_z,left_x,left_y,left_z,right_x,right_y,right_z,rx_total,applied_total,tx_total,rx_interval_s,apply_interval_s,applied_age_s,pose_pending,oldest_pose_pending_s,world_pending_age_s,bots,wave_active,remote_visible");
            }

            double elapsed = now - started;
            if (!hazardRequested && elapsed >= .5 && sync && sync.IsHost && !string.IsNullOrEmpty(disaster))
            {
                hazardRequested = true;
                bool activated = DemoDisasters.ExecuteForMode(session, disaster);
                File.WriteAllText(Path.Combine(output, role + "-motion-disaster.txt"), $"{disaster}: {activated}");
            }
            if (!session || !sync || !peer || !controller)
            {
                completed = true;
                Close();
                File.WriteAllText(Path.Combine(output, role + "-motion-error.txt"), "A bound participant was removed");
                return;
            }
            Record(now);
            if (observer && now >= nextPng)
            {
                nextPng = now + .1;
                try { Capture(now); }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(output, role + "-motion-capture-error.txt"), error.ToString());
                    observer.enabled = false;
                    Destroy(observer.gameObject);
                    observer = null;
                }
            }
            if (elapsed >= 8)
            {
                completed = true;
                Close();
                File.WriteAllText(Mark(role, "done"), "Measured eight seconds");
            }
        }

        void SetUpObserver()
        {
            var cameraObject = new GameObject("Peer evidence fixed camera");
            cameraObject.transform.SetParent(transform);
            observer = cameraObject.AddComponent<Camera>();
            observer.enabled = false;
            var view = peer.ViewPose;
            var front = view.rotation * Vector3.forward;
            front.y = 0;
            front = front.sqrMagnitude > .01f ? front.normalized : Vector3.forward;
            var feet = peer.NetworkPosition;
            var fixedCameraPosition = feet + front * 6f + Vector3.up * 2.5f;
            var center = feet + Vector3.Cross(Vector3.up, front) * 1.7f + Vector3.up;
            observer.transform.SetPositionAndRotation(fixedCameraPosition,
                Quaternion.LookRotation(center - fixedCameraPosition));
            observer.fieldOfView = 56;
            observer.nearClipPlane = .08f;
            observer.farClipPlane = 1200;
            if (Services.Session.ActiveCamera)
            {
                observer.cullingMask = Services.Session.ActiveCamera.cullingMask;
                observer.clearFlags = Services.Session.ActiveCamera.clearFlags;
            }
            renderTarget = new RenderTexture(640, 360, 24);
            renderTarget.Create();
            observer.targetTexture = renderTarget;
            pixels = new Texture2D(640, 360, TextureFormat.RGB24, false);
            renderRequest = new RenderPipeline.StandardRequest { destination = renderTarget };
        }

        void Record(double now)
        {
            var local = controller.transform.position;
            var received = peer.NetworkPosition;
            var head = peer.ViewPose.position;
            var left = peer.HandPose(true).position;
            var right = peer.HandPose(false).position;
            int bots = sync.IsHost ? session.SurvivorBots.Count : sync.Demo.survivors;
            int wave = Tracked.Waves.Any(view => view.Wave.active) ? 1 : 0;
            float poseAge = Time.unscaledTime - traffic.LastAppliedAt;
            float worldAge = sync.OldestPendingWorldAckSeconds ?? -1;
            samples.WriteLine(FormattableString.Invariant(
                $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},{now - started:F6},{Time.frameCount},{now - lastSample:F6},{Time.unscaledDeltaTime:F6},{drive:F2},{local.x:F5},{local.y:F5},{local.z:F5},{received.x:F5},{received.y:F5},{received.z:F5},{head.x:F5},{head.y:F5},{head.z:F5},{left.x:F5},{left.y:F5},{left.z:F5},{right.x:F5},{right.y:F5},{right.z:F5},{traffic.ReceivedFrames},{traffic.AppliedFrames},{traffic.SubmittedFrames},{traffic.LastReceiveInterval:F6},{traffic.LastApplyInterval:F6},{poseAge:F6},{traffic.PendingCount},{traffic.OldestPendingSeconds:F6},{worldAge:F6},{bots},{wave},{(peer.Alive ? 1 : 0)}"));
            lastSample = now;
        }

        void Capture(double now)
        {
            var previous = RenderTexture.active;
            try
            {
                if (RenderPipeline.SupportsRenderRequest(observer, renderRequest))
                    RenderPipeline.SubmitRenderRequest(observer, renderRequest);
                else
                    observer.Render();
                RenderTexture.active = renderTarget;
                pixels.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, $"motion-{images:D4}.jpg"), pixels.EncodeToJPG(85));
                File.AppendAllText(Path.Combine(output, "motion-frames.csv"),
                    string.Format(CultureInfo.InvariantCulture, "{0},{1:F6}\n", images, now - started));
                images++;
            }
            finally { RenderTexture.active = previous; }
        }

        void Close()
        {
            samples?.Dispose();
            samples = null;
            if (observer) Destroy(observer.gameObject);
            if (pixels) Destroy(pixels);
            if (renderTarget)
            {
                renderTarget.Release();
                Destroy(renderTarget);
            }
        }

        void OnDestroy() => Close();
    }
}
#endif
