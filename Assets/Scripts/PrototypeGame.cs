using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Entry point of the grey-box prototype: builds clock, world and HUD, reads input,
    /// runs spells, enemies, lives and round restarts.
    /// </summary>
    public class PrototypeGame : MonoBehaviour
    {
        [SerializeField] PrototypeConfig config = null;
        [SerializeField] AudioClip music = null;
        [Tooltip("Log every judged input (Enter, arrows with manual open) to the Console.")]
        [SerializeField] bool logJudgements = true;
        [Tooltip("Diagnostics: log every key received by the prototype to the Console.")]
        [SerializeField] bool logInput = true;

        BeatClock clock;
        PrototypeWorld world;
        PrototypeHud hud;
        SpellTyper typer;

        InputAction spaceAction;
        InputAction enterAction;
        InputAction leftAction;
        InputAction rightAction;
        InputAction tabAction;
        Keyboard textKeyboard;

        int lives;
        int selectedLane;
        double shieldUntilBeat;
        int nextSpawnBeat;
        bool manualOpen;
        int manualPage;
        GameObject[] enemyViews;
        int[] enemyHitBeats;

        bool ShieldActive => clock.BeatPosition < shieldUntilBeat;

        void Awake()
        {
            clock = gameObject.AddComponent<BeatClock>();
            clock.Init(config, music);
            clock.OnBeat += HandleBeat;
            clock.OnTrackEnded += RestartRound;

            world = gameObject.AddComponent<PrototypeWorld>();
            world.Build(config);

            hud = gameObject.AddComponent<PrototypeHud>();
            hud.Build(config.SpellWords, new[]
            {
                "colpisce il nemico nella corsia selezionata",
                "ripristina vite (di più se lanciata a tempo)",
                "scudo che assorbe il prossimo colpo (dura di più se lanciato a tempo)",
            }, config.maxLives, config.LaneCount);

            typer = new SpellTyper(config.SpellWords);
            enemyViews = new GameObject[config.LaneCount];
            enemyHitBeats = new int[config.LaneCount];

            spaceAction = CreateAction("Space", "<Keyboard>/space", _ => typer.Open());
            enterAction = CreateAction("Enter", "<Keyboard>/enter", OnEnter);
            enterAction.AddBinding("<Keyboard>/numpadEnter");
            leftAction = CreateAction("Left", "<Keyboard>/leftArrow", ctx => OnArrow(ctx, -1));
            rightAction = CreateAction("Right", "<Keyboard>/rightArrow", ctx => OnArrow(ctx, +1));
            tabAction = CreateAction("Tab", "<Keyboard>/tab", _ => manualOpen = !manualOpen);
        }

        void Start()
        {
            RestartRound();
        }

        void OnEnable()
        {
            spaceAction.Enable();
            enterAction.Enable();
            leftAction.Enable();
            rightAction.Enable();
            tabAction.Enable();

            BindTextKeyboard();
            if (logInput)
                Debug.Log($"[Input] Keyboard: {(Keyboard.current != null ? Keyboard.current.displayName : "NONE")}, devices: {InputSystem.devices.Count}");
        }

        void OnDisable()
        {
            spaceAction.Disable();
            enterAction.Disable();
            leftAction.Disable();
            rightAction.Disable();
            tabAction.Disable();

            UnbindTextKeyboard();
        }

        void OnDestroy()
        {
            spaceAction.Dispose();
            enterAction.Dispose();
            leftAction.Dispose();
            rightAction.Dispose();
            tabAction.Dispose();
        }

        void Update()
        {
            // Keyboard.current can be null in OnEnable or change at runtime: keep onTextInput bound to it.
            if (Keyboard.current != textKeyboard) BindTextKeyboard();

            double visualBeat = clock.VisualBeatPosition;
            world.Tick(selectedLane, manualOpen, ShieldActive, visualBeat, enemyViews);
            hud.Tick(visualBeat, lives, selectedLane, manualOpen, manualOpen, manualPage, typer.IsOpen, typer.Buffer);
        }

        InputAction CreateAction(string name, string binding, Action<InputAction.CallbackContext> onPerformed)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            action.performed += ctx => { if (logInput) Debug.Log($"[Input] {name} (t={ctx.time:0.000})"); };
            action.performed += onPerformed;
            return action;
        }

        // ---------------------------------------------------------------- input

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

        void OnTextInput(char c)
        {
            if (logInput && !char.IsControl(c)) Debug.Log($"[Input] text '{c}'");
            typer.Type(c);
        }

        void OnEnter(InputAction.CallbackContext ctx)
        {
            if (!typer.IsOpen) return;

            int spell = typer.Submit();
            if (spell < 0) return; // incomplete word: buffer already cancelled

            double dsp = clock.InputTimeToDsp(ctx.time);
            var judgement = Judge(dsp, "Enter " + config.SpellWords[spell]);
            Cast((Spell)spell, judgement, dsp);
        }

        void OnArrow(InputAction.CallbackContext ctx, int direction)
        {
            if (!manualOpen)
            {
                selectedLane = Mathf.Clamp(selectedLane + direction, 0, config.LaneCount - 1);
                return;
            }

            var judgement = Judge(clock.InputTimeToDsp(ctx.time), direction < 0 ? "Page ←" : "Page →");
            if (judgement != Judgement.Miss)
                manualPage = Mathf.Clamp(manualPage + direction, 0, hud.PageCount - 1);
        }

        Judgement Judge(double dsp, string label)
        {
            var judgement = clock.Judge(dsp, out double error);
            if (logJudgements)
                Debug.Log($"[Judge] {label}: {judgement} ({error * 1000.0:+0.0;-0.0} ms, beat {clock.BeatAt(dsp):0.00})");
            return judgement;
        }

        // ---------------------------------------------------------------- spells

        void Cast(Spell spell, Judgement judgement, double dsp)
        {
            switch (spell)
            {
                case Spell.Pyro:
                    // Any judgement kills; an empty lane means the shot is wasted.
                    var target = enemyViews[selectedLane];
                    enemyViews[selectedLane] = null;
                    world.FirePyro(selectedLane, target);
                    break;

                case Spell.Heal:
                    int amount = judgement == Judgement.Perfect ? config.healPerfect
                        : judgement == Judgement.Good ? config.healGood
                        : config.healMiss;
                    lives = Mathf.Min(config.maxLives, lives + amount);
                    world.FlashWizard(Color.green);
                    break;

                case Spell.Aegis:
                    int beats = judgement == Judgement.Perfect ? config.aegisBeatsPerfect
                        : judgement == Judgement.Good ? config.aegisBeatsGood
                        : config.aegisBeatsMiss;
                    // Not stackable: recasting just restarts the duration from the cast beat.
                    shieldUntilBeat = Math.Round(clock.BeatAt(dsp)) + beats;
                    break;
            }
        }

        // ---------------------------------------------------------------- beats, enemies, round

        void HandleBeat(int beat)
        {
            for (int lane = 0; lane < enemyViews.Length; lane++)
            {
                if (enemyViews[lane] == null || beat < enemyHitBeats[lane]) continue;

                var enemy = enemyViews[lane];
                enemyViews[lane] = null;
                if (ResolveStrike(enemy, beat)) return; // round restarted
            }

            if (beat >= nextSpawnBeat)
            {
                TrySpawnEnemy(beat);
                nextSpawnBeat = beat + SpawnIntervalAt(beat);
            }
        }

        /// <returns>True if the strike killed the wizard and the round restarted.</returns>
        bool ResolveStrike(GameObject enemy, int beat)
        {
            if (beat < shieldUntilBeat)
            {
                shieldUntilBeat = double.NegativeInfinity;
                world.EnemyStrike(enemy, absorbedByShield: true);
                return false;
            }

            world.EnemyStrike(enemy, absorbedByShield: false);
            lives--;
            if (lives > 0) return false;

            RestartRound();
            return true;
        }

        void TrySpawnEnemy(int beat)
        {
            int freeCount = 0;
            for (int lane = 0; lane < enemyViews.Length; lane++)
                if (enemyViews[lane] == null) freeCount++;
            if (freeCount == 0) return;

            int pick = UnityEngine.Random.Range(0, freeCount);
            for (int lane = 0; lane < enemyViews.Length; lane++)
            {
                if (enemyViews[lane] != null) continue;
                if (pick-- > 0) continue;

                enemyViews[lane] = world.SpawnEnemy(lane);
                enemyHitBeats[lane] = beat + config.enemyWarningBeats;
                return;
            }
        }

        /// <summary>Spawn interval in beats, linearly from start to end over spawnRampSeconds of song time.</summary>
        int SpawnIntervalAt(int beat)
        {
            float t = Mathf.Clamp01((float)(clock.SongTimeOfBeat(beat) / config.spawnRampSeconds));
            int interval = Mathf.RoundToInt(Mathf.Lerp(config.spawnIntervalStartBeats, config.spawnIntervalEndBeats, t));
            return Mathf.Max(1, interval);
        }

        void RestartRound()
        {
            world.Clear();
            Array.Clear(enemyViews, 0, enemyViews.Length);
            lives = Mathf.Min(config.startLives, config.maxLives);
            selectedLane = config.LaneCount / 2;
            shieldUntilBeat = double.NegativeInfinity;
            nextSpawnBeat = config.firstSpawnBeat;
            manualOpen = false;
            manualPage = 0;
            typer.Cancel();
            clock.StartTrack();
        }
    }
}
