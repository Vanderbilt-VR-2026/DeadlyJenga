using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;

namespace DeadlyJenga.Tests.EditMode
{
    [TestFixture]
    public class SmokeTests
    {
        [Test]
        public void EditMode_Environment_GravityIsConfigured()
        {
            // Verify project gravity vector has a negative Y component
            Assert.Less(Physics.gravity.y, 0f, "Physics.gravity should have a negative Y component.");
        }

        [Test]
        public void EditMode_Core_TestDummyInitializesExpectedHealth()
        {
            var go = new GameObject("TestDummyHost");
            try
            {
                var dummy = go.AddComponent<TestDummy>();
                Assert.AreEqual(100f, dummy.MaxHealth, "TestDummy should default to 100 max health.");
                
                var awakeMethod = typeof(TestDummy).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                awakeMethod?.Invoke(dummy, null);
                Assert.AreEqual(100f, dummy.Health, "TestDummy should initialize current health to max health after Awake.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
