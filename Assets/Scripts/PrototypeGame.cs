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
        InputAction upAction;
        InputAction downAction;
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

        GameplayMode mode;
        GridEnemyField gridEnemies;
        WandAim wand;
        double jetUntilBeat;
        float jetWidth;

        static readonly string[] LanesDescriptions =
        {
            "colpisce il nemico nella corsia selezionata",
            "ripristina vite (di più se lanciata a tempo)",
            "scudo che assorbe il prossimo colpo (dura di più se lanciato a tempo)",
        };

        static readonly string[] GridDescriptions =
        {
            "getto di fiamma lungo la bacchetta per 3 s (più largo se lanciato a tempo)",
            LanesDescriptions[1],
            LanesDescriptions[2],
        };

        bool ShieldActive => clock.BeatPosition < shieldUntilBeat;
        bool JetActive => clock.BeatPosition < jetUntilBeat;

        void Awake()
        {
            clock = gameObject.AddComponent<BeatClock>();
            clock.Init(config, music);
            clock.OnBeat += HandleBeat;
            clock.OnTrackEnded += RestartRound;

            world = gameObject.AddComponent<PrototypeWorld>();
            world.Build(config);

            hud = gameObject.AddComponent<PrototypeHud>();
            hud.Build(config.SpellWords, LanesDescriptions, config.maxLives, config.LaneCount);

            typer = new SpellTyper(config.SpellWords);
            enemyViews = new GameObject[config.LaneCount];
            enemyHitBeats = new int[config.LaneCount];
            gridEnemies = new GridEnemyField(config, world);
            wand = new WandAim(config);

            spaceAction = CreateAction("Space", "<Keyboard>/space", _ => typer.Open());
            enterAction = CreateAction("Enter", "<Keyboard>/enter", OnEnter);
            enterAction.AddBinding("<Keyboard>/numpadEnter");
            leftAction = CreateAction("Left", "<Keyboard>/leftArrow", ctx => OnArrow(ctx, -1));
            rightAction = CreateAction("Right", "<Keyboard>/rightArrow", ctx => OnArrow(ctx, +1));
            upAction = CreateAction("Up", "<Keyboard>/upArrow", ctx => OnPageKey(ctx, -1));
            downAction = CreateAction("Down", "<Keyboard>/downArrow", ctx => OnPageKey(ctx, +1));
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
            upAction.Enable();
            downAction.Enable();
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
            upAction.Disable();
            downAction.Disable();
            tabAction.Disable();

            UnbindTextKeyboard();
        }

        void OnDestroy()
        {
            spaceAction.Dispose();
            enterAction.Dispose();
            leftAction.Dispose();
            rightAction.Dispose();
            upAction.Dispose();
            downAction.Dispose();
            tabAction.Dispose();
        }

        void Update()
        {
            // Keyboard.current can be null in OnEnable or change at runtime: keep onTextInput bound to it.
            if (Keyboard.current != textKeyboard) BindTextKeyboard();

            double visualBeat = clock.VisualBeatPosition;
            if (mode == GameplayMode.Grid) UpdateGrid(visualBeat);
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

        /// <summary>Left/right: lane or page in Lanes mode; no discrete function in Grid (they drive the wand in UpdateGrid).</summary>
        void OnArrow(InputAction.CallbackContext ctx, int direction)
        {
            if (mode == GameplayMode.Grid) return;

            if (!manualOpen)
            {
                selectedLane = Mathf.Clamp(selectedLane + direction, 0, config.LaneCount - 1);
                return;
            }

            TurnPage(ctx, direction, direction < 0 ? "Page ←" : "Page →");
        }

        /// <summary>Up/down: previous/next page, Grid mode with the manual open only.</summary>
        void OnPageKey(InputAction.CallbackContext ctx, int direction)
        {
            if (mode != GameplayMode.Grid || !manualOpen) return;
            TurnPage(ctx, direction, direction < 0 ? "Page ↑" : "Page ↓");
        }

        /// <summary>Turns the page only on a Perfect or Good input; pages are not cyclic.</summary>
        void TurnPage(InputAction.CallbackContext ctx, int direction, string label)
        {
            var judgement = Judge(clock.InputTimeToDsp(ctx.time), label);
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
                case Spell.Pyro when mode == GameplayMode.Grid:
                    // Judgement scales the width only; duration is fixed. Not stackable: a recast replaces the running jet.
                    float multiplier = judgement == Judgement.Perfect ? config.pyroMultiplierPerfect
                        : judgement == Judgement.Good ? config.pyroMultiplierGood
                        : config.pyroMultiplierMiss;
                    jetWidth = config.pyroWidth * multiplier;
                    jetUntilBeat = clock.BeatAt(dsp + config.pyroDurationSeconds);
                    break;

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
            if (mode == GameplayMode.Grid)
            {
                if (gridEnemies.OnBeat(beat, enemy => ResolveStrike(enemy, beat))) return; // round restarted
            }
            else
            {
                for (int lane = 0; lane < enemyViews.Length; lane++)
                {
                    if (enemyViews[lane] == null || beat < enemyHitBeats[lane]) continue;

                    var enemy = enemyViews[lane];
                    enemyViews[lane] = null;
                    if (ResolveStrike(enemy, beat)) return; // round restarted
                }
            }

            if (beat >= nextSpawnBeat)
            {
                if (mode == GameplayMode.Grid) gridEnemies.TrySpawn(beat);
                else TrySpawnEnemy(beat);
                nextSpawnBeat = beat + SpawnIntervalAt(beat);
            }
        }

        /// <summary>Grid mode per-frame: wand from held arrows, enemy visuals, pyro jet hits.</summary>
        void UpdateGrid(double visualBeat)
        {
            // Always free: also with the manual or the typing buffer open.
            int direction = (rightAction.IsPressed() ? 1 : 0) - (leftAction.IsPressed() ? 1 : 0);
            wand.Update(direction, Time.deltaTime);
            world.SetWand(wand.Angle);

            gridEnemies.Tick(visualBeat);

            bool jetActive = JetActive;
            world.SetJet(jetActive, jetWidth);
            if (jetActive)
                gridEnemies.KillInJet(world.WandTip, world.WandDirection, world.JetLength, jetWidth * 0.5f);
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
            mode = config.gameplayMode;
            world.Clear();
            world.SetMode(mode);
            hud.SetMode(mode, mode == GameplayMode.Grid ? GridDescriptions : LanesDescriptions);
            Array.Clear(enemyViews, 0, enemyViews.Length);
            gridEnemies.Clear();
            wand.Reset();
            jetUntilBeat = double.NegativeInfinity;
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
