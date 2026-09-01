#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.GroundTruth.Consumers;
using UnityEngine.Perception.GroundTruth.DataModel;

namespace UnityEngine.Perception.Settings
{
    /// <summary>
    /// Package setting that can be stored in unity editor and passed during build execution as a command line parameter
    /// </summary>
    [Serializable]
    [AddComponentMenu("")]
    public class PerceptionSettings : MonoBehaviour
    {
        static string s_GameObjectName = "_PerceptionSettings";
        static PerceptionSettings s_Instance;
        static bool s_HideInHierarchy = true;
        internal Metadata userPreferences { get; private set; }
        string m_CachedPathValue = string.Empty;

        [SerializeReference, ConsumerEndpointDrawer(typeof(IConsumerEndpoint))]
        internal IConsumerEndpoint consumerEndpoint = new SoloEndpoint();

        public static IConsumerEndpoint endpoint
        {
            get => instance.consumerEndpoint;
            set => instance.consumerEndpoint = value;
        }

        /// <summary>
        /// Instance for the managed object
        /// </summary>
        internal static PerceptionSettings instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var obj = GameObject.Find(s_GameObjectName);
                    if (obj == null)
                    {
                        obj = new GameObject(s_GameObjectName);
                    }

                    s_Instance = obj.GetComponent<PerceptionSettings>();
                    if (s_Instance == null)
                    {
                        s_Instance = obj.AddComponent<PerceptionSettings>();
                    }

                    s_Instance.Initialize();
                }

                s_Instance.gameObject.hideFlags = s_HideInHierarchy ? HideFlags.HideInHierarchy | HideFlags.HideInInspector : HideFlags.None;

                return s_Instance;
            }
        }

#if !UNITY_EDITOR
        static (bool, string) GetPathFromCommandLine()
        {
            (bool, string)errorResult = (false, string.Empty);

            var args = Environment.GetCommandLineArgs();
            var index = Array.FindIndex(args, x => x.Equals("--output-path"));

            if (index == -1)
            {
                Debug.Log($"--output-path was not provided on command line, using default location: {defaultOutputPath}");
                return errorResult;
            }

            if (index == args.Length - 1)
            {
                var msg = $"--output-path was present on command line, but path was not defined, using default location: {defaultOutputPath}";
                Debug.LogError(msg);
                return errorResult;
            }

            var path = args[index + 1];
            if (Directory.Exists(path))
            {
                return (true, path);
            }

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                var msg = $"An invalid path ({path}) was requested with --output-path, using default location: {defaultOutputPath}";
                Debug.LogError(msg + e);
                return errorResult;
            }

            return (true, path);
        }

        /// <summary>
        /// Method to get output path based on configuration
        /// </summary>
        /// <returns>Path to the output file</returns>
        public static string GetOutputBasePath()
        {
#if UNITY_SIMULATION_CORE_PRESENT
            if (Unity.Simulation.Configuration.Instance.IsSimulationRunningInCloud())
                return defaultOutputPath;
#endif
            var(isOk, path) = GetPathFromCommandLine();

            return isOk ? path : defaultOutputPath;
        }

#else
        /// <summary>
        /// Method to get output path based on configuration
        /// </summary>
        /// <returns>Path to the output file</returns>
        public static string GetOutputBasePath()
        {
#if UNITY_SIMULATION_CORE_PRESENT
            if (Unity.Simulation.Configuration.Instance.IsSimulationRunningInCloud())
                return defaultOutputPath;
#endif
            return instance.userPreferences.TryGetValue($"{instance.consumerEndpoint.GetType().FullName}.output_path", out string path) ? path : defaultOutputPath;
        }

#endif

        /// <summary>
        /// Sets the output path for the active endpoint type. This will set the path for the next simulation, it will
        /// not affect a simulation that is currently executing. In order for this to take effect the caller should call
        /// <see cref="DatasetCapture.ResetSimulation"/>
        /// </summary>
        /// <param name="path">The output path</param>
        public static void SetOutputBasePath(string path)
        {
            instance.userPreferences.Add($"{instance.consumerEndpoint.GetType().FullName}.output_path", path);
#if UNITY_EDITOR
            Save();
#endif
        }

        /// <summary>
        /// Gets the index offset for the active endpoint type. The offset shifts the indices used to
        /// name the generated data, so that a new run can be appended to an already generated dataset
        /// without having to renumber its files. Set it to the amount of data already present in the
        /// target dataset, or to 0 to start from the beginning.
        /// </summary>
        /// <returns>The index offset, or 0 when none was set for the active endpoint type</returns>
        public static int GetIndexOffset()
        {
            return instance.userPreferences.TryGetValue($"{instance.consumerEndpoint.GetType().FullName}.index_offset", out int indexOffset) ? indexOffset : 0;
        }

        /// <summary>
        /// Sets the index offset for the active endpoint type. This will set the offset for the next
        /// simulation, it will not affect a simulation that is currently executing. In order for this
        /// to take effect the caller should call <see cref="DatasetCapture.ResetSimulation"/>
        /// </summary>
        /// <param name="indexOffset">The index offset. Negative values are treated as 0</param>
        public static void SetIndexOffset(int indexOffset)
        {
            instance.userPreferences.Add($"{instance.consumerEndpoint.GetType().FullName}.index_offset", Mathf.Max(0, indexOffset));
#if UNITY_EDITOR
            Save();
#endif
        }

        /// <summary>
        /// Default output folder for the dataset generation
        /// </summary>
        public static string defaultOutputPath
        {
            get
            {
                var persistentDataPath = Application.persistentDataPath;
#if UNITY_SIMULATION_CORE_PRESENT
                if (Unity.Simulation.Configuration.Instance.IsSimulationRunningInCloud())
                    persistentDataPath = Unity.Simulation.Configuration.Instance.GetStoragePath();
#endif
                return persistentDataPath;
            }
        }

        string filePath
        {
            get
            {
                if (string.IsNullOrEmpty(m_CachedPathValue))
                {
                    m_CachedPathValue = Path.Combine(Application.dataPath, "..", "UserSettings", "PerceptionSettings.json");
                }

                return m_CachedPathValue;
            }
        }

        void Initialize()
        {
            if (File.Exists(filePath))
            {
                try
                {
                    var fileStr = File.ReadAllText(filePath);
                    s_Instance.userPreferences = Metadata.FromJson(fileStr);
                }
                catch (Exception e)
                {
                    Debug.Log($"Failed to load data file {e.Message}");
                    s_Instance.userPreferences = new Metadata();
                }
            }
            else
            {
                s_Instance.userPreferences = new Metadata();
            }
        }

#if UNITY_EDITOR
        public static void Save()
        {
            var json = instance.userPreferences.ToJson();
            File.WriteAllText(instance.filePath, json);
        }

#endif

#if UNITY_EDITOR
        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(instance);
        }

#endif

        [SerializeReference]
        public AccumulationSettings accumulationSettings = new AccumulationSettings()
        {
            accumulationSamples = 256,
            shutterInterval = 0,
            shutterFullyOpen = 0,
            shutterBeginsClosing = 1,
            adaptFixedLengthScenarioFrames = true,
        };
    }
}
