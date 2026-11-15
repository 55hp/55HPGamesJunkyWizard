using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Lanes1to5 controller (v4): 1–5 lanes, timed lane selection (← →), cast on the selected lane
    /// (Enter / ↑) or on the wizard (right Shift / ↓), elemental spells vs resistant enemies, shield charges.
    /// Added at runtime by <see cref="PrototypeGame"/> when the session mode is Lanes1to5.
    /// </summary>
    public class LanesGame : MonoBehaviour
    {
        PrototypeConfig config;
        bool logJudgements;
        bool logInput;
        bool initialized;

        int laneCount;
        BeatClock clock;
        PrototypeWorld world;
        PrototypeHud hud;
        LaneEnemyField enemies;
        TimedLetterBuffer letters;
        bool halfBeatLetters;
        List<SpellDef> unlocked;
        string[] unlockedWords;

        InputAction spaceAction;
        InputAction leftAction;
        InputAction rightAction;
        InputAction upAction;
        InputAction downAction;
        InputAction enterAction;
        InputAction rightShiftAction;
        InputAction escapeAction;
        Keyboard textKeyboard;
        Action<InputEventPtr, InputDevice> eventListener;
        double lastTextEventTime = -1.0;

        int lives;
        int shieldCharges;
        int selectedLane;
        bool manualOpen;
        int manualPage;
        double stepBeats;
        long lastStep;
        double nextSpawnBeat;

        float explosionTime = float.NegativeInfinity;
        readonly List<float> cursedEffectStarts = new List<float>();
        Transform cameraTransform;
        Vector3 cameraBasePosition;
        Quaternion cameraBaseRotation;

        public void Init(PrototypeConfig cfg, AudioClip music, bool logJudgements, bool logInput, int lanes)
        {
            config = cfg;
            this.logJudgements = logJudgements;
            this.logInput = logInput;
            laneCount = Mathf.Clamp(lanes, 1, 5);
            halfBeatLetters = GameSession.AllowHalfBeatLetters ?? config.allowHalfBeatLetters;
            stepBeats = config.EnemyStepBeats;

            unlocked = SpellBook.Unlocked(config, laneCount);
            unlockedWords = new string[unlocked.Count];
            var descriptions = new string[unlocked.Count];
            for (int i = 0; i < unlocked.Count; i++)
            {
                unlockedWords[i] = unlocked[i].Word;
                descriptions[i] = $"{unlocked[i].ElementLabel} · {unlocked[i].Effect}\n{unlocked[i].TargetLabel}";
            }
            if (unlocked.Count == 0)
            {
                unlockedWords = new[] { "—" };
                descriptions = new[] { "nessuna magia sbloccata" };
            }

            clock = gameObject.AddComponent<BeatClock>();
            clock.Init(config, music);
            clock.OnTrackEnded += RestartRound;

            world = gameObject.AddComponent<PrototypeWorld>();
            world.Build(config);

            hud = gameObject.AddComponent<PrototypeHud>();
            hud.Build(unlockedWords, descriptions, config.maxLives, laneCount);
            pageDescriptions = descriptions;

            enemies = new LaneEnemyField(config, world, laneCount);
            letters = new TimedLetterBuffer(config.v4MaxLetters);

            spaceAction = CreateAction("Space", "<Keyboard>/space", _ => manualOpen = !manualOpen);
            leftAction = CreateAction("Left", "<Keyboard>/leftArrow", ctx => OnSideArrow(ctx, -1, "←"));
            rightAction = CreateAction("Right", "<Keyboard>/rightArrow", ctx => OnSideArrow(ctx, +1, "→"));
            upAction = CreateAction("Up", "<Keyboard>/upArrow", ctx => OnCast(ctx, toLane: true, "↑"));
            downAction = CreateAction("Down", "<Keyboard>/downArrow", ctx => OnCast(ctx, toLane: false, "↓"));
            enterAction = CreateAction("Enter", "<Keyboard>/enter", ctx => OnCast(ctx, toLane: true, "Invio"));
            enterAction.AddBinding("<Keyboard>/numpadEnter");
            rightShiftAction = CreateAction("RightShift", "<Keyboard>/rightShift", ctx => OnCast(ctx, toLane: false, "Shift destro"));
            escapeAction = CreateAction("Escape", "<Keyboard>/escape", _ => GameSession.LoadBoot());
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

            Debug.Log($"[Lanes] {laneCount} corsie, passo {config.enemyStep}, mezzi beat {(halfBeatLetters ? "ON" : "OFF")}, "
                + $"magie: {string.Join(" ", unlockedWords)}");
        }

        string[] pageDescriptions;

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
            { spaceAction, leftAction, rightAction, upAction, downAction, enterAction, rightShiftAction, escapeAction };

        void Update()
        {
            if (!initialized) return;
            if (Keyboard.current != textKeyboard) BindTextKeyboard();

            // Enemy steps on the dsp clock (bar / beat / half beat).
            long step = (long)Math.Floor(clock.BeatPosition / stepBeats);
            while (lastStep < step)
            {
                lastStep++;
                if (DoStep(lastStep * stepBeats)) return; // round restarted
            }

            double visualBeat = clock.VisualBeatPosition;
            if (enemies.Tick(visualBeat, enemy => ResolveStrike(enemy))) return; // round restarted

            world.Tick(-1, false, false, visualBeat, Array.Empty<GameObject>());
            world.SetSelectedLane(enemies.LaneX(selectedLane));
            world.SetShieldOrbs(shieldCharges, visualBeat);
            hud.Tick(visualBeat, lives, selectedLane, false, manualOpen, manualPage,
                letters.ToRichText() + "_",
                $"{letters.Count}/{letters.MaxLetters}");
            hud.SetShieldCharges(shieldCharges, config.shieldMaxCharges);

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

        /// <summary>Captures the TEXT event time: listeners run before the device raises onTextInput.</summary>
        void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (eventPtr.IsA<TextEvent>()) lastTextEventTime = eventPtr.time;
        }

        /// <summary>Letters only (Enter's '\r', Space and Shift add nothing); case is ignored.</summary>
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
            if (logJudgements) Debug.Log("[Lanes] esplosione del buffer");
            enemies.KillAll();
            explosionTime = Time.time;
            LoseLife();
        }

        // ---------------------------------------------------------------- keys

        /// <summary>← →: manual page with the manual open, otherwise lane selection. Both only on Perfect/Good.</summary>
        void OnSideArrow(InputAction.CallbackContext ctx, int direction, string label)
        {
            double dsp = clock.InputTimeToDsp(ctx.time);
            if (manualOpen)
            {
                if (Judge(dsp, "Pagina " + label) != Judgement.Miss)
                    manualPage = Mathf.Clamp(manualPage + direction, 0, hud.PageCount - 1);
                return;
            }

            if (Judge(dsp, "Corsia " + label) != Judgement.Miss)
                selectedLane = Mathf.Clamp(selectedLane + direction, 0, laneCount - 1);
        }

        /// <summary>Casts the buffer; also with the manual open.</summary>
        void OnCast(InputAction.CallbackContext ctx, bool toLane, string label)
        {
            double dsp = clock.InputTimeToDsp(ctx.time);
            var judgement = Judge(dsp, "Lancio " + label);
            if (letters.Count == 0) return;

            string text = letters.Text;
            int cursedCount = letters.CursedCount;
            letters.Clear();
            AddCursedEffects(cursedCount);

            if (!SpellRecognizer.TryRecognizeBest(text, unlockedWords, out int index, out float precision) || unlocked.Count == 0)
            {
                if (logJudgements) Debug.Log($"[Lanes] '{text}': nessuna magia (sbloccata), maledette {cursedCount}");
                return;
            }

            // [DEFAULT] v4: power is logged only, effects ignore it.
            float multiplier = judgement == Judgement.Perfect ? config.castMultiplierPerfect
                : judgement == Judgement.Good ? config.castMultiplierGood
                : config.castMultiplierMiss;
            var spell = unlocked[index];
            bool success = spell.TargetsLane == toLane && Cast(spell);
            if (logJudgements)
                Debug.Log($"[Lanes] '{text}' → {spell.Word} ({spell.Kind}) {label}: precisione {precision:0.00} × {multiplier:0.0} "
                    + $"= potenza {precision * multiplier:0.00}, {(success ? "ok" : "fallita")}, maledette {cursedCount}");
        }

        Judgement Judge(double dsp, string label)
        {
            var judgement = clock.Judge(dsp, out double error);
            if (logJudgements)
                Debug.Log($"[Judge] {label}: {judgement} ({error * 1000.0:+0.0;-0.0} ms, beat {clock.BeatAt(dsp):0.00})");
            return judgement;
        }

        /// <summary>Applies a spell already matched to its target type.</summary>
        bool Cast(SpellDef spell)
        {
            var color = LaneEnemyField.ResistanceColor(spell.Element);
            switch (spell.Kind)
            {
                case SpellKind.Base:
                case SpellKind.Double:
                case SpellKind.Beam:
                    int maxHits = spell.Kind == SpellKind.Base ? 1 : spell.Kind == SpellKind.Double ? 2 : int.MaxValue;
                    world.FlashLane(enemies.LaneX(selectedLane), color);
                    enemies.HitLane(selectedLane, spell.Element, maxHits, color);
                    return true;

                case SpellKind.Line:
                    for (int lane = 0; lane < laneCount; lane++)
                    {
                        world.FlashLane(enemies.LaneX(lane), color);
                        enemies.HitLane(lane, spell.Element, 1, color);
                    }
                    return true;

                case SpellKind.Heal:
                    lives = Mathf.Min(config.maxLives, lives + 1);
                    world.FlashWizard(Color.green);
                    return true;

                case SpellKind.FullHeal:
                    lives = config.maxLives;
                    world.FlashWizard(Color.green);
                    return true;

                case SpellKind.Aegis:
                    shieldCharges = Mathf.Min(config.shieldMaxCharges, shieldCharges + 1);
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

        // ---------------------------------------------------------------- steps, strikes, round

        /// <returns>True if the round restarted.</returns>
        bool DoStep(double stepBeat)
        {
            if (enemies.OnStep(stepBeat, ResolveStrike)) return true;

            bool barStart = Math.Abs(stepBeat / IntentGridField.BeatsPerBar - Math.Round(stepBeat / IntentGridField.BeatsPerBar)) < 1e-6;
            if (barStart && stepBeat >= nextSpawnBeat)
            {
                enemies.TrySpawn(stepBeat);
                nextSpawnBeat = stepBeat + IntentGridField.BeatsPerBar * SpawnIntervalBarsAt(stepBeat);
            }
            return false;
        }

        /// <summary>A charge absorbs the hit; without charges the wizard loses a life.</summary>
        /// <returns>True if the wizard died and the round restarted.</returns>
        bool ResolveStrike(GameObject enemy)
        {
            if (shieldCharges > 0)
            {
                shieldCharges--;
                world.EnemyStrike(enemy, absorbedByShield: true);
                return false;
            }

            world.EnemyStrike(enemy, absorbedByShield: false);
            return LoseLife();
        }

        bool LoseLife()
        {
            world.FlashWizard(Color.red);
            lives--;
            if (lives > 0) return false;
            RestartRound();
            return true;
        }

        int SpawnIntervalBarsAt(double beat)
        {
            float t = Mathf.Clamp01((float)(clock.SongTimeOfBeat(beat) / config.v3SpawnRampSeconds));
            int bars = Mathf.RoundToInt(Mathf.Lerp(config.v3SpawnIntervalStartBars, config.v3SpawnIntervalEndBars, t));
            return Mathf.Max(1, bars);
        }

        void RestartRound()
        {
            world.Clear();
            world.SetMode(GameplayMode.Lanes1to5);
            var laneXs = new float[laneCount];
            for (int i = 0; i < laneCount; i++) laneXs[i] = enemies.LaneX(i);
            world.SetLaneLayout(laneXs);
            hud.SetMode(GameplayMode.Lanes1to5, pageDescriptions);
            Cursor.visible = true;

            enemies.Clear();
            letters.Clear();
            lives = Mathf.Min(config.startLives, config.maxLives);
            shieldCharges = Mathf.Clamp(config.shieldStartCharges, 0, config.shieldMaxCharges);
            selectedLane = (laneCount - 1) / 2; // centre; even counts: the one left of centre
            manualOpen = false;
            manualPage = 0;
            nextSpawnBeat = config.v3FirstSpawnBar * IntentGridField.BeatsPerBar;
            explosionTime = float.NegativeInfinity;
            cursedEffectStarts.Clear();
            ResetCamera();

            clock.StartTrack();
            lastStep = (long)Math.Floor(clock.BeatPosition / stepBeats);
        }
    }
}
