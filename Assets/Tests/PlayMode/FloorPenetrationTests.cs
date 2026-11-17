using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadlyJenga.Tests.PlayMode
{
    [TestFixture]
    public class FloorPenetrationTests
    {
        private GameObject floorGo;
        private BoxCollider floorCollider;

        [SetUp]
        public void SetUp()
        {
            floorGo = new GameObject("TestFloor");
            floorGo.transform.position = Vector3.zero;
            floorCollider = floorGo.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(20f, 1f, 20f);
            floorCollider.center = new Vector3(0f, -0.5f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (floorGo != null)
            {
                Object.Destroy(floorGo);
            }
        }

        [UnityTest]
        public IEnumerator FallingObject_DoesNotPenetrateFloor()
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "DroppingBlock";
            block.transform.position = new Vector3(0f, 3f, 0f);

            var rb = block.AddComponent<Rigidbody>();
            rb.mass = 1.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            float elapsed = 0f;
            while (elapsed < 2.0f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            var blockCollider = block.GetComponent<Collider>();
            float floorTopY = floorCollider.bounds.max.y;
            float blockBottomY = blockCollider.bounds.min.y;

            Assert.GreaterOrEqual(blockBottomY, floorTopY - 0.05f,
                $"Block penetrated floor! Block bottom Y: {blockBottomY:F4}, Floor top Y: {floorTopY:F4}");

            Assert.Less(rb.linearVelocity.magnitude, 0.5f,
                $"Block should come to rest on the floor. Linear velocity was {rb.linearVelocity.magnitude}");

            Object.Destroy(block);
        }

        [UnityTest]
        public IEnumerator HighVelocityFallingObject_DoesNotPenetrateFloor()
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "FastBlock";
            block.transform.position = new Vector3(2f, 5f, 2f);

            var rb = block.AddComponent<Rigidbody>();
            rb.mass = 2.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = new Vector3(0f, -25f, 0f);

            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            var blockCollider = block.GetComponent<Collider>();
            float floorTopY = floorCollider.bounds.max.y;
            float blockBottomY = blockCollider.bounds.min.y;

            Assert.GreaterOrEqual(blockBottomY, floorTopY - 0.05f,
                $"High velocity block penetrated floor! Block bottom Y: {blockBottomY:F4}, Floor top Y: {floorTopY:F4}");

            Object.Destroy(block);
        }

        [UnityTest]
        public IEnumerator StackedBlocks_MaintainSupportStability()
        {
            var bottomBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomBlock.name = "BottomBlock";
            bottomBlock.transform.position = new Vector3(0f, 0.5f, 0f);
            var bottomRb = bottomBlock.AddComponent<Rigidbody>();
            bottomRb.mass = 1.0f;

            var topBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topBlock.name = "TopBlock";
            topBlock.transform.position = new Vector3(0f, 1.55f, 0f);
            var topRb = topBlock.AddComponent<Rigidbody>();
            topRb.mass = 1.0f;

            float elapsed = 0f;
            while (elapsed < 2.0f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            var topCollider = topBlock.GetComponent<Collider>();
            var bottomCollider = bottomBlock.GetComponent<Collider>();

            Assert.GreaterOrEqual(topCollider.bounds.min.y, bottomCollider.bounds.max.y - 0.05f,
                "Top block fell through bottom block.");

            Assert.GreaterOrEqual(bottomCollider.bounds.min.y, floorCollider.bounds.max.y - 0.05f,
                "Bottom block fell through floor.");

            Object.Destroy(bottomBlock);
            Object.Destroy(topBlock);
        }
    }
}
