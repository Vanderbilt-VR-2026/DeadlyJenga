using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        static readonly string[] DuplexParts =
        {
            "Duplex receiver", "Rotating 180 degree barrel carriage", "RIFLE display", "SHOTGUN display"
        };

        static (Mesh mesh, byte part, bool glass)[] duplexVisual;
        static Material duplexCoating, duplexGlass;
        static bool duplexVisualChecked;

        // The authored asset is shared by held, remote and pickup instances.
        // Positions are already in the existing receiver and carriage coordinate spaces.
        bool TryBuildApprovedDuplex(Transform parent)
        {
            if (!duplexVisualChecked)
            {
                duplexVisualChecked = true;
                var data = Resources.Load<TextAsset>("DuplexVisual");
                var shader = Resources.Load<Shader>("DuplexSurface");
                if (data && shader && shader.isSupported)
                {
                    try
                    {
                        duplexVisual = ReadDuplexVisual(data.bytes);
                        duplexCoating = new Material(shader) { name = "Duplex opaque finishes", enableInstancing = true };
                        duplexGlass = new Material(duplexCoating)
                        {
                            name = "Duplex glass and candy residue",
                            renderQueue = (int)RenderQueue.Transparent
                        };
                        duplexGlass.SetOverrideTag("RenderType", "Transparent");
                        duplexGlass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        duplexGlass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        duplexGlass.SetInt("_ZWrite", 0);
                        duplexGlass.EnableKeyword("_DUPLEX_GLASS");
                    }
                    catch (Exception error) when (error is IOException || error is InvalidDataException)
                    {
                        Debug.LogWarning("Could not read the approved Duplex visual. " + error.Message);
                    }
                }
            }

            if (duplexVisual == null) return false;
            var parts = new Transform[DuplexParts.Length];
            for (int part = 0; part < parts.Length; part++)
            {
                parts[part] = new GameObject(DuplexParts[part]).transform;
                parts[part].SetParent(parent, false);
            }

            barrel = parts[1];
            barrel.localPosition = new Vector3(0, .09f, .23f);
            barrel.localRotation = Quaternion.Euler(0, 0, 180);
            foreach (var piece in duplexVisual)
            {
                var go = new GameObject(piece.glass ? "Glass" : "Surface", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parts[piece.part], false);
                go.GetComponent<MeshFilter>().sharedMesh = piece.mesh;
                var render = go.GetComponent<MeshRenderer>();
                render.sharedMaterial = piece.glass ? duplexGlass : duplexCoating;
                render.shadowCastingMode = ShadowCastingMode.Off;
            }

            AddDuplexOptic("Rifle optic", new Vector3(0, -.167f, .203f), 180);
            AddDuplexOptic("Shotgun optic", new Vector3(0, .19f, .231f), 0);
            parent.gameObject.AddComponent<CandyDuplexModeIndicator>().Initialize(barrel, parts[2], parts[3]);
            return true;
        }

        void AddDuplexOptic(string opticName, Vector3 position, float roll)
        {
            var optic = new GameObject(opticName).transform;
            optic.SetParent(barrel, false);
            optic.localPosition = position;
            optic.localRotation = Quaternion.Euler(0, 0, roll);
            var lens = new GameObject("Optic lens").transform;
            lens.SetParent(optic, false);
            lens.localPosition = Vector3.forward * .008f;
        }

        static (Mesh, byte, bool)[] ReadDuplexVisual(byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data, false));
            if (reader.ReadUInt32() != 0x584C5044 || reader.ReadInt32() != 6)
                throw new InvalidDataException("Unexpected Duplex mesh format.");

            var result = new (Mesh, byte, bool)[6];
            var used = new HashSet<int>();
            try
            {
                for (int piece = 0; piece < result.Length; piece++)
                {
                    byte part = reader.ReadByte();
                    byte layer = reader.ReadByte();
                    int vertexCount = reader.ReadInt32();
                    int indexCount = reader.ReadInt32();
                    if (part >= DuplexParts.Length || layer > 1 || !used.Add(part * 2 + layer) ||
                        vertexCount <= 0 || vertexCount > 100_000 ||
                        indexCount <= 0 || indexCount > 600_000 || indexCount % 3 != 0 ||
                        reader.BaseStream.Length - reader.BaseStream.Position < (long)vertexCount * 56 + (long)indexCount * 4)
                        throw new InvalidDataException("Invalid Duplex mesh section.");

                    var positions = new Vector3[vertexCount];
                    var normals = new Vector3[vertexCount];
                    var colors = new Color[vertexCount];
                    var finish = new List<Vector4>(vertexCount);
                    for (int vertex = 0; vertex < vertexCount; vertex++)
                    {
                        positions[vertex] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        normals[vertex] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        colors[vertex] = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        finish.Add(new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    }

                    var indices = new int[indexCount];
                    for (int index = 0; index < indexCount; index++)
                    {
                        indices[index] = reader.ReadInt32();
                        if ((uint)indices[index] >= vertexCount)
                            throw new InvalidDataException("Invalid Duplex triangle index.");
                    }

                    var mesh = new Mesh
                    {
                        name = DuplexParts[part] + (layer == 1 ? " glass" : " opaque"),
                        indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                        vertices = positions, normals = normals, colors = colors
                    };
                    result[piece] = (mesh, part, layer == 1);
                    mesh.SetUVs(0, finish);
                    mesh.SetTriangles(indices, 0);
                    mesh.UploadMeshData(true);
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                    throw new InvalidDataException("Unexpected data after Duplex meshes.");
                return result;
            }
            catch
            {
                foreach (var item in result)
                    if (item.Item1) UnityEngine.Object.Destroy(item.Item1);
                throw;
            }
        }
    }
}
