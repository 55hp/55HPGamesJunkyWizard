using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// GridV3 / SingleLane controller: free typing judged letter by letter on the beat, enemies move one cell per bar.
    /// GridV3: arrows cast the buffer on a column (← ↑ →) or on the wizard (↓); enemies show their intent.
    /// SingleLane: centre column only, enemies always straight; Enter casts on the lane, right Shift on the wizard.
    /// Added at runtime by <see cref="PrototypeGame"/> when the session mode is one of the two.
    /// </summary>
    public class GridV3Game : MonoBehaviour
    {
        const int Self = -1; // ↓ target

        static readonly string[] GridDescriptions =
        {
            "uccide i nemici nella colonna scelta\n(potenziata: tutte le colonne)\nFrecce: ← ↑ →",
            "ripristina vite (potenziata: tutte)\nFreccia: ↓",
            "assorbe il prossimo colpo (potenziata: 2)\nFreccia: ↓",
        };

        static readonly string[] SingleLaneDescriptions =
        {
            "uccide tutti i nemici nella corsia\nTasto: Invio",
            "ripristina vite (potenziata: tutte)\nTasto: Shift destro",
            "assorbe il prossimo colpo (potenziata: 2)\nTasto: Shift destro",
        };

        GameplayMode mode;
        bool SingleLane => mode == GameplayMode.SingleLane;
        string[] Descriptions => SingleLane ? SingleLaneDescriptions : GridDescriptions;

        PrototypeConfig config;
        bool logJudgements;
        bool logInput;
        bool initialized;

        BeatClock clock;
        PrototypeWorld world;
        PrototypeHud hud;
        IntentGridField enemies;
        TimedLetterBuffer letters;
        bool halfBeatLetters;

        InputAction spaceAction;
        InputAction leftAction;
        InputAction upAction;
        InputAction rightAction;
        InputAction downAction;
        InputAction escapeAction;
        InputAction enterAction;
        InputAction rightShiftAction;
        Keyboard textKeyboard;
        Action<InputEventPtr, InputDevice> eventListener;
        double lastTextEventTime = -1.0;

        int lives;
        double shieldUntilBeat;
        int shieldCharges;
        int nextSpawnBeat;
        bool manualOpen;
        int manualPage;

        float explosionTime = float.NegativeInfinity;
        readonly List<float> cursedEffectStarts = new List<float>();
        Transform cameraTransform;
        Vector3 cameraBasePosition;
        Quaternion cameraBaseRotation;

        bool ShieldActive => shieldCharges > 0 && clock.BeatPosition < shieldUntilBeat;

        /// <param name="gameMode">GridV3 or SingleLane.</param>
        public void Init(PrototypeConfig cfg, AudioClip music, bool logJudgements, bool logInput, GameplayMode gameMode)
        {
            config = cfg;
            mode = gameMode;
            this.logJudgements = logJudgements;
            this.logInput = logInput;
            halfBeatLetters = GameSession.AllowHalfBeatLetters ?? config.allowHalfBeatLetters;

            clock = gameObject.AddComponent<BeatClock>();
            clock.Init(config, music);
            clock.OnBeat += HandleBeat;
            clock.OnTrackEnded += RestartRound;

            world = gameObject.AddComponent<PrototypeWorld>();
            world.Build(config);

            hud = gameObject.AddComponent<PrototypeHud>();
            hud.Build(config.SpellWords, Descriptions, config.maxLives, config.LaneCount);

            enemies = new IntentGridField(config, world, SingleLane);
            letters = new TimedLetterBuffer(config.maxLetters);

            spaceAction = CreateAction("Space", "<Keyboard>/space", _ => manualOpen = !manualOpen);
            leftAction = CreateAction("Left", "<Keyboard>/leftArrow", ctx => OnArrow(ctx, 0, -1, "←"));
            upAction = CreateAction("Up", "<Keyboard>/upArrow", ctx => OnArrow(ctx, config.LaneCount / 2, 0, "↑"));
            rightAction = CreateAction("Right", "<Keyboard>/rightArrow", ctx => OnArrow(ctx, config.LaneCount - 1, +1, "→"));
            downAction = CreateAction("Down", "<Keyboard>/downArrow", ctx => OnArrow(ctx, Self, 0, "↓"));
            escapeAction = CreateAction("Escape", "<Keyboard>/escape", _ => GameSession.LoadBoot());
            // SingleLane casting keys. They produce no letters: Enter's '\r' is not a letter, Shift produces no text.
            enterAction = CreateAction("Enter", "<Keyboard>/enter", ctx => OnSingleLaneCast(ctx, enemies.CenterColumn, "Invio"));
            enterAction.AddBinding("<Keyboard>/numpadEnter");
            rightShiftAction = CreateAction("RightShift", "<Keyboard>/rightShift", ctx => OnSingleLaneCast(ctx, Self, "Shift destro"));
            eventListener = OnInputEvent;

            var camera = Camera.main;
            if (camera != null)
            {
                cameraTransform = camera.transform;
                cameraBasePosition = cameraTransform.localPosition;
                cameraBaseRotation = cameraTransform.localRotation;
            }

            initialized = true;
            OnEnable(); // AddComponent already ran OnEnable before Init

            Debug.Log($"[GridV3] mode {mode}, half-beat letters: {(halfBeatLetters ? "ON" : "OFF")}");
        }

        void Start()
        {
            RestartRound();
        }

        void OnEnable()
        {
            if (!initialized) return;
            foreach (var action in Actions) action.Enable();
            InputSystem.onEvent += eventListener;
            BindTextKeyboard();
        }

        void OnDisable()
        {
            if (!initialized) return;
            foreach (var action in Actions) action.Disable();
            InputSystem.onEvent -= eventListener;
            UnbindTextKeyboard();
            ResetCamera();
        }

        void OnDestroy()
        {
            if (!initialized) return;
            foreach (var action in Actions) action.Dispose();
        }

        InputAction[] Actions => new[]
            { spaceAction, leftAction, upAction, rightAction, downAction, escapeAction, enterAction, rightShiftAction };

        void Update()
        {
            if (!initialized) return;
            if (Keyboard.current != textKeyboard) BindTextKeyboard();

            double visualBeat = clock.VisualBeatPosition;
            if (enemies.Tick(visualBeat, enemy => ResolveStrike(enemy, clock.BeatPosition))) return; // round restarted

            world.Tick(-1, false, ShieldActive, visualBeat, Array.Empty<GameObject>());
            hud.Tick(visualBeat, lives, -1, false, manualOpen, manualPage,
                letters.ToRichText() + "_",
                $"{letters.Count}/{letters.MaxLetters}");

            float sinceExplosion = Time.time - explosionTime;
            hud.SetDarkness(config.explosionDarkSeconds > 0f
                ? 0.8f * (1f - Mathf.Clamp01(sinceExplosion / config.explosionDarkSeconds))
                : 0f);

            float cursed = CursedIntensity();
            hud.SetCursedNoise(cursed);
            ShakeCamera(cursed);
        }

        InputAction CreateAction(string name, string binding, Action<InputAction.CallbackContext> onPerformed)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            action.performed += ctx => { if (logInput) Debug.Log($"[Input] {name} (t={ctx.time:0.000})"); };
            action.performed += onPerformed;
            return action;
        }

        // ---------------------------------------------------------------- letters

        void BindTextKeyboard()
        {
            UnbindTextKeyboard();
            textKeyboard = Keyboard.current;
            if (textKeyboard != null) textKeyboard.onTextInput += OnTextInput;
        }

        void UnbindTextKeyboard()
        {
            if (textKeyboard != null) textKeyboard.onTextInput -= OnTextInput;
            textKeyboard = null;
        }

        /// <summary>
        /// onTextInput carries no timestamp: the TEXT event's own time is captured here, since event
        /// listeners run before the event is handed to the device (which then raises onTextInput).
        /// </summary>
        void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (eventPtr.IsA<TextEvent>()) lastTextEventTime = eventPtr.time;
        }

        void OnTextInput(char c)
        {
            if (!char.IsLetter(c)) return;

            double inputTime = lastTextEventTime >= 0.0 ? lastTextEventTime : InputState.currentTime;
            double dsp = clock.InputTimeToDsp(inputTime);
            bool onTime = clock.IsOnGrid(dsp, halfBeatLetters, out double error);
            if (logJudgements)
                Debug.Log($"[Letter] '{c}': {(onTime ? "ok" : "MALEDETTA")} ({error * 1000.0:+0.0;-0.0} ms, beat {clock.BeatAt(dsp):0.00})");

            if (!letters.Add(c, isCursed: !onTime)) Explode();
        }

        /// <summary>Letter over the limit: every enemy dies, the wizard loses a life, the scene goes dark.</summary>
        void Explode()
        {
            if (logJudgements) Debug.Log("[GridV3] buffer explosion");
            enemies.KillAll();
            explosionTime = Time.time;
            LoseLife();
        }

        // ---------------------------------------------------------------- arrows

        /// <param name="target">Column index, or <see cref="Self"/> for ↓.</param>
        /// <param name="pageDirection">-1 / +1 for ← / → with the manual open, 0 for ↑ ↓.</param>
        void OnArrow(InputAction.CallbackContext ctx, int target, int pageDirection, string label)
        {
            double dsp = clock.InputTimeToDsp(ctx.time);

            if (manualOpen)
            {
                // ← → turn the page only on time; ↑ ↓ do nothing. Arrows never cast with the manual open.
                if (pageDirection == 0) return;
                if (Judge(dsp, "Page " + label) != Judgement.Miss)
                    manualPage = Mathf.Clamp(manualPage + pageDirection, 0, hud.PageCount - 1);
                return;
            }

            if (SingleLane) return; // arrows only turn pages in SingleLane
            CastBuffer(dsp, target, label);
        }

        /// <summary>SingleLane: Enter casts on the lane, right Shift on the wizard. No cast with the manual open (as the GridV3 arrows).</summary>
        void OnSingleLaneCast(InputAction.CallbackContext ctx, int target, string label)
        {
            if (!SingleLane || manualOpen) return;
            CastBuffer(clock.InputTimeToDsp(ctx.time), target, label);
        }

        /// <summary>Judges the cast key, empties the buffer, applies the cursed backlash and the recognised spell.</summary>
        /// <param name="target">Column index, or <see cref="Self"/>.</param>
        void CastBuffer(double dsp, int target, string label)
        {
            var judgement = Judge(dsp, "Cast " + label);
            if (letters.Count == 0) return;

            string text = letters.Text;
            int cursedCount = letters.CursedCount;
            letters.Clear();
            AddCursedEffects(cursedCount);

            if (!SpellRecognizer.TryRecognize(text, config.SpellWords, out var spell))
            {
                if (logJudgements) Debug.Log($"[GridV3] '{text}': no spell");
                return;
            }

            float multiplier = judgement == Judgement.Perfect ? config.castMultiplierPerfect
                : judgement == Judgement.Good ? config.castMultiplierGood
                : config.castMultiplierMiss;
            float power = spell.Precision * multiplier;
            bool success = Cast(spell, power, target, dsp);
            if (logJudgements)
                Debug.Log($"[GridV3] '{text}' → {spell.Spell}{(spell.Powered ? " POTENZIATA" : "")} {label}: "
                    + $"precisione {spell.Precision:0.00} × {multiplier:0.0} = potenza {power:0.00}, "
                    + $"{(success ? "ok" : "fallita")}, maledette {cursedCount}");
        }

        Judgement Judge(double dsp, string label)
        {
            var judgement = clock.Judge(dsp, out double error);
            if (logJudgements)
                Debug.Log($"[Judge] {label}: {judgement} ({error * 1000.0:+0.0;-0.0} ms, beat {clock.BeatAt(dsp):0.00})");
            return judgement;
        }

        /// <returns>False if the spell fails on this target.</returns>
        bool Cast(RecognizedSpell spell, float power, int target, double dsp)
        {
            switch (spell.Spell)
            {
                case Spell.Pyro:
                    if (target == Self) return false;
                    if (spell.Powered && !SingleLane) // SingleLane: powered pyro is the same as pyro
                    {
                        for (int column = 0; column < config.LaneCount; column++) world.FlashColumn(column);
                        enemies.KillAll();
                    }
                    else
                    {
                        world.FlashColumn(target);
                        enemies.KillColumn(target);
                    }
                    return true;

                case Spell.Heal:
                    if (target != Self) return false;
                    int amount = spell.Powered ? config.maxLives
                        : Mathf.Max(config.healMinLives, Mathf.RoundToInt(config.healLivesPerPower * power));
                    lives = Mathf.Min(config.maxLives, lives + amount);
                    world.FlashWizard(Color.green);
                    return true;

                case Spell.Aegis:
                    if (target != Self) return false;
                    int beats = Mathf.Max(config.aegisMinBeats, Mathf.RoundToInt(config.aegisBeatsPerPower * power));
                    // Not stackable: a recast replaces charges and duration, counted from the cast beat.
                    shieldUntilBeat = Math.Round(clock.BeatAt(dsp)) + beats;
                    shieldCharges = spell.Powered ? Mathf.Max(1, config.aegisPoweredCharges) : 1;
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- cursed backlash

        void AddCursedEffects(int count)
        {
            for (int i = 0; i < count; i++) cursedEffectStarts.Add(Time.time);
        }

        /// <summary>Sum of the active effects, each fading linearly from 1 to 0 over cursedEffectSeconds.</summary>
        float CursedIntensity()
        {
            float duration = Mathf.Max(0.01f, config.cursedEffectSeconds);
            float intensity = 0f;
            for (int i = cursedEffectStarts.Count - 1; i >= 0; i--)
            {
                float remaining = 1f - (Time.time - cursedEffectStarts[i]) / duration;
                if (remaining <= 0f) cursedEffectStarts.RemoveAt(i);
                else intensity += remaining;
            }
            return intensity;
        }

        void ShakeCamera(float intensity)
        {
            if (cameraTransform == null) return;
            float amount = Mathf.Min(intensity, 5f);
            float t = Time.time;
            cameraTransform.localPosition = cameraBasePosition
                + new Vector3(Mathf.Sin(t * 13f), Mathf.Sin(t * 17f + 1f) * 0.6f, 0f) * (0.06f * amount);
            cameraTransform.localRotation = cameraBaseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 0.8f * amount);
        }

        void ResetCamera()
        {
            if (cameraTransform == null) return;
            cameraTransform.localPosition = cameraBasePosition;
            cameraTransform.localRotation = cameraBaseRotation;
        }

        // ---------------------------------------------------------------- beats, strikes, round

        void HandleBeat(int beat)
        {
            if (enemies.OnBeat(beat, enemy => ResolveStrike(enemy, beat))) return; // round restarted

            if (IntentGridField.IsBarStart(beat) && beat >= nextSpawnBeat)
            {
                enemies.TrySpawn(beat);
                nextSpawnBeat = beat + IntentGridField.BeatsPerBar * SpawnIntervalBarsAt(beat);
            }
        }

        /// <returns>True if the strike killed the wizard and the round restarted.</returns>
        bool ResolveStrike(GameObject enemy, double beat)
        {
            if (shieldCharges > 0 && beat < shieldUntilBeat)
            {
                shieldCharges--;
                world.EnemyStrike(enemy, absorbedByShield: true);
                return false;
            }

            world.EnemyStrike(enemy, absorbedByShield: false);
            return LoseLife();
        }

        /// <returns>True if the wizard died and the round restarted.</returns>
        bool LoseLife()
        {
            world.FlashWizard(Color.red);
            lives--;
            if (lives > 0) return false;
            RestartRound();
            return true;
        }

        /// <summary>Spawn interval in bars, linearly from start to end over v3SpawnRampSeconds of song time.</summary>
        int SpawnIntervalBarsAt(int beat)
        {
            float t = Mathf.Clamp01((float)(clock.SongTimeOfBeat(beat) / config.v3SpawnRampSeconds));
            int bars = Mathf.RoundToInt(Mathf.Lerp(config.v3SpawnIntervalStartBars, config.v3SpawnIntervalEndBars, t));
            return Mathf.Max(1, bars);
        }

        void RestartRound()
        {
            world.Clear();
            world.SetMode(mode);
            hud.SetMode(mode, Descriptions);
            Cursor.visible = true;
            enemies.Clear();
            letters.Clear();
            lives = Mathf.Min(config.startLives, config.maxLives);
            shieldUntilBeat = double.NegativeInfinity;
            shieldCharges = 0;
            nextSpawnBeat = config.v3FirstSpawnBar * IntentGridField.BeatsPerBar;
            manualOpen = false;
            manualPage = 0;
            explosionTime = float.NegativeInfinity;
            cursedEffectStarts.Clear();
            ResetCamera();
            clock.StartTrack();
        }
    }
}
