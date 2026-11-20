using System;
using System.Collections.Generic;
using System.IO;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    sealed class TheatreParticleTape : IDisposable
    {
        const int MaxSystems = 64, MaxParticles = 8192;
        sealed class ReplaySystem
        {
            public ParticleSystem system;
            public Material ownedMaterial;
        }

        readonly Dictionary<int, ReplaySystem> replay = new();
        readonly HashSet<int> seen = new();
        readonly Dictionary<ParticleSystemRenderer, bool> suppressed = new();
        readonly HashSet<ParticleSystem> owned = new();
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[MaxParticles];
        readonly List<ParticleSystem> visible = new(MaxSystems);
        public int PeakSamples { get; private set; }
        public bool Truncated { get; private set; }

        public void Write(BinaryWriter writer)
        {
            visible.Clear();
            foreach (var source in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.InstanceID))
            {
                if (source.particleCount == 0 || !source.TryGetComponent<ParticleSystemRenderer>(out var renderer) ||
                    !renderer.enabled || renderer.forceRenderingOff || renderer.renderMode == ParticleSystemRenderMode.None) continue;
                if (visible.Count == MaxSystems) { Truncated = true; break; }
                visible.Add(source);
            }
            writer.Write(visible.Count);
            int remaining = MaxParticles;
            foreach (var source in visible)
            {
                var renderer = source.GetComponent<ParticleSystemRenderer>();
                var material = renderer.sharedMaterial;
                writer.Write(source.GetInstanceID());
                writer.Write((int)renderer.renderMode);
                writer.Write(material ? AssetDatabase.GetAssetPath(material) : "");
                writer.Write(material && material.shader ? material.shader.name : "");
                writer.Write(renderer.mesh ? AssetDatabase.GetAssetPath(renderer.mesh) : "");
                int count = remaining == 0 ? 0 : source.GetParticles(particles, remaining);
                if (source.particleCount > remaining) Truncated = true;
                writer.Write(count);
                remaining -= count;
                var main = source.main;
                Transform basis = main.simulationSpace == ParticleSystemSimulationSpace.Custom
                    ? main.customSimulationSpace : main.simulationSpace == ParticleSystemSimulationSpace.Local ? source.transform : null;
                for (int i = 0; i < count; i++)
                {
                    var particle = particles[i];
                    NetWire.Write(writer, basis ? basis.TransformPoint(particle.position) : particle.position);
                    NetWire.Write(writer, basis ? basis.TransformVector(particle.velocity) : particle.velocity);
                    NetWire.Write(writer, particle.rotation3D);
                    NetWire.Write(writer, particle.GetCurrentSize3D(source));
                    Color32 color = particle.GetCurrentColor(source);
                    writer.Write(color.r); writer.Write(color.g); writer.Write(color.b); writer.Write(color.a);
                }
            }
            PeakSamples = Math.Max(PeakSamples, MaxParticles - remaining);
        }

        public void Read(BinaryReader reader, bool apply)
        {
            int systems = reader.ReadInt32(), remaining = MaxParticles;
            if (systems < 0 || systems > MaxSystems) throw new InvalidDataException("Invalid particle system count.");
            seen.Clear();
            for (int s = 0; s < systems; s++)
            {
                int id = reader.ReadInt32();
                var mode = (ParticleSystemRenderMode)reader.ReadInt32();
                string materialPath = reader.ReadString(), shader = reader.ReadString(), meshPath = reader.ReadString();
                int count = reader.ReadInt32();
                if (count < 0 || count > remaining) throw new InvalidDataException("Invalid particle sample count.");
                remaining -= count;
                if (!apply)
                {
                    reader.BaseStream.Seek(count * 52L, SeekOrigin.Current);
                    continue;
                }
                for (int i = 0; i < count; i++)
                {
                    particles[i] = new ParticleSystem.Particle
                    {
                        position = NetWire.ReadVector(reader), velocity = NetWire.ReadVector(reader),
                        rotation3D = NetWire.ReadVector(reader), startSize3D = NetWire.ReadVector(reader),
                        startColor = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()),
                        startLifetime = 1, remainingLifetime = 1
                    };
                }
                if (count == 0) continue;
                seen.Add(id);
                if (!replay.TryGetValue(id, out var view))
                {
                    view = Create(mode, materialPath, shader, meshPath);
                    replay.Add(id, view);
                }
                view.system.gameObject.SetActive(true);
                view.system.Pause(false);
                view.system.SetParticles(particles, count);
            }
            if (!apply) return;
            foreach (var pair in replay)
                if (!seen.Contains(pair.Key)) pair.Value.system.gameObject.SetActive(false);
            foreach (var source in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (!owned.Contains(source) && source.TryGetComponent<ParticleSystemRenderer>(out var renderer) &&
                    !suppressed.ContainsKey(renderer))
                {
                    suppressed.Add(renderer, renderer.forceRenderingOff);
                    renderer.forceRenderingOff = true;
                }
            PeakSamples = Math.Max(PeakSamples, MaxParticles - remaining);
        }

        ReplaySystem Create(ParticleSystemRenderMode mode, string materialPath, string shaderName, string meshPath)
        {
            var go = new GameObject("Theatre recorded particles");
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;
            main.startSize3D = true;
            var emission = system.emission;
            emission.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            var mesh = string.IsNullOrEmpty(meshPath) ? null : AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            renderer.renderMode = mode == ParticleSystemRenderMode.Mesh && !mesh ? ParticleSystemRenderMode.Billboard : mode;
            if (mesh) renderer.mesh = mesh;
            var material = string.IsNullOrEmpty(materialPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Material generated = null;
            if (!material && !string.IsNullOrEmpty(shaderName))
            {
                var shader = Shader.Find(shaderName);
                if (shader) material = generated = new Material(shader);
            }
            if (material) renderer.sharedMaterial = material;
            system.Play(false);
            system.Pause(false);
            owned.Add(system);
            return new ReplaySystem { system = system, ownedMaterial = generated };
        }

        public void Dispose()
        {
            foreach (var pair in suppressed)
                if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
            foreach (var view in replay.Values)
            {
                if (view.system) Object.Destroy(view.system.gameObject);
                if (view.ownedMaterial) Object.Destroy(view.ownedMaterial);
            }
        }
    }
}
