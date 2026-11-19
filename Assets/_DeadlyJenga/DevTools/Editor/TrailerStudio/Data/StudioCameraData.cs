using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // One point of a camera move. Times are tape seconds; positions are metres from the stage spot.
    [Serializable] public sealed class StudioCameraKey
    {
        public float time;
        public Vector3 position;
        public Vector3 euler;             // pitch, yaw, roll in degrees
        public float focal = 35;          // focal length in millimetres
        public float focus = 6;           // focus distance in metres
        public float aperture = 4;        // f-number: lower = blurrier background
        public float shake = 1;           // how strong the handheld noise is around this key (a strength curve)
        public int ease = 1;              // into this key: 0 straight, 1 smooth, 2 slow start, 3 slow end, 4 hold then cut
    }

    // A change of playback speed on a shot (speed ramps).
    [Serializable] public sealed class StudioSpeedMark
    {
        public float time;                // tape seconds
        public float speed = 1;           // 1 = real speed, 0.25 = quarter speed
    }

    // A camera with its own lens and moves: one shot over the finished tape.
    [Serializable] public sealed class StudioCameraTrack
    {
        public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string name = "Camera";
        public int color;
        public float start, end = -1;     // the part of the tape this shot covers (end -1 = to the end)
        public List<StudioCameraKey> keys = new List<StudioCameraKey>();
        public bool spline = true;        // curved path through the keys instead of straight lines
        public bool constantSpeed;        // travel the path at an even pace (dolly feel), ignoring key spacing

        // What the camera does by itself, on top of its keys.
        public string behavior = "";      // "", follow, ride, twoShot, orbit, dollyZoom, firstPerson
        public string subject = "", second = "";   // actor mark ids
        public float lag = .25f;          // seconds the aim trails behind the subject
        public float orbitSpeed = 20;     // degrees per second
        public Vector3 aimOffset = new Vector3(0, 1.1f, 0);

        public string noise = "";         // "", subtle, walking, running, impact
        public float noiseStrength = 1;
        public bool avoidBlocks;
        public bool focusOnSubject;
        public float sensorHeight = 18.66f;   // millimetres (Super 35). Field of view comes from focal length and this.
        public float squeeze = 1;         // anamorphic squeeze (1 = spherical lens); see DECISIONS for what it does

        // Time remap
        public List<StudioSpeedMark> speeds = new List<StudioSpeedMark>();
        public float freezeAt = -1, freezeFor;    // hold the tape at this time for this many seconds (the camera keeps moving)

        // Look, applied only to this shot's renders.
        public float bloom, vignette, grain, chroma;
        public string lut = "";           // name of a LUT texture in the project, or empty

        public float End(StudioSet set) => end < 0 ? set.length : Mathf.Min(end, set.length);
    }

    // One piece of the final cut: part of a shot.
    [Serializable] public sealed class StudioCut
    {
        public string camera = "";
        public float from, to = 1;        // seconds inside the rendered shot
        public float dissolve;            // seconds of cross-dissolve into this piece (0 = straight cut)
    }

    [Serializable] public sealed class StudioRenderSettings
    {
        public int width = 1920, height = 1080, fps = 30;
        public int subFrames = 1;         // more than 1 = motion blur and smooth edges by averaging in-between frames
        public float shutter = .5f;       // part of the frame the shutter is open (0.5 = 180 degrees)
        public string format = "png";     // png, exr, mp4, prores
        public bool cinema = true;        // cinema quality preset during the render
        public bool freezeAmbient;        // true = ocean, sky and birds hold still, so two renders are bit-for-bit the same
    }

    public static class StudioAspects
    {
        public static readonly List<string> Names = new List<string> { "16:9", "9:16 (vertical)", "1:1", "2.39:1" };
        public static readonly float[] Ratios = { 16f / 9, 9f / 16, 1, 2.39f };

        // Width and height for an aspect at a quality step ("1080", "1440", "2160"): the short side or 16:9 height.
        public static Vector2Int Size(int aspect, int lines)
        {
            float ratio = Ratios[Mathf.Clamp(aspect, 0, Ratios.Length - 1)];
            int height = ratio >= 1 ? (aspect == 3 ? Mathf.RoundToInt(lines * 16f / 9 / ratio) : lines) : Mathf.RoundToInt(lines * 16f / 9);
            int width = Mathf.RoundToInt(height * ratio);
            if (ratio < 1) { width = lines; height = Mathf.RoundToInt(lines / ratio); }
            return new Vector2Int(width + width % 2, height + height % 2);
        }
    }
}
