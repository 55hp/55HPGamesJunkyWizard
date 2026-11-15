using UnityEngine;
using UnityEngine.SceneManagement;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Mode chosen in the boot menu, carried to Prototype.unity. Null when Prototype.unity
    /// is played directly, in which case the config's gameplayMode is used.
    /// </summary>
    public static class GameSession
    {
        const string BootScenePath = "Assets/Scenes/Boot.unity";
        const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";

        public static GameplayMode? SelectedMode { get; private set; }

        /// <summary>GridV3 half-beat letters toggled in the boot menu; null = use the config value.</summary>
        public static bool? AllowHalfBeatLetters { get; set; }

        /// <summary>Lanes1to5 lane count picked in the boot menu; null = config default.</summary>
        public static int? LaneCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            // In case domain reload is disabled.
            SelectedMode = null;
            AllowHalfBeatLetters = null;
            LaneCount = null;
        }

        public static void StartLanes(int laneCount)
        {
            LaneCount = laneCount;
            StartPrototype(GameplayMode.Lanes1to5);
        }

        public static void StartPrototype(GameplayMode mode)
        {
            SelectedMode = mode;
            Load(PrototypeScenePath);
        }

        public static void LoadBoot() => Load(BootScenePath);

        static void Load(string scenePath)
        {
#if UNITY_EDITOR
            // Works even if the editor has not picked up the Build Settings change yet.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(System.IO.Path.GetFileNameWithoutExtension(scenePath));
#endif
        }
    }
}
