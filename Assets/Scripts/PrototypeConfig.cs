using UnityEngine;

namespace JunkyWizard.Prototype
{
    public enum GameplayMode { Lanes, Grid, GridMouse, GridV3, SingleLane, Lanes1to5 }

    /// <summary>Lanes1to5 enemy speed: one cell per bar, per beat or per half beat.</summary>
    public enum EnemyStep { Bar, Beat, HalfBeat }

    /// <summary>All tunable numbers of the grey-box prototype.</summary>
    [CreateAssetMenu(menuName = "Junky Wizard/Prototype Config", fileName = "PrototypeConfig")]
    public class PrototypeConfig : ScriptableObject
    {
        [Header("Beat clock")]
        public double bpm = 123.0;
        [Tooltip("Seconds from the start of the audio file to the first beat.")]
        public double firstBeatOffset = 0.142;
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

        [Header("GridV3: enemies (one cell per bar)")]
        [Tooltip("Bar (4 beats) on which the first enemy can spawn.")]
        public int v3FirstSpawnBar = 2;
        public int v3SpawnIntervalStartBars = 4;
        public int v3SpawnIntervalEndBars = 1;
        [Tooltip("Song time (s) over which the spawn interval ramps linearly from start to end (in bars).")]
        public float v3SpawnRampSeconds = 180f;
        public float intentWeightForward = 1f;
        public float intentWeightForwardRight = 1f;
        public float intentWeightForwardLeft = 1f;

        [Header("GridV3: timed letters")]
        [Tooltip("Letters also count as on time when close to a half beat.")]
        public bool allowHalfBeatLetters = false;
        [Tooltip("Typing one letter more than this makes the buffer explode.")]
        public int maxLetters = 10;
        [Tooltip("Black overlay (80%) fade-out after a buffer explosion.")]
        public float explosionDarkSeconds = 4f;
        [Tooltip("Visual disturbance per cursed letter, added at cast time.")]
        public float cursedEffectSeconds = 3f;

        [Header("GridV3: arrow cast (power = precision x multiplier)")]
        public float castMultiplierPerfect = 1f;
        public float castMultiplierGood = 0.6f;
        public float castMultiplierMiss = 0.3f;
        [Tooltip("heal: +max(healMinLives, round(healLivesPerPower * power)).")]
        public int healMinLives = 1;
        public float healLivesPerPower = 2f;
        [Tooltip("aegis: duration max(aegisMinBeats, round(aegisBeatsPerPower * power)) beats.")]
        public int aegisMinBeats = 4;
        public float aegisBeatsPerPower = 16f;
        [Tooltip("Hits absorbed by a powered aegis (normal: 1).")]
        public int aegisPoweredCharges = 2;

        [Header("Lanes1to5: field")]
        [Tooltip("Lane count used when Prototype.unity is played directly (the boot menu picks 1-5).")]
        [Range(1, 5)] public int v4DefaultLaneCount = 3;
        [Tooltip("Distance between lane centres (m).")]
        public float v4LaneSpacing = 3f;
        public EnemyStep enemyStep = EnemyStep.Bar;

        [Header("Lanes1to5: buffer")]
        [Tooltip("Typing one letter more than this makes the buffer explode.")]
        public int v4MaxLetters = 9;

        [Header("Lanes1to5: enemy resistances (weights)")]
        public float resistWeightNone = 40f;
        public float resistWeightFire = 20f;
        public float resistWeightIce = 20f;
        public float resistWeightWind = 20f;

        [Header("Lanes1to5: shield charges")]
        public int shieldStartCharges = 1;
        public int shieldMaxCharges = 5;

        [Header("Lanes1to5: unlocked spells per lane count (space separated, index = lanes - 1)")]
        public string[] spellsUnlockedByLaneCount =
        {
            "pyro kryo aero pyropyro kryokryo aeroaero iasis aegis",
            "pyro kryo aero pyropyro kryokryo aeroaero pyrsos krymos anemos iasis aegis",
            "pyro kryo aero pyropyro kryokryo aeroaero pyrsos krymos anemos pyrkaia kryeros thyella iasis panakeia aegis",
            "pyro kryo aero pyropyro kryokryo aeroaero pyrsos krymos anemos pyrkaia kryeros thyella iasis panakeia aegis",
            "pyro kryo aero pyropyro kryokryo aeroaero pyrsos krymos anemos pyrkaia kryeros thyella iasis panakeia aegis",
        };

        public int LaneCount => laneX.Length;

        /// <summary>Beats per enemy step in Lanes1to5.</summary>
        public double EnemyStepBeats => enemyStep == EnemyStep.Bar ? 4.0 : enemyStep == EnemyStep.Beat ? 1.0 : 0.5;

        /// <summary>Exponent of speed = min(max, start * exp(accel * holdTime)), so that max is reached at wandTimeToMaxSpeed.</summary>
        public float WandAccel => Mathf.Log(wandMaxSpeed / wandStartSpeed) / Mathf.Max(0.01f, wandTimeToMaxSpeed);

        /// <summary>Spell words indexed by <see cref="Spell"/>.</summary>
        public string[] SpellWords => new[] { pyroWord, healWord, aegisWord };
    }
}
