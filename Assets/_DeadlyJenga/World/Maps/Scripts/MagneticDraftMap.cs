using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class MagneticDraftMap : MonoBehaviour
    {
        public GameObject cubePrefab, catwalkPrefab;
        public Material weakMaterial, mediumMaterial, strongMaterial;
        public MagneticStructure Structure { get; private set; }
        public const string Prefix = "magnetic-evan2-v1:";
        [Serializable] public class State { public int version = 1; public int[] broken = Array.Empty<int>(); }
        public readonly struct Piece
        {
            public readonly Vector3 Position, Size;
            public readonly int Strength;
            public readonly bool Anchor, Catwalk;
            public Piece(Vector3 p, Vector3 size, int strength, bool anchor = false, bool catwalk = false)
            { Position = p; Size = size; Strength = strength; Anchor = anchor; Catwalk = catwalk; }
        }
        public static List<Piece> Layout()
        {
            var pieces = new List<Piece>();
            // Four open towers. Full ring courses tie the four corner columns together.
            for (int tx = -1; tx <= 1; tx += 2)
            for (int tz = -1; tz <= 1; tz += 2)
            {
                int height = tx == tz ? 14 : 11;
                for (int y = 0; y < height; y++)
                for (int x = -3; x <= 3; x += 2)
                for (int z = -3; z <= 3; z += 2)
                {
                    bool corner = Mathf.Abs(x) == 3 && Mathf.Abs(z) == 3;
                    bool rim = Mathf.Abs(x) == 3 || Mathf.Abs(z) == 3;
                    if (!rim || (!corner && y % 3 != 2 && y != 0 && y != height - 1)) continue;
                    pieces.Add(new Piece(new Vector3(tx * 10 + x, y * 2 + 1, tz * 10 + z), Vector3.one * 2, 3, y == 0));
                }
            }
            // Thin bridges span genuine gaps. Each half has its own breakable attachment.
            for (int level = 0; level < 3; level++)
            for (int side = -1; side <= 1; side += 2)
            for (int half = -1; half <= 1; half += 2)
            {
                float y = 5.85f + level * 6;
                pieces.Add(new Piece(new Vector3(half * 3, y, side * 10), new Vector3(6, .3f, 2), 1, false, true));
                pieces.Add(new Piece(new Vector3(side * 10, y, half * 3), new Vector3(2, .3f, 6), 1, false, true));
            }
            // Green cantilevers reach into the courtyard, with smaller red tips.
            for (int side = -1; side <= 1; side += 2)
            {
                pieces.Add(new Piece(new Vector3(side * 4, 23, side * 7), new Vector3(4, 2, 2), 2));
                pieces.Add(new Piece(new Vector3(side, 23, side * 7), new Vector3(2, 2, 2), 1));
            }
            return pieces;
        }
        public string Save() => Prefix + JsonUtility.ToJson(new State { broken = Structure ? Structure.SaveBroken() : Array.Empty<int>() });
        public static bool TryRead(string text, int count, out State state)
        {
            state = null;
            if (text == null || !text.StartsWith(Prefix, StringComparison.Ordinal) || count != Layout().Count) return false;
            try { state = JsonUtility.FromJson<State>(text.Substring(Prefix.Length)); }
            catch (ArgumentException) { return false; }
            if (state == null || state.version != 1 || state.broken == null) return false;
            foreach (int index in state.broken) if (index < 0 || index >= count) return false;
            return true;
        }
        public void Build(Transform root, List<Rigidbody> bodies, Action changed)
        {
            Structure = root.gameObject.AddComponent<MagneticStructure>();
            var layout = Layout();
            var generator = GetComponent<CastleGenerator>();
            var standard = generator && generator.blockPrefab ? generator.blockPrefab.GetComponent<CastleBlock>() : null;
            foreach (var piece in layout)
            {
                var go = Instantiate(piece.Catwalk ? catwalkPrefab : cubePrefab, root);
                go.name = (piece.Strength == 3 ? "Strong tower " : piece.Strength == 2 ? "Medium cantilever " : "Weak bridge ") + bodies.Count;
                go.transform.localPosition = piece.Position;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var block = go.GetComponent<CastleBlock>();
                go.GetComponent<BoxCollider>().size = piece.Size;
                block.appearance = standard ? standard.appearance : block.appearance;
                var material = piece.Strength == 3 ? strongMaterial : piece.Strength == 2 ? mediumMaterial : weakMaterial;
                if (generator && block.appearance && block.appearance.visualPrefab)
                    material = (piece.Strength == 3 ? generator.keepMaterial : piece.Strength == 2 ? generator.cornerMaterial : generator.wallMaterial) ?? material;
                block.sectionMaterial = material;
                if (block.visualRoot)
                {
                    block.visualRoot.localScale = piece.Size;
                    var renderer = block.visualRoot.GetComponent<Renderer>();
                    if (renderer)
                    {
                        renderer.enabled = !block.appearance || !block.appearance.visualPrefab;
                        if (material) renderer.sharedMaterial = material;
                    }
                }
                block.RefreshAppearance();
                block.Body.position = go.transform.position;
                block.Body.rotation = go.transform.rotation;
                block.Body.mass = piece.Catwalk ? 25 : 40;
                var magnet = go.AddComponent<MagneticBlock>();
                magnet.strength = piece.Strength; magnet.foundationAnchor = piece.Anchor;
                Structure.Add(magnet); bodies.Add(block.Body);
                block.SyncSurface();
            }
            // Once at build time. No broadphase queries or pair scans in Update/FixedUpdate.
            for (int a = 0; a < layout.Count; a++)
            for (int b = a + 1; b < layout.Count; b++)
                if (Touches(layout[a], layout[b])) Structure.Connect(a, b);
            Structure.Recompute();
            Structure.Changed += changed;
        }
        public static bool Touches(Piece a, Piece b)
        {
            Vector3 gap = (a.Size + b.Size) * .5f;
            Vector3 delta = a.Position - b.Position;
            gap -= new Vector3(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
            if (gap.x < -.025f || gap.y < -.025f || gap.z < -.025f) return false;
            return (gap.x > .05f ? 1 : 0) + (gap.y > .05f ? 1 : 0) + (gap.z > .05f ? 1 : 0) >= 2;
        }
    }
}
