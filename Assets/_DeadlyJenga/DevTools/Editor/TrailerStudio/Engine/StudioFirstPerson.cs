using System.Collections.Generic;
using DeadlyJenga.CandyPlayers;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // First-person shots: "playing as" a recorded survivor.
    //
    // How to set one up: a camera with behavior "firstPerson" and subject = the survivor's mark id. Keys only give
    // the shot its start and end (their positions are ignored). The settings, on StudioCameraTrack:
    //   fpSmoothMove  .25 s  smooths where the eye is (head bob, recording jitter). Never more than 12 cm off the head.
    //   fpSmoothTurn  .45 s  smooths where the eye looks. Never more than 30 degrees off the head.
    //   fpLevel       true   keeps the horizon level (head roll removed).
    //   fpFieldOfView 62     vertical degrees (the keys' focal length is replaced; 0 keeps it).
    //   fpOwnBody     true   draws the survivor the way the game draws the local player: its arms, hands and held
    //                        weapon are seen, its head, hair, hats and upper torso are not (they still cast shadows).
    //                        The eye has no depth of field and a near clip of 4 cm, like the game's own camera.
    // Setting all five to 0 / false gives the old raw head lock.
    //
    // The smoothing is centred on the shown moment (a recording can look ahead), so a fall or a knockback does not
    // leave the camera behind the body. The hands and the weapon are not smoothed: they sit where the recording
    // puts them, so with strong smoothing they drift a little in the frame, like a stabilised camera.
    //
    // The own-body view is the game's own: CandyPlayerRig draws itself in first person for its ownerCamera (the
    // local player's camera in a match). The Studio lends a played-back rig the shot's camera as its ownerCamera,
    // for that camera only, so every other camera in the same take still sees the whole body.
    public static class StudioFirstPerson
    {
        const float MaxMove = .12f, MaxTurn = 30, Near = .04f;
        const int Steps = 8;   // samples on each side of the shown moment

        // Where the eye is and where it looks at a tape time. False when the actor is not shown then.
        public static bool Eye(StudioCameraTrack camera, StudioSet set, StudioTape tape, float time, out Vector3 position, out Quaternion rotation)
        {
            var now = StudioActorPoses.At(tape, camera.subject, time, set.fps);
            position = now.head; rotation = now.headRotation;
            if (!now.found) return false;
            float move = Mathf.Max(0, camera.fpSmoothMove), turn = Mathf.Max(0, camera.fpSmoothTurn);
            if (move > .001f || turn > .001f)
            {
                // A Gaussian average over the moments around this one; each smoothing time reaches that far either side.
                float reach = Mathf.Max(move, turn);
                Vector3 sumPosition = Vector3.zero, sumForward = Vector3.zero, sumUp = Vector3.zero;
                float positionWeights = 0;
                for (int i = -Steps; i <= Steps; i++)
                {
                    float offset = reach * i / Steps;
                    var pose = i == 0 ? now : StudioActorPoses.At(tape, camera.subject, time + offset, set.fps);
                    if (!pose.found) continue;
                    float Weight(float span) => span <= .001f ? (i == 0 ? 1 : 0) : Mathf.Exp(-2 * (offset / span) * (offset / span));
                    float p = Weight(move), r = Weight(turn);
                    sumPosition += pose.head * p; positionWeights += p;
                    sumForward += pose.headRotation * Vector3.forward * r; sumUp += pose.headRotation * Vector3.up * r;
                }
                if (positionWeights > 0) position = now.head + Vector3.ClampMagnitude(sumPosition / positionWeights - now.head, MaxMove);
                if (sumForward.sqrMagnitude > 1e-6f && sumUp.sqrMagnitude > 1e-6f)
                    rotation = Quaternion.RotateTowards(now.headRotation, Quaternion.LookRotation(sumForward, sumUp), MaxTurn);
            }
            if (camera.fpLevel) rotation = Level(rotation);
            return true;
        }

        // The same direction of view with the roll taken out.
        static Quaternion Level(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            // Straight up or down has no horizon: keep the head's own up there.
            if (Mathf.Abs(forward.y) > .995f) return rotation;
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        // ---- Drawing the actor as its player sees it ----

        sealed class Lent { public CandyPlayerRig rig; public float near; }
        static readonly Dictionary<Camera, Lent> lent = new Dictionary<Camera, Lent>();

        // Called with every shot a camera shows (StudioCameras.Apply). markId empty = not a first-person own-body shot.
        public static void Show(Camera camera, string markId)
        {
            if (!camera) return;
            if (lent.Count > 0) Forget();
            var stage = StudioRun.Stage;
            CandyPlayerRig rig = null;
            if (!string.IsNullOrEmpty(markId) && stage != null)
            {
                // Only played-back copies: a live actor's rig already belongs to its own player's camera.
                if (stage.playback.TryGetValue(markId, out var shown) && shown) rig = shown.GetComponentInChildren<CandyPlayerRig>(true);
                if (rig && rig.ownerCamera && rig.ownerCamera != camera) rig = null;
            }
            if (!rig) { Release(camera); return; }
            if (!lent.TryGetValue(camera, out var held)) lent[camera] = held = new Lent { near = camera.nearClipPlane };
            if (held.rig && held.rig != rig && held.rig.ownerCamera == camera) held.rig.ownerCamera = null;
            held.rig = rig;
            rig.ownerCamera = camera;
            camera.nearClipPlane = Near;
        }

        // The camera stops looking through an actor's eyes: give the rig back and restore the lens.
        public static void Release(Camera camera)
        {
            if (!camera || !lent.TryGetValue(camera, out var held)) return;
            if (held.rig && held.rig.ownerCamera == camera) held.rig.ownerCamera = null;
            camera.nearClipPlane = held.near;
            lent.Remove(camera);
        }

        // Cameras that were destroyed (a finished render's camera): drop them.
        static void Forget()
        {
            List<Camera> gone = null;
            foreach (var pair in lent) if (!pair.Key) (gone ??= new List<Camera>()).Add(pair.Key);
            if (gone != null) foreach (var camera in gone) lent.Remove(camera);
        }

        // For tests: the rig a camera is looking out of right now, or null.
        public static CandyPlayerRig LentTo(Camera camera) => camera && lent.TryGetValue(camera, out var held) && held.rig && held.rig.ownerCamera == camera ? held.rig : null;
    }
}
