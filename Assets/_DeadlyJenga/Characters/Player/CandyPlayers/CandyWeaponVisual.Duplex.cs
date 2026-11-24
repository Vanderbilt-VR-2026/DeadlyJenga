using System;
using System.Collections.Generic;
using System.IO;
using DeadlyJenga.Core;
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
        static readonly Dictionary<EquipmentKind, (Mesh mesh, byte part, bool glass)[]> reviewedWeaponMeshes = new();
        static readonly string[] SugarCorpsLauncherParts =
        {
            "Sugar Corps launcher", "Sugar Corps drum", "Charge cell 1", "Charge cell 2", "Charge cell 3",
            "Charge cell 4", "Charge cell 5", "Charge cell 6"
        };
        static readonly string[] RoundedSpikerParts =
        {
            "Rounded Spiker receiver", "Prismatic power cell", "Crystal 1", "Crystal 2", "Crystal 3", "Crystal 4", "Crystal 5"
        };
        static readonly string[] StaffArtParts = { "Weathered branch and mount", "Serrated saw wheel - worn shed steel" };
        static readonly string[] AlienArtParts = { "Crimson Alien Blaster body", "Crimson Alien cooling pose" };
        static readonly string[] SheriffArtParts = { "Cobalt Sheriff and ivory grip", "Sheriff cylinder" };
        static readonly string[] SwordArtParts = { "Garnet Murderer Sword" };
        static readonly string[] RocketArtParts = { "Charcoal lollipop launcher" };
        static readonly string[] AxeArtParts = { "Battle-worn Storm Axe" };
        static readonly string[] MarshmallowArtParts = { "Roasted marshmallow and fork" };
        static readonly string[] SawShooterArtParts =
        {
            "Garage Saw Wheel Shooter", "Garage wheel magazine", "Serrated saw wheel - garage steel"
        };
        static readonly string[] ChaingunArtParts =
        {
            "Black and brass Chaingun", "Looped brass feed belt", "Foundry six-barrel rotor",
            "Heat bar 1", "Heat bar 2", "Heat bar 3", "Heat bar 4", "Heat bar 5",
            "Heat bar 6", "Heat bar 7", "Heat bar 8", "Heat bar 9", "Heat bar 10",
            "Belt phase 2", "Belt phase 3", "Belt phase 4", "Belt phase 5", "Belt phase 6"
        };

        const int FirstAlternateBeltPart = 13;
        static readonly Mesh[] chainBeltFrames = new Mesh[6];
        MeshFilter chainBelt;
        float chainBeltPhase;

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
                        duplexVisual = ReadWeaponMeshes(data.bytes, 0x584C5044, DuplexParts, 6);
                        EnsureReviewedWeaponMaterials();
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

        bool TryBuildReviewedWeapon(Transform parent, EquipmentKind weapon, string resource, string[] names)
        {
            if (!reviewedWeaponMeshes.TryGetValue(weapon, out var meshes))
            {
                meshes = null;
                var asset = Resources.Load<TextAsset>(resource);
                var shader = Resources.Load<Shader>("DuplexSurface");
                if (asset && shader && shader.isSupported)
                {
                    try { meshes = ReadWeaponMeshes(asset.bytes, 0x54524157, names); }
                    catch (Exception error) when (error is IOException || error is InvalidDataException)
                    { Debug.LogWarning($"Could not read the {resource} model. {error.Message}"); }
                }
                reviewedWeaponMeshes[weapon] = meshes;
                if (meshes == null)
                    Debug.LogError($"The reviewed {resource} weapon model is unavailable.");
            }

            if (meshes == null) return false;
            var parts = new Transform[names.Length];
            parts[0] = parent;
            if (weapon == EquipmentKind.RocketLauncher)
                MakeFeedAssembly(parent);
            else if (weapon == EquipmentKind.Blaster)
            {
                parts[1] = new GameObject("Open cooling hatch pose").transform;
                parts[1].SetParent(parent, false);
                parts[1].gameObject.SetActive(false);
            }
            else if (parts.Length > 1 && weapon != EquipmentKind.SawStaff)
                parts[1] = MakeFeedAssembly(parent);
            int mountedParts = weapon == EquipmentKind.ChainGun ? FirstAlternateBeltPart : parts.Length;
            for (int i = weapon == EquipmentKind.SawStaff ? 1 : 2; i < mountedParts; i++)
            {
                parts[i] = new GameObject(names[i]).transform;
                parts[i].SetParent(weapon == EquipmentKind.GrenadeLauncher ? parts[1] : parent, false);
            }
            if (weapon is EquipmentKind.SawStaff or EquipmentKind.ChainGun or EquipmentKind.Chainsaw)
            {
                rotor = parts[weapon == EquipmentKind.SawStaff ? 1 : 2];
                rotor.localPosition = weapon == EquipmentKind.ChainGun ? new Vector3(0, ChainGunBarrelHeight, ChainGunRotorStart) :
                    Vector3.forward * (weapon == EquipmentKind.SawStaff ? .88f : .79f);
            }

            EnsureReviewedWeaponMaterials();
            foreach (var mesh in meshes)
            {
                if (weapon == EquipmentKind.ChainGun && mesh.part >= FirstAlternateBeltPart)
                {
                    chainBeltFrames[mesh.part - FirstAlternateBeltPart + 1] = mesh.mesh;
                    continue;
                }
                MountFeedMesh(mesh.mesh, mesh.glass ? duplexGlass : duplexCoating, parts[mesh.part]);
                if (weapon == EquipmentKind.ChainGun && mesh.part == 1 && !mesh.glass)
                {
                    chainBeltFrames[0] = mesh.mesh;
                    chainBelt = parts[1].Find(mesh.mesh.name).GetComponent<MeshFilter>();
                    chainBeltPhase = 0;
                }
            }
            return true;
        }

        void PresentChainBelt(EquipmentState state)
        {
            if (!chainBelt || !state.drawn || !state.triggerHeld || state.reloadRemaining > 0 ||
                state.magazineInitialized && state.rounds == 0)
                return;

            float interval = EquipmentSpecifications.Get(EquipmentKind.ChainGun).cooldown;
            chainBeltPhase = Mathf.Repeat(chainBeltPhase + Time.deltaTime / interval, 1);
            var frame = chainBeltFrames[(int)(chainBeltPhase * chainBeltFrames.Length)];
            if (chainBelt.sharedMesh != frame) chainBelt.sharedMesh = frame;
        }

        static (Mesh mesh, byte part, bool glass)[] alternateSpiker;
        static bool alternateSpikerLoaded;
        bool spikerPaletteApplied, spikerWasLeft;

        void PresentSpikerPalettes(bool primaryLeft)
        {
            if (spikerPaletteApplied && spikerWasLeft == primaryLeft) return;
            if (!alternateSpikerLoaded)
            {
                alternateSpikerLoaded = true;
                var asset = Resources.Load<TextAsset>("RoundedSpikerRight");
                if (asset)
                {
                    try { alternateSpiker = ReadWeaponMeshes(asset.bytes, 0x54524157, RoundedSpikerParts); }
                    catch (Exception error) when (error is IOException || error is InvalidDataException)
                    { Debug.LogError($"Could not read the reviewed right Spiker. {error.Message}"); }
                }
            }
            if (alternateSpiker == null ||
                !reviewedWeaponMeshes.TryGetValue(EquipmentKind.DualUzi, out var original) || original == null)
                return;

            ApplySpikerPalette(root, primaryLeft ? original : alternateSpiker);
            ApplySpikerPalette(paired, primaryLeft ? alternateSpiker : original);
            spikerWasLeft = primaryLeft;
            spikerPaletteApplied = true;
        }

        static void ApplySpikerPalette(Transform weapon, (Mesh mesh, byte part, bool glass)[] art)
        {
            foreach (var part in art)
            {
                if (part.part < 2) continue;
                var socket = weapon.Find(RoundedSpikerParts[part.part]);
                var model = socket ? socket.Find(part.mesh.name) : null;
                if (model) model.GetComponent<MeshFilter>().sharedMesh = part.mesh;
            }
        }

        static void EnsureReviewedWeaponMaterials()
        {
            if (duplexCoating && duplexGlass) return;
            var shader = Resources.Load<Shader>("DuplexSurface");
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

        static (Mesh, byte, bool)[] ReadWeaponMeshes(byte[] data, uint magic, string[] names, int requiredSections = 0)
        {
            using var reader = new BinaryReader(new MemoryStream(data, false));
            if (reader.ReadUInt32() != magic)
                throw new InvalidDataException("Unexpected weapon mesh format.");
            int sections = reader.ReadInt32();
            if (sections < names.Length || sections > names.Length * 2 || requiredSections != 0 && sections != requiredSections)
                throw new InvalidDataException("Unexpected weapon mesh section count.");

            var result = new (Mesh, byte, bool)[sections];
            var used = new HashSet<int>();
            try
            {
                for (int piece = 0; piece < result.Length; piece++)
                {
                    byte part = reader.ReadByte();
                    byte layer = reader.ReadByte();
                    int vertexCount = reader.ReadInt32();
                    int indexCount = reader.ReadInt32();
                    if (part >= names.Length || layer > 1 || !used.Add(part * 2 + layer) ||
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
                        name = names[part] + (layer == 1 ? " glass" : " opaque"),
                        indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                        vertices = positions, normals = normals, colors = colors
                    };
                    result[piece] = (mesh, part, layer == 1);
                    mesh.SetUVs(0, finish);
                    mesh.SetTriangles(indices, 0);
                    mesh.UploadMeshData(true);
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                    throw new InvalidDataException("Unexpected data after weapon meshes.");
                for (int part = 0; part < names.Length; part++)
                    if (!used.Contains(part * 2) && !used.Contains(part * 2 + 1))
                        throw new InvalidDataException("A weapon mesh part is missing.");
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
