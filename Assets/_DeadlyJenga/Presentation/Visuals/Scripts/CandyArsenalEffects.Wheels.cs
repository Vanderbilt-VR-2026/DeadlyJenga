using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyArsenalEffects
    {
        sealed class LodgedWheel
        {
            public Transform model;
            public CandyWeaponImpacts.SurfaceContact contact;
            public Vector3 normal, point, releasePoint;
            public Quaternion rotation;
            public float releaseAt, dropStarted = -1;
        }

        const int WheelResidueLimit = 20;
        const float WheelHangSeconds = 2.8f;
        readonly List<LodgedWheel> lodgedWheels = new List<LodgedWheel>(WheelResidueLimit);
        ParticleSystem wheelFragments;
        Mesh wheelFragmentMesh;

        void LodgeWheel(EquipmentActionFrame action)
        {
            if (MatchRuntime.Current?.Frame.phase != RoundPhase.Playing ||
                !CandyWeaponImpacts.VisibleToAnyCamera(action.position, 90)) return;

            var actor = effectsRuntime.FindActor(action.actor);
            var delta = actor != null ? action.position - actor.ViewPose.position : Vector3.zero;
            var forward = delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.forward;
            CandyWeaponImpacts.TryContact(action.position, forward, out var contact, .19f);
            if (lodgedWheels.Count == WheelResidueLimit)
            {
                ReturnProjectile(lodgedWheels[0].model);
                lodgedWheels.RemoveAt(0);
            }

            var model = TakeProjectile(EquipmentKind.Chainsaw, action.position);
            var trail = model.GetComponent<TrailRenderer>();
            trail.emitting = false;
            trail.Clear();
            var wheel = new LodgedWheel
            {
                model = model, contact = contact, normal = -forward, point = action.position,
                releaseAt = Time.time + WheelHangSeconds - Mathf.Clamp(.8f - action.remaining, 0, .8f)
            };
            SeatWheel(wheel);
            lodgedWheels.Add(wheel);
        }

        static void SeatWheel(LodgedWheel wheel)
        {
            if (wheel.contact.anchor && wheel.contact.anchor.gameObject.activeInHierarchy)
            {
                var frame = wheel.contact.anchor.localToWorldMatrix * wheel.contact.localFrame;
                wheel.point = frame.GetColumn(3);
                wheel.normal = ((Vector3)frame.GetColumn(2)).normalized;
            }
            var forward = -wheel.normal;
            var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .94f ? Vector3.forward : Vector3.up;
            wheel.rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(90, 0, 0);
            wheel.model.SetPositionAndRotation(wheel.point + wheel.normal * .18f, wheel.rotation);
        }

        void UpdateLodgedWheels(bool playing)
        {
            if (!playing)
            {
                ClearLodgedWheels();
                return;
            }

            for (int i = lodgedWheels.Count - 1; i >= 0; i--)
            {
                var wheel = lodgedWheels[i];
                if (Time.time < wheel.releaseAt)
                {
                    SeatWheel(wheel);
                    continue;
                }
                if (wheel.dropStarted < 0)
                {
                    wheel.dropStarted = Time.time;
                    wheel.releasePoint = wheel.model.position;
                }

                float age = Time.time - wheel.dropStarted;
                var point = wheel.releasePoint + wheel.normal * (.24f * age) + Vector3.down * (4.9f * age * age);
                var travel = point - wheel.model.position;
                int mask = 1 | (1 << Layers.Block) | (1 << Layers.Surface);
                RaycastHit hit = default;
                bool landed = travel.sqrMagnitude > .000001f && Physics.Raycast(wheel.model.position, travel.normalized, out hit,
                    travel.magnitude + .10f, mask, QueryTriggerInteraction.Ignore);
                if (landed || age >= 1.05f)
                {
                    BreakWheel(landed ? hit.point : point);
                    ReturnProjectile(wheel.model);
                    lodgedWheels.RemoveAt(i);
                    continue;
                }
                wheel.model.SetPositionAndRotation(point, wheel.rotation * Quaternion.Euler(age * 410, 0, age * 180));
            }
        }

        void BreakWheel(Vector3 point)
        {
            if (!wheelFragments)
            {
                var root = new GameObject("Pooled wheel fragments");
                root.transform.SetParent(transform, false);
                wheelFragments = root.AddComponent<ParticleSystem>();
                wheelFragments.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = wheelFragments.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 128;
                main.gravityModifier = 1;
                var emission = wheelFragments.emission;
                emission.enabled = false;
                var shape = wheelFragments.shape;
                shape.enabled = false;
                wheelFragmentMesh = new Mesh
                {
                    name = "Broken wheel steel shard",
                    vertices = new[] { new Vector3(-.48f,0,-.31f), new Vector3(.57f,0,-.21f),
                        new Vector3(.15f,0,.49f), new Vector3(-.06f,.26f,.10f) },
                    triangles = new[] { 0,1,2,0,3,1,1,3,2,2,3,0 }
                };
                wheelFragmentMesh.RecalculateNormals();
                wheelFragmentMesh.RecalculateBounds();
                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = wheelFragmentMesh;
                renderer.sharedMaterial = ordnanceMetal;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (wheelFragments.isStopped) wheelFragments.Play();
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 2.39996f;
                wheelFragments.Emit(new ParticleSystem.EmitParams
                {
                    position = point + Vector3.up * .04f,
                    velocity = new Vector3(Mathf.Cos(angle), .55f + i % 3 * .24f, Mathf.Sin(angle)) * (1.2f + i * .24f),
                    rotation = i * 51, startLifetime = .25f + .035f * i, startSize = .06f + .015f * (i % 3)
                }, 1);
            }
            EmitBlast(new EquipmentActionFrame { kind = EquipmentKind.Chainsaw, position = point });
        }

        void ClearLodgedWheels()
        {
            foreach (var wheel in lodgedWheels) ReturnProjectile(wheel.model);
            lodgedWheels.Clear();
            if (wheelFragments) wheelFragments.Clear();
        }
    }
}
