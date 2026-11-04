using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [Serializable]
    public sealed class TheatreStuntPose
    {
        public float time;
        public Vector3[] joints;

        internal const int Hips = 0, Chest = 1, Head = 2, LeftShoulder = 3, LeftElbow = 4, LeftHand = 5,
            RightShoulder = 6, RightElbow = 7, RightHand = 8, LeftHip = 9, LeftKnee = 10, LeftFoot = 11,
            RightHip = 12, RightKnee = 13, RightFoot = 14, JointCount = 15;

        internal static bool ValidTrack(TheatreStuntPose[] track)
        {
            if (track.Length < 2 || track.Length > 3601) return false;
            float previous = -1;
            foreach (var key in track)
            {
                if (key == null || !float.IsFinite(key.time) || key.time < 0 || key.time <= previous ||
                    key.joints == null || key.joints.Length != JointCount ||
                    key.joints.Any(j => !float.IsFinite(j.sqrMagnitude) || j.sqrMagnitude > 40000)) return false;
                previous = key.time;
            }
            return true;
        }

        internal static Vector3 Present(TheatreStuntPose[] track, CandyPlayerAvatar avatar, Transform root, Vector3 stage, float time)
        {
            int low = 0, high = track.Length - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (track[middle].time <= time) low = middle;
                else high = middle;
            }
            var a = track[low];
            var b = track[high];
            float u = Mathf.InverseLerp(a.time, b.time, time);
            Vector3 Joint(int joint) => stage + Vector3.LerpUnclamped(a.joints[joint], b.joints[joint], u);
            var hips = Joint(Hips);
            var leftFoot = Joint(LeftFoot);
            var rightFoot = Joint(RightFoot);
            var feet = new Vector3(hips.x, Mathf.Min(leftFoot.y, rightFoot.y) - .045f, hips.z);
            var right = Joint(RightShoulder) - Joint(LeftShoulder);
            var spine = Joint(Chest) - hips;
            var forward = Vector3.Cross(right, spine);
            var horizontal = Vector3.ProjectOnPlane(forward, Vector3.up);
            var facing = horizontal.sqrMagnitude > .001f ? Quaternion.LookRotation(horizontal) : root.rotation;
            root.SetPositionAndRotation(feet, facing);
            var frame = avatar.ReadFrame();
            frame.feet = feet;
            frame.head = Joint(Head) + Vector3.up * .08f * frame.scale;
            frame.headRotation = facing;
            frame.left = Hand(LeftElbow, LeftHand, true);
            frame.right = Hand(RightElbow, RightHand, false);
            avatar.OverridePose(frame);
            avatar.ApplyCosmeticFrame(frame);
            var rig = avatar.Rig;
            if (!rig || rig.BoneCount < 14) throw new InvalidOperationException("The candy stunt rig lacks its body bones.");
            var torso = rig.BodyAnchor;
            var leftShoulderAnchor = torso.InverseTransformPoint(rig.AppearanceBone(2).position);
            var rightShoulderAnchor = torso.InverseTransformPoint(rig.AppearanceBone(5).position);
            var leftHipAnchor = torso.InverseTransformPoint(rig.AppearanceBone(8).position);
            var rightHipAnchor = torso.InverseTransformPoint(rig.AppearanceBone(11).position);
            if (forward.sqrMagnitude > .001f && spine.sqrMagnitude > .001f)
            {
                var hipPivot = (leftHipAnchor + rightHipAnchor) * .5f;
                var shoulderPivot = (leftShoulderAnchor + rightShoulderAnchor) * .5f;
                var targetHips = (Joint(LeftHip) + Joint(RightHip)) * .5f;
                var targetShoulders = (Joint(LeftShoulder) + Joint(RightShoulder)) * .5f;
                var axis = targetShoulders - targetHips;
                var priorLength = torso.TransformVector(shoulderPivot - hipPivot).magnitude;
                if (axis.sqrMagnitude > .001f && priorLength > .01f)
                {
                    var bodyForward = Vector3.Cross(right, axis);
                    if (bodyForward.sqrMagnitude > .001f) torso.rotation = Quaternion.LookRotation(bodyForward, axis);
                    var torsoScale = torso.localScale;
                    torsoScale.y *= Mathf.Clamp(axis.magnitude / priorLength, .66f, 1.65f);
                    torso.localScale = torsoScale;
                    torso.position += targetHips - torso.TransformPoint(hipPivot);
                }
            }
            AimChain(2, torso.TransformPoint(leftShoulderAnchor), Joint(LeftElbow), frame.left.position, frame.left.rotation);
            AimChain(5, torso.TransformPoint(rightShoulderAnchor), Joint(RightElbow), frame.right.position, frame.right.rotation);
            AimChain(8, torso.TransformPoint(leftHipAnchor), Joint(LeftKnee), leftFoot, facing);
            AimChain(11, torso.TransformPoint(rightHipAnchor), Joint(RightKnee), rightFoot, facing);
            return feet;

            CandyHandPose Hand(int elbow, int wrist, bool left)
            {
                var p = Joint(wrist);
                var direction = p - Joint(elbow);
                var up = Vector3.ProjectOnPlane(Vector3.up, direction);
                var rotation = direction.sqrMagnitude > .00001f && up.sqrMagnitude > .00001f
                    ? Quaternion.LookRotation(direction, up) : facing;
                return new CandyHandPose { tracked = true, skeletonTracked = true, position = p,
                    rotation = rotation, skeleton = CandyHandSkeleton.Rest(left, .68f) };
            }

            void AimChain(int index, Vector3 upper, Vector3 hinge, Vector3 endpoint, Quaternion rotation)
            {
                var upperBone = rig.AppearanceBone(index);
                var lowerBone = rig.AppearanceBone(index + 1);
                var end = rig.AppearanceBone(index + 2);
                float upperLength = Vector3.Distance(upperBone.position, lowerBone.position);
                float lowerLength = Vector3.Distance(lowerBone.position, end.position);
                Aim(upperBone, upper, hinge, upperLength);
                Aim(lowerBone, hinge, endpoint, lowerLength);
                end.SetPositionAndRotation(endpoint, rotation);
            }

            void Aim(Transform bone, Vector3 start, Vector3 end, float previousLength)
            {
                var direction = end - start;
                if (direction.sqrMagnitude < .00001f) return;
                var normal = Vector3.ProjectOnPlane(facing * Vector3.forward, direction);
                if (normal.sqrMagnitude < .00001f) normal = Vector3.ProjectOnPlane(Vector3.up, direction);
                if (normal.sqrMagnitude < .00001f) normal = Vector3.ProjectOnPlane(Vector3.right, direction);
                bone.SetPositionAndRotation(start, Quaternion.LookRotation(normal, direction));
                var scale = bone.localScale;
                scale.y *= direction.magnitude / Mathf.Max(.001f, previousLength);
                bone.localScale = scale;
            }
        }
    }

    public sealed class TheatreStuntImport : EditorWindow
    {
        static readonly string[][] Names =
        {
            new[] { "Hips", "Pelvis" }, new[] { "Chest", "Spine2", "Spine3", "Spine02", "Spine1", "Spine" }, new[] { "Head" },
            new[] { "LeftArm", "LeftUpperArm", "UpperArmL" }, new[] { "LeftForeArm", "LeftLowerArm", "LowerArmL" }, new[] { "LeftHand", "HandL" },
            new[] { "RightArm", "RightUpperArm", "UpperArmR" }, new[] { "RightForeArm", "RightLowerArm", "LowerArmR" }, new[] { "RightHand", "HandR" },
            new[] { "LeftUpLeg", "LeftThigh", "ThighL" }, new[] { "LeftLeg", "LeftShin", "CalfL" }, new[] { "LeftFoot", "FootL" },
            new[] { "RightUpLeg", "RightThigh", "ThighR" }, new[] { "RightLeg", "RightShin", "CalfR" }, new[] { "RightFoot", "FootR" }
        };

        [Serializable]
        sealed class Source
        {
            public string actor;
            public GameObject model;
            public AnimationClip clip;
            public float trim;
        }

        [Serializable] sealed class PairFile
        {
            public Vector3 origin;
            public float yaw, scale = .65f, duration = 6;
            public FileSource[] performers;
        }

        [Serializable] sealed class FileSource
        {
            public string actor, model, clip;
            public float trim;
        }

        // Explicit batch entry point for repeatable vendor-FBX import verification.
        public static void BakePairBatch()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                string Argument(string flag)
                {
                    int index = Array.IndexOf(args, flag);
                    if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing " + flag);
                    return args[index + 1];
                }
                var plan = JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(Argument("-theatrePlan")));
                var pair = JsonUtility.FromJson<PairFile>(File.ReadAllText(Argument("-stuntPair")));
                if (plan == null || pair?.performers?.Length != 2 || pair.performers.Any(item => item == null))
                    throw new InvalidDataException("Provide a Theatre recipe and exactly two performer entries.");
                if (pair.performers[0].actor == pair.performers[1].actor)
                    throw new InvalidDataException("Performers need different cast IDs.");
                foreach (var source in pair.performers)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(source.model);
                    var clips = AssetDatabase.LoadAllAssetsAtPath(source.model).OfType<AnimationClip>()
                        .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                            (string.IsNullOrEmpty(source.clip) || clip.name == source.clip)).ToArray();
                    if (clips.Length != 1)
                        throw new InvalidDataException($"{source.model} has {clips.Length} matching clips. Supply a unique clip name.");
                    var cast = plan.cast.Single(actor => actor.id == source.actor);
                    SetTrack(cast, Bake(model, clips[0], source.trim, pair.duration, plan.fps, pair.origin, pair.yaw, pair.scale));
                    Debug.Log($"Baked {cast.stunt.Length} full-body samples from {source.model} for {cast.id}.");
                }
                SaveRecipe(plan, pair.duration, Argument("-stuntBakedRecipe"));
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        [SerializeField] Source first = new() { actor = "blue" };
        [SerializeField] Source second = new() { actor = "red" };
        [SerializeField] Vector3 origin;
        [SerializeField] float yaw, scale = .65f, duration = 6;
        [SerializeField] string recipe = "";
        string status;

        [MenuItem("Tools/Deadly Jenga/Stunt Motion Import")]
        static void Open() => GetWindow<TheatreStuntImport>("Stunt Motion Import");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Use two Generic FBX clips exported from the same take and coordinate origin. Set trim to the same clap in both clips.", MessageType.Info);
            Draw(first, "Performer A");
            Draw(second, "Performer B");
            origin = EditorGUILayout.Vector3Field("Shared source floor origin", origin);
            yaw = EditorGUILayout.FloatField("Shared rotation", yaw);
            scale = EditorGUILayout.FloatField("Stage units per source unit", scale);
            duration = EditorGUILayout.FloatField("Duration in seconds", duration);
            EditorGUILayout.LabelField("Source Theatre recipe", recipe);
            if (GUILayout.Button("Choose recipe"))
                recipe = EditorUtility.OpenFilePanel("Open two-cast Theatre recipe", "", "json");
            if (GUILayout.Button("Bake motion and save new recipe"))
                try
                {
                    var plan = JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(recipe));
                    if (plan == null || first.actor == second.actor) throw new InvalidDataException("Choose two different cast IDs.");
                    foreach (var source in new[] { first, second })
                    {
                        var cast = plan.cast.Single(actor => actor.id == source.actor);
                        SetTrack(cast, Bake(source.model, source.clip, source.trim, duration, plan.fps, origin, yaw, scale));
                    }
                    var path = EditorUtility.SaveFilePanel("Save baked stunt take", Path.GetDirectoryName(recipe), "stunt-take", "json");
                    if (path.Length > 0)
                    {
                        SaveRecipe(plan, duration, path);
                        status = "Baked both performers. Open the saved recipe in Gameplay Theatre.";
                    }
                }
                catch (Exception error) { status = error.Message; }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        }

        static void SetTrack(TheatrePlan.TheatreActor actor, TheatreStuntPose[] track)
        {
            actor.stunt = track;
            actor.live = false;
            actor.moves = null;
            actor.poses = null;
            actor.performance = null;
        }

        static void SaveRecipe(TheatrePlan plan, float duration, string path)
        {
            plan.replaySource = "";
            plan.Validate();
            float required = plan.shots.Max(shot => (Mathf.RoundToInt(shot.start * plan.fps) +
                Mathf.CeilToInt(shot.duration * plan.fps) - 1) / (float)plan.fps);
            if (duration + .001f < required)
                throw new InvalidDataException($"The cameras need at least {required:F2} seconds of motion.");
            using var stream = new FileStream(path, FileMode.CreateNew);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonUtility.ToJson(plan, true));
        }

        static void Draw(Source source, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            source.actor = EditorGUILayout.TextField("Cast ID", source.actor);
            source.model = (GameObject)EditorGUILayout.ObjectField("Source FBX model", source.model, typeof(GameObject), false);
            source.clip = (AnimationClip)EditorGUILayout.ObjectField("Animation clip", source.clip, typeof(AnimationClip), false);
            source.trim = EditorGUILayout.FloatField("Source time at slate", source.trim);
        }

        static TheatreStuntPose[] Bake(GameObject model, AnimationClip clip, float trim, float duration, int fps,
            Vector3 origin, float yaw, float scale)
        {
            if (!model || !clip || clip.isHumanMotion || !float.IsFinite(duration) || !float.IsFinite(trim) ||
                !float.IsFinite(yaw) || !float.IsFinite(origin.sqrMagnitude) || !float.IsFinite(scale) ||
                duration < .25f || duration > 60 || fps < 12 || fps > 60 || trim < 0 ||
                trim + duration > clip.length + .001f || scale <= 0)
                throw new InvalidDataException("Use a Generic rig clip with the full trim range and a positive shared scale.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var clone = EditorUtility.IsPersistent(model)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(model, scene) : Object.Instantiate(model);
                if (clone.scene != scene) SceneManager.MoveGameObjectToScene(clone, scene);
                var rig = clone.GetComponentsInChildren<Transform>(true);
                var joints = Names.Select(aliases => aliases
                    .Select(alias => rig.FirstOrDefault(bone => Normalize(bone.name) == Normalize(alias)))
                    .FirstOrDefault(bone => bone) ?? throw new InvalidDataException("Missing source joint: " + aliases[0])).ToArray();
                var rotation = Quaternion.Euler(0, yaw, 0);
                int count = Mathf.CeilToInt(duration * fps) + 1;
                var result = new TheatreStuntPose[count];
                for (int i = 0; i < count; i++)
                {
                    float t = Mathf.Min(duration, i / (float)fps);
                    clip.SampleAnimation(clone, trim + t);
                    result[i] = new TheatreStuntPose { time = t,
                        joints = joints.Select(joint => rotation * (joint.position - origin) * scale).ToArray() };
                }
                if (!TheatreStuntPose.ValidTrack(result)) throw new InvalidDataException("Baked motion contains invalid samples.");
                if (result.All(pose => (pose.joints[TheatreStuntPose.LeftHand] - result[0].joints[TheatreStuntPose.LeftHand]).sqrMagnitude < .000001f &&
                    (pose.joints[TheatreStuntPose.RightFoot] - result[0].joints[TheatreStuntPose.RightFoot]).sqrMagnitude < .000001f))
                    throw new InvalidDataException("No hand or foot motion sampled. Check the FBX and clip binding.");
                return result;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static string Normalize(string value) => new string(value.Split(':').Last().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        internal static void AttachFixture(TheatrePlan plan)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                for (int i = 0; i < plan.cast.Length; i++)
                {
                    var model = new GameObject("Stunt binding fixture");
                    SceneManager.MoveGameObjectToScene(model, scene);
                    var clip = new AnimationClip { legacy = true };
                    float sign = i % 2 == 0 ? 1 : -1;
                    for (int j = 0; j < Names.Length; j++)
                    {
                        var bone = new GameObject(Names[j][0]).transform;
                        bone.SetParent(model.transform, false);
                        bool left = j >= 3 && j <= 5 || j >= 9 && j <= 11;
                        bool arm = j >= 3 && j <= 8;
                        float side = left ? -1 : 1;
                        float y = j == 0 ? .82f : j == 1 ? 1.3f : j == 2 ? 1.62f :
                            arm ? (j - 3) % 3 == 0 ? 1.32f : 1.15f : (j - 9) % 3 == 0 ? .82f : (j - 9) % 3 == 1 ? .42f : .075f;
                        float x = j <= 2 ? 0 : side * (arm ? .23f : .12f);
                        float z = arm ? (j - 3) % 3 * .20f : .02f;
                        Vector3 point = Quaternion.Euler(0, 90 * sign, 0) * new Vector3(x, y, z) + new Vector3(-.72f * sign, 0, 0);
                        foreach (var channel in new[] { "x", "y", "z" })
                        {
                            float initial = channel == "x" ? point.x : channel == "y" ? point.y : point.z;
                            float reach = channel == "x" ? sign * (j == 8 ? .52f : .11f) :
                                channel == "y" && j == 14 ? .14f : 0;
                            clip.SetCurve(bone.name, typeof(Transform), "localPosition." + channel,
                                new AnimationCurve(new Keyframe(0, initial), new Keyframe(.55f + i * .18f, initial + reach),
                                    new Keyframe(1.25f, initial), new Keyframe(8, initial)));
                        }
                    }
                    try
                    {
                        var cast = plan.cast[i];
                        SetTrack(cast, Bake(model, clip, 0, 7, plan.fps, Vector3.zero, 0, .72f));
                        Debug.Log($"Stunt fixture sampled {cast.stunt.Length} full-body poses for {cast.id}.");
                    }
                    finally { Object.DestroyImmediate(clip); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
