using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A small library of camera moves, written as keys and aim keys on a camera track.
    // Positions are metres from the stage spot; times are tape seconds. Every move eases in and out
    // (ease 5), so nothing starts or stops like a robot. Moves can be stacked on one camera.
    public static class StudioMoves
    {
        const int Smooth = 5;

        public static StudioCameraTrack Camera(string name, float start, float end, float focal)
        {
            var camera = new StudioCameraTrack { name = name, start = start, end = end, spline = true };
            camera.keys.Add(new StudioCameraKey { time = start, focal = focal, aperture = 16, focus = 10, ease = Smooth });
            return camera;
        }

        static StudioCameraKey Last(StudioCameraTrack camera) => camera.keys[camera.keys.Count - 1];

        static StudioCameraKey At(StudioCameraTrack camera, float time, Vector3 position)
        {
            var before = Last(camera);
            var key = new StudioCameraKey { time = time, position = position, focal = before.focal, aperture = before.aperture, focus = before.focus, shake = before.shake, ease = Smooth };
            StudioCameras.AddKey(camera, key);
            return key;
        }

        // Locked-off: the camera stands still at 'position' and looks at 'target' from 'time'.
        public static void LockedOff(StudioCameraTrack camera, float time, Vector3 position, string target, Vector3 offset = default)
        {
            if (camera.keys.Count == 1 && Mathf.Abs(camera.keys[0].time - time) < .001f) camera.keys[0].position = position;
            else At(camera, time, position);
            Aim(camera, time, target, offset, 0);
        }

        // Push-in: move 'fraction' of the way toward a point (15 to 35 % is the usual range) between two times.
        public static void PushIn(StudioCameraTrack camera, float from, float to, Vector3 toward, float fraction)
        {
            var start = Last(camera).position;
            if (Last(camera).time < from - .001f) At(camera, from, start);
            At(camera, to, Vector3.Lerp(start, toward, fraction));
        }

        // Crane up: rise straight up by 'rise' metres between two times (the aim keeps the subject).
        public static void CraneUp(StudioCameraTrack camera, float from, float to, float rise)
        {
            var start = Last(camera).position;
            if (Last(camera).time < from - .001f) At(camera, from, start);
            At(camera, to, start + Vector3.up * rise);
        }

        // Orbit: an eased arc around a point, from one angle to another (degrees), at a fixed radius and height.
        public static void Orbit(StudioCameraTrack camera, float from, float to, Vector3 centre, float radius, float height, float fromDegrees, float toDegrees, string target)
        {
            const int steps = 6;
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps, eased = u * u * u * (u * (u * 6 - 15) + 10);
                float angle = Mathf.Lerp(fromDegrees, toDegrees, eased) * Mathf.Deg2Rad;
                var key = At(camera, Mathf.Lerp(from, to, u), centre + new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius));
                key.ease = 0;   // the easing is in the angles already; keys are evenly spaced in time
            }
            camera.constantSpeed = false;
            Aim(camera, from, target, Vector3.zero, 0);
        }

        // Tilt-up reveal (or any change of what the camera looks at): ease the aim to a new target.
        public static void TiltTo(StudioCameraTrack camera, float time, string target, Vector3 offset, float seconds) => Aim(camera, time, target, offset, seconds);

        // Follow with lead room: ride along with a target at an offset, aiming 'lead' seconds ahead of it.
        public static void Follow(StudioCameraTrack camera, string target, Vector3 offset, float lead)
        {
            camera.rideTarget = target;
            foreach (var key in camera.keys) key.position = offset;
            camera.leadRoom = lead;
            Aim(camera, camera.start, target, new Vector3(0, 1.1f, 0), 0);
        }

        // Impact shake: rotation only, decaying (1 to 2 degrees for a block near camera, decay 0.3 to 1.5 s).
        public static void Shake(StudioCameraTrack camera, float time, float degrees, float decay) =>
            camera.shakes.Add(new StudioShake { time = time, degrees = degrees, decay = decay });

        public static void Aim(StudioCameraTrack camera, float time, string target, Vector3 offset, float blend)
        {
            camera.aims.RemoveAll(a => Mathf.Abs(a.time - time) < .001f);
            camera.aims.Add(new StudioAimKey { time = time, target = target, offset = offset, blend = blend });
            camera.aims.Sort((a, b) => a.time.CompareTo(b.time));
        }

        // A focal length for a vertical field of view on this camera's sensor.
        public static float FocalForVertical(StudioCameraTrack camera, float degrees) => StudioCameras.FocalFor(degrees, camera.sensorHeight);
    }
}
