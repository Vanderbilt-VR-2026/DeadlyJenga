using System.Collections;
using System.Collections.Generic;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadlyJenga.Tests
{
    // Integration checks against the real CastleBlock component (World/Blocks).
    public class CastleBlockTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in spawned) if (go) Object.Destroy(go);
            spawned.Clear();
        }

        CastleBlock MakeBlock(Vector3 position)
        {
            var go = new GameObject("Test CastleBlock");
            go.SetActive(false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.75f, 0.25f, 0.25f);
            go.AddComponent<Rigidbody>();
            go.AddComponent<BoxCollider>();
            var block = go.AddComponent<CastleBlock>();
            go.SetActive(true);
            spawned.Add(go);
            return block;
        }

        void Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(20, 1, 20);
            spawned.Add(floor);
        }

        [UnityTest]
        public IEnumerator BlockUsesBlockLayerAndIsClimbable()
        {
            var block = MakeBlock(new Vector3(0, 5, 0));
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(Layers.Block, block.gameObject.layer);
            Assert.IsTrue(block.CanClimb, "A fresh block should be climbable.");
            Assert.IsNotNull(block.Body);
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Block, 0),
                "Blocks must still collide with the default layer.");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Survivor, Layers.Block),
                "Survivors should walk on surface proxies, not real blocks.");
        }

        [UnityTest]
        public IEnumerator PushAppliesVelocityChangeAndRaisesEvent()
        {
            var block = MakeBlock(new Vector3(0, 5, 0));
            block.Body.useGravity = false;
            yield return new WaitForFixedUpdate();
            GameObject pushed = null;
            System.Action<GameObject, Vector3> onPushed = (who, force) => pushed = who;
            GameEvents.Pushed += onPushed;
            try
            {
                block.Push(new Vector3(2, 0, 0), null);
                yield return new WaitForFixedUpdate();
                Assert.AreSame(block.gameObject, pushed, "GameEvents.Pushed was not raised for the block.");
                Assert.That(block.Body.linearVelocity.x, Is.EqualTo(2f).Within(0.2f),
                    $"Velocity after a 2 m/s push: {block.Body.linearVelocity}.");
            }
            finally { GameEvents.Pushed -= onPushed; }
        }

        [UnityTest]
        public IEnumerator DroppedBlockRestsOnFloor()
        {
            Floor();
            var block = MakeBlock(new Vector3(0, 2, 0));
            int steps = Mathf.CeilToInt(4f / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
            float bottom = block.GetComponent<BoxCollider>().bounds.min.y;
            Assert.That(bottom, Is.InRange(-0.03f, 0.05f), $"Block bottom at {bottom:F3} m.");
        }
    }
}
