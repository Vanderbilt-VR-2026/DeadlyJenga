using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum CastleSection { Corner, Wall, Keep }
    public enum CastleStructure { Original, Catwalks, FourKeeps }

    [Serializable]
    public class CastleSettings
    {
        public Vector3 blockSize = new Vector3(6f, 2f, 2f);
        [Range(3, 7)] public int modulesPerSide = 5;
        [Range(2, 40)] public int wallCourses = 12;
        [Range(2, 48)] public int cornerCourses = 18;
        [Range(2, 64)] public int keepCourses = 30;
        [Range(0.02f, 10f)] public float moduleGap = 0.08f;
        public bool singleTower;
        public CastleStructure structureVariant;
        [Tooltip("Evan's cathedral draft. Uses the same blocks with a separate large, open layout.")]
        public bool cathedral;
        [Tooltip("Optional deterministic mode layout. Empty preserves the original castle.")]
        public string layoutPreset = "";
    }

    public readonly struct CastlePlacement
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly CastleSection Section;
        public readonly int Course;
        public readonly Vector3 Size;
        public Vector3 BlockSize(CastleSettings settings) => Size == Vector3.zero ? settings.blockSize : Size;
        public CastlePlacement(Vector3 p, Quaternion r, CastleSection s, int c)
         : this(p,r,s,c,Vector3.zero) { }
        public CastlePlacement(Vector3 p, Quaternion r, CastleSection s, int c, Vector3 size)
        { Position = p; Rotation = r; Section = s; Course = c; Size = size; }
    }

    // Pure layout: no scene objects, physics, networking, or randomness.
    // Coordinates are relative to the foundation's TOP surface.
    public static class CastleLayout
    {
        public static void Validate(CastleSettings s)
        {
            if (s == null || !Finite(s.blockSize.x) || !Finite(s.blockSize.y) || !Finite(s.blockSize.z)
                || s.blockSize.y <= 0 || s.blockSize.z <= 0 || s.blockSize.x <= 2 * s.blockSize.z
                || s.modulesPerSide < 3 || s.modulesPerSide > 7 || s.modulesPerSide % 2 == 0
                || s.wallCourses < 2 || s.wallCourses > 40 || s.cornerCourses < 2 || s.cornerCourses > 48
                || s.keepCourses < 2 || s.keepCourses > 64 || !Finite(s.moduleGap) || s.moduleGap < .02f)
                throw new ArgumentException("Use positive block dimensions (length > 2 × width), an odd module count from 3–7, and valid course counts/gaps.");
            if (!Enum.IsDefined(typeof(CastleStructure),s.structureVariant)) throw new ArgumentException("Unknown castle structure.");
            if(s.structureVariant!=CastleStructure.Original&&(s.singleTower||s.cathedral||!string.IsNullOrEmpty(s.layoutPreset)||s.modulesPerSide!=5||s.keepCourses<=s.wallCourses+4))
                throw new ArgumentException("Experimental structures require the five-module castle and a keep above the wall towers.");
            if (!s.singleTower && s.modulesPerSide < 5)
                throw new ArgumentException("The octagonal keep needs at least five modules per side.");
            if(s.cathedral&&Mathf.Abs(s.blockSize.x-3*s.blockSize.z)>.001f)
                throw new ArgumentException("The cathedral bearing pattern requires blocks three times as long as they are wide.");
            if(!string.IsNullOrEmpty(s.layoutPreset)&&!ModeMapLayout.Known(s.layoutPreset))throw new ArgumentException("Unknown mode map layout: "+s.layoutPreset);
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        public static int HeightCourses(CastleSettings s)=>!string.IsNullOrEmpty(s.layoutPreset)?ModeMapLayout.Height(s.layoutPreset):s.cathedral?90:s.structureVariant==CastleStructure.FourKeeps?Mathf.Max(s.cornerCourses,s.wallCourses+3+s.keepCourses):s.singleTower?s.cornerCourses:Mathf.Max(s.keepCourses,s.cornerCourses,s.wallCourses+2);
        public static float Footprint(CastleSettings s) => !string.IsNullOrEmpty(s.layoutPreset)?Mathf.Max(ModeMapLayout.Size(s).x,ModeMapLayout.Size(s).y):s.cathedral?27*s.blockSize.x:s.singleTower ? s.blockSize.x :
            (s.modulesPerSide - 1) * (s.blockSize.x + s.moduleGap) + s.blockSize.x;
        public static Vector2 FootprintSize(CastleSettings s)=>!string.IsNullOrEmpty(s.layoutPreset)?ModeMapLayout.Size(s):Vector2.one*Footprint(s);

        public static List<CastlePlacement> Build(CastleSettings s)
        {
            Validate(s);
            if(!string.IsNullOrEmpty(s.layoutPreset))return ModeMapLayout.Build(s);
            if(s.cathedral)return CathedralLayout.Build(s);
            var result = new List<CastlePlacement>();
            if (s.singleTower) { AddTower(result, s, Vector3.zero, s.cornerCourses, CastleSection.Corner); return result; }
            int last = s.modulesPerSide - 1;
            float pitch = s.blockSize.x + s.moduleGap;
            for (int x = 0; x <= last; x++)
            for (int z = 0; z <= last; z++)
            {
                if (x != 0 && x != last && z != 0 && z != last) continue;
                bool corner = (x == 0 || x == last) && (z == 0 || z == last);
                // Alternate wall heights to create the battlement silhouette.
                int height = corner ? s.cornerCourses : s.wallCourses + ((x + z) % 2 == 0 ? 2 : 0);
                AddTower(result, s, new Vector3((x-last*.5f)*pitch, 0, (z-last*.5f)*pitch),
                    height, corner ? CastleSection.Corner : CastleSection.Wall);
            }
            // Four cardinal crates, then four diagonal crates. Repeat, never accumulate twist.
            // Overlap the ends by part of a crate width. Meeting only at the end centres
            // gives marginal bearing and lets the upper courses tip under gravity.
            float radius = (s.blockSize.x - s.blockSize.z * .4f) * .5f / Mathf.Tan(Mathf.PI / 8);
            for (int c = 0; c < s.keepCourses; c++)
            for (int side = 0; side < 4; side++)
            {
                var r = Quaternion.Euler(0, side * 90 + (c % 2) * 45, 0);
                result.Add(new CastlePlacement(Vector3.up * ((c + .5f) * s.blockSize.y)
                    + r * new Vector3(0, 0, radius), r, CastleSection.Keep, c));
            }
            CastleVariants.Apply(result,s);
            return result;
        }

        static void AddTower(List<CastlePlacement> output, CastleSettings s, Vector3 center, int courses, CastleSection section)
        {
            float offset = (s.blockSize.x - s.blockSize.z) * .5f;
            for (int c = 0; c < courses; c++)
            {
                Quaternion r = Quaternion.Euler(0, (c % 2) * 90, 0);
                for (int side = -1; side <= 1; side += 2)
                    output.Add(new CastlePlacement(center + Vector3.up * ((c + .5f) * s.blockSize.y)
                        + r * new Vector3(0, 0, side * offset), r, section, c));
            }
        }
    }
}
