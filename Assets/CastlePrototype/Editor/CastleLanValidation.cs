using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using P = DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastleLanValidation
    {
        const string Request = "Temp/LanValidation/check.request";
        static CastleLanValidation()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
                File.Delete(Request);
                try { Run(); }
                catch (Exception e) { File.WriteAllText("Temp/LanValidation/editor-results.txt", "FAIL " + e); Debug.LogException(e); }
            };
        }
        [MenuItem("Tools/Castle Prototype/Validate Local Multiplayer Protocol")]
        public static void Run()
        {
            Directory.CreateDirectory("Temp/LanValidation");
            var result = new StringBuilder();
            void Check(bool ok, string description) { if (!ok) throw new Exception(description); result.AppendLine("PASS " + description); }
            var left = new CandyHandPose { tracked = true, skeletonTracked = true, position = new Vector3(1, 2, 3), rotation = Quaternion.Euler(10, 20, 30), skeleton = CandyHandSkeleton.Rest(true, .3f) };
            var frame = new CandyAvatarFrame { feet = Vector3.one, head = Vector3.up * 2, headRotation = Quaternion.identity, scale = .5f, left = left, right = left };
            var s = new P.Snapshot { revision = 9, settings = JsonUtility.ToJson(new CastleSettings()), origin = new P.Pose(Vector3.zero, Quaternion.identity), originScale = Vector3.one,
                blocks = new[] { new P.Pose(new Vector3(7, 2, -6), Quaternion.Euler(25, 80, 3)), new P.Pose(new Vector3(-3, -12, 2), Quaternion.identity) }, active = new[] { true, false },
                actors = new[] { new P.Actor { id = "human", avatar = frame }, new P.Actor { id = "hand", hand = true, target = left, size = 20, left = true, bones = Enumerable.Repeat(new P.Pose(Vector3.one, Quaternion.identity), 16).ToArray() } } };
            var encoded = P.EncodeWorld(s); var restored = P.DecodeWorld(encoded);
            Check(restored.revision == 9 && restored.settings == s.settings && !restored.active[1], "Late-join snapshot preserves generation, layout and removed blocks");
            Check(Vector3.Distance(restored.blocks[0].position, s.blocks[0].position) < .0001f && Quaternion.Angle(restored.blocks[0].rotation, s.blocks[0].rotation) < .01f, "Moved and rotated blocks round-trip exactly");
            Check(restored.actors[1].bones.Length == 16 && restored.actors[1].left && restored.actors[0].avatar.scale == .5f, "Survivor and all sixteen physical hand bones round-trip");
            Check(Vector3.Distance(restored.actors[0].avatar.left.skeleton.Joint(7), left.skeleton.Joint(7)) < .001f, "Tracked finger pose retains millimetre precision");
            bool truncated = false;
            try { P.DecodeWorld(encoded.Take(encoded.Length - 1).ToArray()); } catch (EndOfStreamException) { truncated = true; }
            Check(truncated, "Truncated snapshots are rejected");
            bool invalid = false;
            s.blocks[0].position.x = float.NaN;
            try { P.DecodeWorld(P.EncodeWorld(s)); } catch (InvalidDataException) { invalid = true; }
            Check(invalid, "Non-finite coordinates are rejected before scene mutation");
            s.blocks[0].position.x = 7;
            s.actors = Enumerable.Range(0, 24).Select(i => new P.Actor { id = "human:" + i, avatar = frame }).ToArray();
            Check(P.DecodeWorld(P.EncodeWorld(s)).actors.Length == 24, "Protocol supports more than ten human players");
            int count = CastleLayout.Build(new CastleSettings()).Count;
            s.blocks = Enumerable.Repeat(new P.Pose(Vector3.one, Quaternion.identity), count).ToArray(); s.active = Enumerable.Repeat(true, count).ToArray();
            int bytes = P.EncodeWorld(s).Length;
            Check(bytes < CastleLanTransport.MaxPacket, "Full castle with 24 players fits one bounded state message (" + bytes + " bytes; " + count + " blocks)");
            var preview=EditorSceneManager.NewPreviewScene();
            var settings=ScriptableObject.CreateInstance<CandyHandSettings>();settings.size=1;
            try
            {
                var go=new GameObject("Isolated LAN hand check");SceneManager.MoveGameObjectToScene(go,preview);
                var hand=go.AddComponent<CandyPhysicalHand>();hand.settings=settings;hand.left=true;
                var pose=left;pose.position=Vector3.zero;pose.rotation=Quaternion.identity;hand.Build(pose);
                hand.NetworkPreview=true;hand.SetInteractive(true);hand.SetTarget(pose,1);hand.StepDrives(.02f);
                Check(hand.bodies.All(b=>b.isKinematic)&&hand.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Client hand preview cannot apply forces to the castle");
                hand.NetworkPreview=false;hand.SetInteractive(true);
                Check(hand.IsInteractive&&hand.bodies.All(b=>!b.isKinematic),"Leaving the network restores authoritative hand physics");
                hand.SetInteractive(false);
                Check(hand.bodies.All(b=>b.isKinematic)&&hand.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Remote display hand can remain entirely non-interacting");
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);UnityEngine.Object.DestroyImmediate(settings);}
#if UNITY_ANDROID
            var folder = "Temp/LanValidation/Android/src/main"; Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/AndroidManifest.xml", "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application><activity android:name=\"existing.activity\" /></application></manifest>");
            var hook = new CastleLanAndroidBuild(); hook.OnPostGenerateGradleAndroidProject("Temp/LanValidation/Android"); hook.OnPostGenerateGradleAndroidProject("Temp/LanValidation/Android");
            var manifest = XDocument.Load(folder + "/AndroidManifest.xml");
            Check(manifest.Root.Elements("uses-permission").Count() == 3 && manifest.Root.Element("application").Element("activity") != null, "Android permission merge is idempotent and preserves activity configuration");
#endif
            File.WriteAllText("Temp/LanValidation/editor-results.txt", result.ToString()); Debug.Log(result.ToString());
        }
    }
}
