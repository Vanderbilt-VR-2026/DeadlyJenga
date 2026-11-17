using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadlyJenga.Tests
{
    // Generic Unity physics checks. They do not use any DeadlyJenga gameplay code.
    public class PhysicsSmokeTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in spawned) if (go) Object.Destroy(go);
            spawned.Clear();
        }

        [Test]
        public void TestFrameworkRuns()
        {
            Assert.AreEqual(4, 2 + 2);
        }

        GameObject Floor()
        {
            // 1 m thick floor whose top surface is at y = 0.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(20, 1, 20);
            spawned.Add(floor);
            return floor;
        }

        Rigidbody Box(Vector3 position, Vector3 size, float mass, CollisionDetectionMode mode)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Test Box";
            box.transform.position = position;
            box.transform.localScale = size;
            var body = box.AddComponent<Rigidbody>();
            body.mass = mass;
            body.collisionDetectionMode = mode;
            spawned.Add(box);
            return body;
        }

        static IEnumerator Simulate(float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        // Height, start speed (down), detection mode.
        static readonly object[] FallCases =
        {
            new object[] { 0.5f, 0f, CollisionDetectionMode.Discrete },
            new object[] { 5f, 0f, CollisionDetectionMode.Discrete },
            new object[] { 20f, 0f, CollisionDetectionMode.ContinuousDynamic },
            new object[] { 10f, 40f, CollisionDetectionMode.ContinuousDynamic },
        };

        [UnityTest]
        public IEnumerator FallingBoxDoesNotPenetrateFloor(
            [ValueSource(nameof(FallCases))] object[] c)
        {
            float height = (float)c[0], speed = (float)c[1];
            var mode = (CollisionDetectionMode)c[2];
            Floor();
            var body = Box(new Vector3(0, height, 0), new Vector3(0.75f, 0.25f, 0.25f), 1f, mode);
            body.linearVelocity = Vector3.down * speed;
            var box = body.GetComponent<Collider>();

            float lowest = float.MaxValue;
            int steps = Mathf.CeilToInt(4f / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, box.bounds.min.y);
            }

            // Solver contact offset is ~0.01 m; allow a little extra for a hard landing.
            const float tolerance = 0.03f;
            float bottom = box.bounds.min.y;
            // The solver allows brief overlap on a hard impact; passing halfway
            // through the 1 m floor would mean the box tunnelled.
            Assert.That(lowest, Is.GreaterThan(-0.5f),
                $"Box tunnelled {-lowest:F3} m into the floor (height {height}, speed {speed}, {mode}).");
            Assert.That(bottom, Is.GreaterThan(-tolerance),
                $"Box rests {-bottom:F3} m below the floor top (tolerance {tolerance}).");
            Assert.That(bottom, Is.LessThan(0.3f), $"Box did not land: bottom at {bottom:F3} m.");
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.1f),
                $"Box still moving at {body.linearVelocity.magnitude:F3} m/s after 4 s.");
        }

        [UnityTest]
        public IEnumerator HeavyAndLightBoxesBothRestOnFloor([Values(0.1f, 1f, 50f)] float mass)
        {
            Floor();
            var body = Box(new Vector3(0, 2, 0), Vector3.one * 0.5f, mass, CollisionDetectionMode.Discrete);
            yield return Simulate(3f);
            float bottom = body.GetComponent<Collider>().bounds.min.y;
            Assert.That(bottom, Is.InRange(-0.03f, 0.05f), $"Mass {mass}: bottom at {bottom:F3} m.");
        }

        [UnityTest]
        public IEnumerator StackOfThreeBlocksStaysStanding()
        {
            Floor();
            var size = new Vector3(0.75f, 0.25f, 0.25f);
            var blocks = new List<Rigidbody>();
            for (int i = 0; i < 3; i++)
                blocks.Add(Box(new Vector3(0, 0.125f + i * 0.25f + 0.001f, 0), size, 1f,
                    CollisionDetectionMode.Discrete));
            var start = blocks[2].position;
            yield return Simulate(4f);
            var top = blocks[2].position;
            Assert.That(Vector2.Distance(new Vector2(top.x, top.z), new Vector2(start.x, start.z)),
                Is.LessThan(0.05f), $"Top block slid sideways to {top}.");
            Assert.That(top.y, Is.EqualTo(0.625f).Within(0.03f), $"Top block height {top.y:F3}.");
        }

        [UnityTest]
        public IEnumerator FrozenAxisDoesNotMove()
        {
            Floor();
            var body = Box(new Vector3(0, 1, 0), Vector3.one * 0.5f, 1f, CollisionDetectionMode.Discrete);
            body.constraints = RigidbodyConstraints.FreezePositionX;
            yield return new WaitForFixedUpdate();
            body.AddForce(new Vector3(5, 0, 5), ForceMode.VelocityChange);
            yield return Simulate(1f);
            Assert.That(body.position.x, Is.EqualTo(0f).Within(0.001f), $"X moved to {body.position.x:F4}.");
            Assert.That(body.position.z, Is.GreaterThan(0.5f), "Unfrozen Z axis should still move.");
        }
    }
}
