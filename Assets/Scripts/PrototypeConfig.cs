using UnityEngine;

namespace JunkyWizard.Prototype
{
    public enum GameplayMode { Lanes, Grid }

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

        [Header("Mode (applied on round restart)")]
        public GameplayMode gameplayMode = GameplayMode.Grid;

        [Header("Grid")]
        [Tooltip("Rows of depth. Row 0 is the wizard row; enemies spawn up to row gridRows.")]
        [Range(8, 20)] public int gridRows = 12;
        public float rowSpacing = 2f;
        [Tooltip("Very faint debug lines for columns and rows.")]
        public bool showGridGuides = false;

        [Header("Grid enemies: spawn")]
        public float spawnWeightBack = 50f;
        public float spawnWeightSide = 50f;
        [Tooltip("Side spawns pick a random row between this and gridRows.")]
        public int sideSpawnMinRow = 4;
        [Tooltip("Visual x of side spawns, outside the grid.")]
        public float sideSpawnX = 6f;

        [Header("Grid enemies: movement patterns (weights)")]
        public float patternWeightStraight = 1f;
        public float patternWeightZigZag = 1f;
        public float patternWeightLateral = 1f;

        [Header("Grid enemies: hitbox and visual motion")]
        public float enemyHitRadius = 1f;
        [Tooltip("Cell-to-cell interpolation over one beat (x = beat fraction, y = progress). Overshoot above 1 is allowed.")]
        public AnimationCurve moveCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.2f),
            new Keyframe(0.6f, 1.12f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));
        public float wobbleAmplitude = 0.4f;
        [Tooltip("Lateral sine oscillation, in cycles per beat.")]
        public float wobbleFrequency = 0.5f;

        [Header("Wand")]
        [Tooltip("Max yaw from forward, both sides (degrees).")]
        public float wandMaxAngle = 60f;
        [Tooltip("Degrees per second when an arrow is first held.")]
        public float wandStartSpeed = 60f;
        [Tooltip("Degrees per second cap.")]
        public float wandMaxSpeed = 200f;
        [Tooltip("Seconds of holding needed to reach wandMaxSpeed (wandAccel is derived from this).")]
        public float wandTimeToMaxSpeed = 0.5f;

        [Header("Pyro jet (Grid)")]
        public float pyroWidth = 1.5f;
        [Tooltip("Jet duration from the Enter press; not scaled by the judgement.")]
        public float pyroDurationSeconds = 3f;
        [Tooltip("Scales the jet width only.")]
        public float pyroMultiplierPerfect = 1f;
        public float pyroMultiplierGood = 0.6f;
        public float pyroMultiplierMiss = 0.3f;

        public int LaneCount => laneX.Length;

        /// <summary>Exponent of speed = min(max, start * exp(accel * holdTime)), so that max is reached at wandTimeToMaxSpeed.</summary>
        public float WandAccel => Mathf.Log(wandMaxSpeed / wandStartSpeed) / Mathf.Max(0.01f, wandTimeToMaxSpeed);

        /// <summary>Spell words indexed by <see cref="Spell"/>.</summary>
        public string[] SpellWords => new[] { pyroWord, healWord, aegisWord };
    }
}
