using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.InputSystem.LowLevel;

namespace DeadlyJenga.XRSimulation.Editor
{
    // Explicit fixture lifetime only. No editor-load registration and no attachable Editor MonoBehaviour.
    public static class ManagedFrameObserver
    {
        static readonly List<Action> observers = new List<Action>();
        public const string SamplingPoint = "Immediately after PreLateUpdate.ScriptRunBehaviourLateUpdate in the current real PlayerLoop";
        public static int ObserverCount => observers.Count;
        [Serializable] sealed class Cleanup
        {
            public int frame, observers, ownedHooks;
            public bool stillPlaying;
            public string updateType;
        }
        public static bool SaveCleanup(string path)
        {
            var state = new Cleanup { frame = Time.frameCount, observers = observers.Count,
                ownedHooks = Count(PlayerLoop.GetCurrentPlayerLoop(), typeof(ManagedFrameObserver)),
                stillPlaying = Application.isPlaying, updateType = InputState.currentUpdateType.ToString() };
            File.WriteAllText(path, JsonUtility.ToJson(state, true));
            return state.stillPlaying && state.observers == 0 && state.ownedHooks == 0;
        }

        // One read-only observation on a real player frame. The existing runner still owns scenario execution.
        public static IEnumerator Observe(Action observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            bool done = false, disposed = false, running = false;
            Exception error = null;
            int firstFrame = Time.frameCount;
            float deadline = Time.realtimeSinceStartup + 15;
            using (Register(() =>
            {
                if (done || disposed || running) return;
                running = true;
                try
                {
                    if (InputState.currentUpdateType == InputUpdateType.Editor)
                        throw new InvalidOperationException("Real player observation still has Editor input state; do not use it as a controller result.");
                    observation();
                }
                catch (Exception exception) { error = exception; }
                finally { done = true; running = false; }
            }))
            {
                try
                {
                    while (!done)
                    {
                        if (Time.frameCount - firstFrame > 120 || Time.realtimeSinceStartup > deadline)
                            throw new TimeoutException("One real post-LateUpdate input observation did not execute within its bounded window.");
                        yield return null;
                    }
                    if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
                }
                finally { disposed = true; }
            }
        }

        sealed class Subscription : IDisposable
        {
            Action callback;
            public Subscription(Action callback) { this.callback = callback; }
            public void Dispose()
            {
                if (callback == null) return;
                observers.Remove(callback); callback = null;
                if (observers.Count != 0) return;
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                Remove(ref loop);
                PlayerLoop.SetPlayerLoop(loop);
            }
        }
        public static IDisposable Register(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (observers.Contains(callback)) throw new InvalidOperationException("Observer already registered.");
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            int hooks = Count(loop, typeof(ManagedFrameObserver));
            if (hooks > 1) throw new InvalidOperationException("Duplicate XR fixture PlayerLoop hooks.");
            if (hooks == 0)
            {
                if (Count(loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate)) != 1 || !Insert(ref loop))
                    throw new InvalidOperationException("Real ScriptRunBehaviourLateUpdate hook point is missing or ambiguous; timing fixture cannot run.");
                PlayerLoop.SetPlayerLoop(loop);
            }
            else if (!Owns(loop)) throw new InvalidOperationException("XR fixture hook type has a foreign delegate; preserve it.");
            observers.Add(callback);
            return new Subscription(callback);
        }
        static int Count(PlayerLoopSystem node, Type type)
        {
            int count = node.type == type ? 1 : 0;
            if (node.subSystemList != null) foreach (var child in node.subSystemList) count += Count(child, type);
            return count;
        }
        static bool Owns(PlayerLoopSystem node)
        {
            if (node.type == typeof(ManagedFrameObserver)) return node.updateDelegate == Tick;
            if (node.subSystemList != null) foreach (var child in node.subSystemList) if (Owns(child)) return true;
            return false;
        }
        static bool Insert(ref PlayerLoopSystem node)
        {
            if (node.subSystemList == null) return false;
            for (int i = 0; i < node.subSystemList.Length; i++)
            {
                var child = node.subSystemList[i];
                if (child.type == typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate))
                {
                    var list = new List<PlayerLoopSystem>(node.subSystemList);
                    list.Insert(i + 1, new PlayerLoopSystem { type = typeof(ManagedFrameObserver), updateDelegate = Tick });
                    node.subSystemList = list.ToArray(); return true;
                }
                if (Insert(ref child)) { node.subSystemList[i] = child; return true; }
            }
            return false;
        }
        static void Remove(ref PlayerLoopSystem node)
        {
            if (node.subSystemList == null) return;
            var keep = new List<PlayerLoopSystem>();
            foreach (var entry in node.subSystemList)
            {
                var child = entry;
                if (child.type == typeof(ManagedFrameObserver) && child.updateDelegate == Tick) continue;
                Remove(ref child); keep.Add(child);
            }
            // Remove only our current node. Never restore an old whole-loop snapshot over another owner's hooks.
            node.subSystemList = keep.ToArray();
        }
        static void Tick()
        {
            foreach (var callback in observers.ToArray())
            {
                if (!observers.Contains(callback)) continue;
                try { callback(); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }
    }
}
