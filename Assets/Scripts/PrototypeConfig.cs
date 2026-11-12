using UnityEngine;

namespace JunkyWizard.Prototype
{
    /// <summary>All tunable numbers of the grey-box prototype.</summary>
    [CreateAssetMenu(menuName = "Junky Wizard/Prototype Config", fileName = "PrototypeConfig")]
    public class PrototypeConfig : ScriptableObject
    {
        [Header("Beat clock")]
        public double bpm = 123.0;
        [Tooltip("Seconds from the start of the audio file to the first beat.")]
        public double firstBeatOffset = 0.272;
        [Tooltip("Delay between the PlayScheduled call and the actual audio start (s).")]
        public double scheduleLeadTime = 0.5;

        [Header("Timing windows (s, distance from nearest beat)")]
        public double perfectWindow = 0.045;
        public double goodWindow = 0.090;

        [Header("Wizard")]
        public int maxLives = 5;
        public int startLives = 5;

        [Header("Heal (lives restored)")]
        public int healPerfect = 2;
        public int healGood = 1;
        public int healMiss = 1;

        [Header("Aegis (duration in beats)")]
        public int aegisBeatsPerfect = 16;
        public int aegisBeatsGood = 10;
        public int aegisBeatsMiss = 4;

        [Header("Enemies")]
        [Tooltip("Beats between spawn (red warning) and the strike.")]
        public int enemyWarningBeats = 4;
        [Tooltip("First beat on which an enemy can spawn.")]
        public int firstSpawnBeat = 8;
        public int spawnIntervalStartBeats = 16;
        public int spawnIntervalEndBeats = 4;
        [Tooltip("Song time (s) over which the spawn interval ramps linearly from start to end.")]
        public float spawnRampSeconds = 180f;

        [Header("Lanes")]
        public float[] laneX = { -3f, 0f, 3f };
        public float laneDistance = 15f;

        [Header("Spells (placeholder words)")]
        public string pyroWord = "pyro";
        public string healWord = "heal";
        public string aegisWord = "aegis";

        public int LaneCount => laneX.Length;

        /// <summary>Spell words indexed by <see cref="Spell"/>.</summary>
        public string[] SpellWords => new[] { pyroWord, healWord, aegisWord };
    }
}
