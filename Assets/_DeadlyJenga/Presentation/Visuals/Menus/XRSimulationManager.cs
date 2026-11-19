using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace DeadlyJenga.Presentation
{
    public static class XRSimulationManager
    {
        public const string ControlModePrefKey = "DeadlyJenga.PreferredControlMode";

        public const int AutoDetect = 0;
        public const int Desktop = 1;
        public const int XRSimulation = 2;
        public const int VRHeadset = 3;

        static bool initialized;

        public static int CurrentMode => PlayerPrefs.GetInt(ControlModePrefKey, AutoDetect);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (!initialized)
            {
                initialized = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            ApplyMode(CurrentMode);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyMode(CurrentMode);
        }

        public static void ApplyMode(int mode)
        {
            if (mode == XRSimulation)
            {
                EnsureSimulator();
            }
            else
            {
                DestroySimulator();
            }
        }

        static void EnsureSimulator()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // Desktop: our own keyboard + mouse driver feeds virtual Quest-style devices.
            DestroyUnitySimulators();
            PcSimulatedVR.Create();
            return;
#else
            if (XRInteractionSimulator.instance != null)
            {
                if (!XRInteractionSimulator.instance.gameObject.activeSelf)
                    XRInteractionSimulator.instance.gameObject.SetActive(true);
                Object.DontDestroyOnLoad(XRInteractionSimulator.instance.gameObject);
                return;
            }

            if (XRDeviceSimulator.instance != null)
            {
                if (!XRDeviceSimulator.instance.gameObject.activeSelf)
                    XRDeviceSimulator.instance.gameObject.SetActive(true);
                Object.DontDestroyOnLoad(XRDeviceSimulator.instance.gameObject);
                return;
            }

            var prefab = LoadSimulatorPrefab();
            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab);
                instance.name = prefab.name;
                Object.DontDestroyOnLoad(instance);
                Debug.Log("[XRSimulationManager] XR Interaction Simulator instantiated and set to DontDestroyOnLoad.");
            }
            else
            {
                Debug.LogWarning("[XRSimulationManager] Unable to find XR Interaction Simulator prefab.");
            }
#endif
        }

        static void DestroySimulator()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (PcSimulatedVR.Instance) Object.Destroy(PcSimulatedVR.Instance.gameObject);
#endif
            DestroyUnitySimulators();
        }

        static void DestroyUnitySimulators()
        {
            if (XRInteractionSimulator.instance != null)
            {
                Object.Destroy(XRInteractionSimulator.instance.gameObject);
            }
            if (XRDeviceSimulator.instance != null)
            {
                Object.Destroy(XRDeviceSimulator.instance.gameObject);
            }

            var existing = Object.FindObjectsByType<XRInteractionSimulator>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].gameObject != null)
                    Object.Destroy(existing[i].gameObject);
            }
            var legacy = Object.FindObjectsByType<XRDeviceSimulator>(FindObjectsSortMode.None);
            for (int i = 0; i < legacy.Length; i++)
            {
                if (legacy[i] != null && legacy[i].gameObject != null)
                    Object.Destroy(legacy[i].gameObject);
            }
        }

        static GameObject LoadSimulatorPrefab()
        {
            var settings = Resources.Load<ScriptableObject>("XRDeviceSimulatorSettings");
            if (settings != null)
            {
                var prop = settings.GetType().GetProperty("simulatorPrefab",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var prefab = prop?.GetValue(settings) as GameObject;
                if (prefab != null) return prefab;

                var field = settings.GetType().GetField("m_SimulatorPrefab",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                prefab = field?.GetValue(settings) as GameObject;
                if (prefab != null) return prefab;
            }

            var resPrefab = Resources.Load<GameObject>("XR Interaction Simulator");
            if (resPrefab != null) return resPrefab;

#if UNITY_EDITOR
            var editorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Samples/XR Interaction Toolkit/3.3.2/XR Interaction Simulator/XR Interaction Simulator.prefab");
            if (editorPrefab != null) return editorPrefab;
#endif

            return null;
        }
    }
}
