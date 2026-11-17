using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadlyJenga.Tests.PlayMode
{
    [TestFixture]
    public class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator PlayMode_Smoke_ObjectSpawnsAndTicks()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PlayModeSmokeCube";
            go.transform.position = new Vector3(0, 10, 0);

            // Yield 1 fixed update frame to let engine simulate
            yield return new WaitForFixedUpdate();

            Assert.IsNotNull(go, "Spawned GameObject should exist.");
            Assert.AreEqual(new Vector3(0, 10, 0), go.transform.position, "Position should match spawn coordinates.");

            Object.Destroy(go);
        }
    }
}
