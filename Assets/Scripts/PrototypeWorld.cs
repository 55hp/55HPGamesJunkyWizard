using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace JunkyWizard.Prototype
{
    /// <summary>Grey-box 3D objects (wizard, lanes, enemies, shield) and simple feedback effects.</summary>
    public class PrototypeWorld : MonoBehaviour
    {
        static readonly Color WizardColor = new Color(0.75f, 0.78f, 0.85f);
        static readonly Color PadColor = new Color(0.25f, 0.25f, 0.27f);
        static readonly Color PadSelectedColor = new Color(0.95f, 0.8f, 0.2f);
        static readonly Color PadLockedColor = new Color(0.55f, 0.48f, 0.2f);
        static readonly Color EnemyWarningColor = new Color(0.9f, 0.12f, 0.1f);
        static readonly Color ShieldColor = new Color(0.3f, 0.8f, 1f, 0.25f);
        static readonly Color PyroColor = new Color(1f, 0.55f, 0.1f);

        const float ProjectileTravelTime = 0.12f;

        static readonly Color GroundColor = new Color(0.4f, 0.4f, 0.42f);
        static readonly Color GuideColor = new Color(0.44f, 0.44f, 0.47f);
        static readonly Color ArmColor = new Color(0.6f, 0.62f, 0.7f);
        static readonly Color WandColor = new Color(0.45f, 0.3f, 0.15f);
        static readonly Color AimLineColor = new Color(1f, 0.85f, 0.3f);
        static readonly Color JetColor = new Color(1f, 0.5f, 0.05f, 0.45f);

        static readonly Vector3 ShoulderPosition = new Vector3(0.45f, 1.5f, 0f);
        const float WandReach = 1.5f; // shoulder to wand tip

        PrototypeConfig config;
        Transform transientRoot;
        Transform ground;
        Transform lanesRoot;
        Transform gridRoot;
        Transform guidesRoot;
        Transform wandPivot;
        Transform aimLine;
        Transform jet;
        Renderer wizardRenderer;
        Renderer[] lanePads;
        GameObject shield;
        float wizardFlashUntil;
        Color wizardFlashColor;

        public Vector3 WizardPosition => new Vector3(0f, 1f, 0f);

        public void Build(PrototypeConfig cfg)
        {
            config = cfg;

            ground = CreatePrimitive(PrimitiveType.Cube, "Ground", transform, GroundColor).transform;

            var wizard = CreatePrimitive(PrimitiveType.Capsule, "Wizard", transform, WizardColor);
            wizard.transform.position = WizardPosition;
            wizardRenderer = wizard.GetComponent<Renderer>();

            shield = CreatePrimitive(PrimitiveType.Sphere, "AegisShield", wizard.transform, ShieldColor, transparent: true);
            shield.transform.localScale = Vector3.one * 2.6f;
            shield.SetActive(false);

            lanesRoot = new GameObject("Lanes").transform;
            lanesRoot.SetParent(transform, false);
            gridRoot = new GameObject("Grid").transform;
            gridRoot.SetParent(transform, false);
            BuildWand();

            lanePads = new Renderer[config.LaneCount];
            for (int i = 0; i < config.LaneCount; i++)
            {
                var pad = CreatePrimitive(PrimitiveType.Cube, $"LanePad{i}", lanesRoot, PadColor);
                pad.transform.position = LanePosition(i);
                pad.transform.localScale = new Vector3(2.2f, 0.12f, 2.2f);
                lanePads[i] = pad.GetComponent<Renderer>();

                var strip = CreatePrimitive(PrimitiveType.Cube, $"LaneStrip{i}", lanesRoot, PadColor);
                strip.transform.position = new Vector3(config.laneX[i], 0.01f, config.laneDistance * 0.5f + 0.5f);
                strip.transform.localScale = new Vector3(0.08f, 0.02f, config.laneDistance - 2f);
            }

            transientRoot = new GameObject("Transient").transform;
            transientRoot.SetParent(transform, false);
        }

        public Vector3 LanePosition(int lane) => new Vector3(config.laneX[lane], 0f, config.laneDistance);

        /// <summary>Removes enemies, projectiles and effects (round restart).</summary>
        public void Clear()
        {
            StopAllCoroutines();
            foreach (Transform child in transientRoot) Destroy(child.gameObject);
            wizardFlashUntil = 0f;
        }

        public GameObject SpawnEnemy(int lane)
        {
            var enemy = CreatePrimitive(PrimitiveType.Cube, $"Enemy{lane}", transientRoot, Color.gray);
            enemy.transform.position = LanePosition(lane) + Vector3.up * 0.8f;
            enemy.transform.localScale = Vector3.one * 1.4f;
            // Becomes red right away: the whole lifetime is the warning phase.
            enemy.GetComponent<Renderer>().material.color = EnemyWarningColor;
            return enemy;
        }

        /// <summary>Shows the objects of <paramref name="mode"/> and sizes ground and guides (called on round restart).</summary>
        public void SetMode(GameplayMode mode)
        {
            bool grid = mode != GameplayMode.Lanes;
            lanesRoot.gameObject.SetActive(!grid);
            gridRoot.gameObject.SetActive(grid);

            float depth = grid ? GridDepth : config.laneDistance;
            ground.localScale = new Vector3(16f, 0.1f, depth + 10f);
            ground.position = new Vector3(0f, -0.05f, depth * 0.5f + 1f);

            if (guidesRoot) Destroy(guidesRoot.gameObject);
            guidesRoot = null;
            if (grid && config.showGridGuides) BuildGridGuides();

            SetWand(0f);
            SetJet(false, 0f);
        }

        // ---------------------------------------------------------------- grid mode

        /// <summary>Depth of the last grid row (m).</summary>
        public float GridDepth => config.gridRows * config.rowSpacing;

        public Vector3 WandPivotPosition => wandPivot.position;
        public Vector3 WandTip => wandPivot.position + wandPivot.forward * WandReach;
        public Vector3 WandDirection => wandPivot.forward;

        /// <summary>Jet length from the wand tip so that it covers the whole grid depth at the current angle.</summary>
        public float JetLength
        {
            get
            {
                float depth = GridDepth + config.rowSpacing * 0.5f - WandTip.z;
                return depth / Mathf.Max(0.1f, WandDirection.z);
            }
        }

        public GameObject SpawnGridEnemy(Vector3 position)
        {
            var enemy = CreatePrimitive(PrimitiveType.Cube, "GridEnemy", transientRoot, Color.gray);
            enemy.transform.position = position;
            enemy.transform.localScale = Vector3.one * 1.4f;
            return enemy;
        }

        public void KillEnemy(GameObject enemy)
        {
            if (!enemy) return;
            StartCoroutine(Burst(enemy.transform.position, PyroColor, 1.4f, 3f, 0.2f));
            Destroy(enemy);
        }

        public void SetWand(float angle)
        {
            wandPivot.localRotation = Quaternion.Euler(0f, angle, 0f);

            const float aimLength = 60f; // long enough to leave the grid at any angle
            var start = WandTip;
            start.y = 0.03f;
            aimLine.position = start + WandDirection * (aimLength * 0.5f);
            aimLine.rotation = wandPivot.rotation;
            aimLine.localScale = new Vector3(0.06f, 0.02f, aimLength);
        }

        public void SetJet(bool active, float width)
        {
            jet.gameObject.SetActive(active);
            if (!active) return;

            float length = JetLength;
            jet.position = WandTip + WandDirection * (length * 0.5f);
            jet.rotation = wandPivot.rotation;
            jet.localScale = new Vector3(width, 0.6f, length);
        }

        void BuildWand()
        {
            wandPivot = new GameObject("WandPivot").transform;
            wandPivot.SetParent(gridRoot, false);
            wandPivot.position = ShoulderPosition;

            var arm = CreatePrimitive(PrimitiveType.Cube, "Arm", wandPivot, ArmColor);
            arm.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            arm.transform.localScale = new Vector3(0.18f, 0.18f, 0.9f);

            var wand = CreatePrimitive(PrimitiveType.Cube, "Wand", wandPivot, WandColor);
            wand.transform.localPosition = new Vector3(0f, 0f, 0.9f + (WandReach - 0.9f) * 0.5f);
            wand.transform.localScale = new Vector3(0.07f, 0.07f, WandReach - 0.9f);

            aimLine = CreatePrimitive(PrimitiveType.Cube, "AimLine", gridRoot, AimLineColor).transform;
            jet = CreatePrimitive(PrimitiveType.Cube, "PyroJet", gridRoot, JetColor, transparent: true).transform;
        }

        void BuildGridGuides()
        {
            guidesRoot = new GameObject("GridGuides").transform;
            guidesRoot.SetParent(gridRoot, false);

            float depth = GridDepth;
            foreach (float x in config.laneX)
            {
                var line = CreatePrimitive(PrimitiveType.Cube, "GuideColumn", guidesRoot, GuideColor);
                line.transform.position = new Vector3(x, 0.01f, depth * 0.5f);
                line.transform.localScale = new Vector3(0.04f, 0.01f, depth);
            }

            float minX = config.laneX[0], maxX = config.laneX[config.LaneCount - 1];
            for (int row = 0; row <= config.gridRows; row++)
            {
                var line = CreatePrimitive(PrimitiveType.Cube, "GuideRow", guidesRoot, GuideColor);
                line.transform.position = new Vector3((minX + maxX) * 0.5f, 0.01f, row * config.rowSpacing);
                line.transform.localScale = new Vector3(maxX - minX, 0.01f, 0.04f);
            }
        }

        // ---------------------------------------------------------------- lanes mode and shared feedback

        /// <summary>Fires a projectile at the lane; destroys <paramref name="target"/> on arrival (may be null).</summary>
        public void FirePyro(int lane, GameObject target)
        {
            var projectile = CreatePrimitive(PrimitiveType.Sphere, "Pyro", transientRoot, PyroColor);
            projectile.transform.localScale = Vector3.one * 0.5f;
            StartCoroutine(FlyProjectile(projectile, WizardPosition + new Vector3(0f, 0.3f, 0.6f),
                LanePosition(lane) + Vector3.up * 0.8f, target));
        }

        /// <summary>Enemy strike visual: the enemy vanishes and the wizard flashes.</summary>
        public void EnemyStrike(GameObject enemy, bool absorbedByShield)
        {
            if (enemy) Destroy(enemy);
            if (absorbedByShield)
                StartCoroutine(Burst(WizardPosition, ShieldColor * 2f, 2.6f, 4f, 0.25f));
            else
                FlashWizard(Color.red);
        }

        public void FlashWizard(Color color)
        {
            wizardFlashColor = color;
            wizardFlashUntil = Time.time + 0.2f;
        }

        public void Tick(int selectedLane, bool laneLocked, bool shieldActive, double beatPosition, GameObject[] enemies)
        {
            for (int i = 0; i < lanePads.Length; i++)
            {
                Color c = PadColor;
                if (i == selectedLane) c = laneLocked ? PadLockedColor : PadSelectedColor;
                lanePads[i].material.color = c;
            }

            wizardRenderer.material.color = Time.time < wizardFlashUntil ? wizardFlashColor : WizardColor;
            shield.SetActive(shieldActive);

            float pulse = Pulse(beatPosition);
            foreach (var enemy in enemies)
                if (enemy) enemy.transform.localScale = Vector3.one * (1.4f + 0.25f * pulse);
        }

        /// <summary>1 on the beat, decaying to 0 before the next one.</summary>
        public static float Pulse(double beatPosition)
        {
            float phase = (float)(beatPosition - System.Math.Floor(beatPosition));
            return Mathf.Pow(1f - phase, 3f);
        }

        IEnumerator FlyProjectile(GameObject projectile, Vector3 from, Vector3 to, GameObject target)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / ProjectileTravelTime)
            {
                projectile.transform.position = Vector3.Lerp(from, to, t);
                yield return null;
            }
            Destroy(projectile);
            if (target)
            {
                Destroy(target);
                StartCoroutine(Burst(to, PyroColor, 1.4f, 3f, 0.2f));
            }
        }

        IEnumerator Burst(Vector3 position, Color color, float fromScale, float toScale, float duration)
        {
            var burst = CreatePrimitive(PrimitiveType.Sphere, "Burst", transientRoot, color, transparent: true);
            burst.transform.position = position;
            var material = burst.GetComponent<Renderer>().material;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                burst.transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);
                material.color = new Color(color.r, color.g, color.b, 0.5f * (1f - t));
                yield return null;
            }
            Destroy(burst);
            Destroy(material);
        }

        GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Color color, bool transparent = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (transparent) renderer.material = CreateTransparentMaterial(renderer.sharedMaterial);
            renderer.material.color = color;
            return go;
        }

        /// <summary>Copy of the URP Lit default material switched to alpha-blended transparency.</summary>
        static Material CreateTransparentMaterial(Material source)
        {
            var m = new Material(source);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
    }
}
