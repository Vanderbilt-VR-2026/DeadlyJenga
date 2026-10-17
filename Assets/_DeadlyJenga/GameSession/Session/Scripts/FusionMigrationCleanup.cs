using System;
using System.Reflection;
using Fusion;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class FusionMigrationCleanup
    {
        // Fusion 2.1.3 disposes its allocator before freeing resumed-host snapshots.
        // Free that local cache while the allocator is alive. The cloud snapshot stays intact.
        public static bool BeforeShutdown(NetworkRunner runner)
        {
            if(!runner||!runner.IsRunning||!runner.IsServer)return false;
            var version=typeof(NetworkRunner).Assembly.GetName().Version;
            if(version!=new Version(2,1,3,0))return false;
            var field=typeof(NetworkRunner).GetField("_simulation",BindingFlags.Instance|BindingFlags.NonPublic);
            var simulation=field?.GetValue(runner);
            if(simulation==null)return false;
            var cleanup=simulation.GetType().GetMethod("DisposeHostMigration",BindingFlags.Instance|BindingFlags.NonPublic);
            if(cleanup==null){Debug.LogWarning("Fusion 2.1.3 migration cleanup was unavailable.");return false;}
            cleanup.Invoke(simulation,null);return true;
        }
    }
}
