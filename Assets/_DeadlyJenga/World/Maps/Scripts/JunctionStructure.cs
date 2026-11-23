using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Blender export in map-local metres, with decorations assigned to their bearing members.
    internal sealed class JunctionStructure
    {
        internal readonly struct Triangle
        {
            internal readonly ushort A, B, C;
            internal readonly Color32 Color;

            internal Triangle(BinaryReader reader)
            {
                A = reader.ReadUInt16();
                B = reader.ReadUInt16();
                C = reader.ReadUInt16();
                Color = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 255);
            }
        }

        internal sealed class Part
        {
            internal readonly bool Solid;
            internal readonly Vector3[] Points;
            internal readonly Triangle[] Triangles;

            internal Part(BinaryReader reader)
            {
                Solid = reader.ReadBoolean();
                int points = reader.ReadUInt16();
                int triangles = reader.ReadUInt16();
                if (points < 3 || triangles < 1)
                    throw new InvalidDataException("Empty Sphinx Junction mesh part.");
                Points = new Vector3[points];
                for (int i = 0; i < points; i++)
                {
                    Points[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    if (!float.IsFinite(Points[i].sqrMagnitude))
                        throw new InvalidDataException("Non-finite Sphinx Junction mesh point.");
                }
                Triangles = new Triangle[triangles];
                for (int i = 0; i < triangles; i++)
                {
                    var triangle = new Triangle(reader);
                    if (triangle.A >= points || triangle.B >= points || triangle.C >= points)
                        throw new InvalidDataException("Sphinx Junction triangle references a missing point.");
                    Triangles[i] = triangle;
                }
            }
        }

        readonly SortedDictionary<string, Part[]> members = new(StringComparer.Ordinal);
        static JunctionStructure cached;
        internal IEnumerable<string> Names => members.Keys;

        internal static JunctionStructure Load()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>("ModeMaps/SphinxJunctionStructure");
            if (!asset) return null;
            try
            {
                using var stream = new MemoryStream(asset.bytes, false);
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt32() != 0x4a53424c)
                    throw new InvalidDataException("Unknown Sphinx Junction structure version.");
                int count = reader.ReadUInt16();
                var structure = new JunctionStructure();
                for (int i = 0; i < count; i++)
                {
                    string name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
                    var pieces = new Part[reader.ReadUInt16()];
                    for (int piece = 0; piece < pieces.Length; piece++) pieces[piece] = new Part(reader);
                    structure.members.Add(name, pieces);
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Trailing Sphinx Junction structure data.");
                return cached = structure;
            }
            catch (IOException error)
            {
                Debug.LogError(error.Message);
                return null;
            }
        }

        internal void AddTo(string name, JunctionModel model)
        {
            if (!members.TryGetValue(name, out var parts)) return;
            foreach (var part in parts) model.AddBakedPart(part);
        }

        internal Bounds BoundsOf(string name)
        {
            var parts = members[name];
            var bounds = new Bounds(parts[0].Points[0], Vector3.zero);
            foreach (var part in parts)
                foreach (var point in part.Points)
                    bounds.Encapsulate(point);
            return bounds;
        }
    }
}
