using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Grey-box boot menu built from code (v4): Lanes1to5 only, the mouse or keys 1–5 pick the lane count;
    /// H toggles half-beat letters. Older modes are no longer listed (their code stays).
    /// </summary>
    public class BootMenu : MonoBehaviour
    {
        const int MaxLanes = 5;

        [Tooltip("Initial value of the half-beat toggle.")]
        [SerializeField] PrototypeConfig config = null;

        Font font;
        Text halfBeatLabel;

        bool HalfBeatLetters => GameSession.AllowHalfBeatLetters ?? (config != null && config.allowHalfBeatLetters);

        void Awake()
        {
            Cursor.visible = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasGo = new GameObject("Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            var background = CreateRect("Background", root, Vector2.zero, new Vector2(1920f, 1080f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.sizeDelta = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f);

            CreateText("Title", root, new Vector2(0f, 260f), new Vector2(1200f, 100f), 64).text = "JUNKY WIZARD — prototipo";
            CreateText("Hint", root, new Vector2(0f, -300f), new Vector2(1200f, 40f), 24).text =
                "Clic o tasti 1–5: numero di corsie  ·  H: mezzi beat  ·  in gioco Esc torna qui";
            CreateText("Subtitle", root, new Vector2(0f, 150f), new Vector2(1200f, 50f), 32).text = "Corsie 1–5";

            for (int lanes = 1; lanes <= MaxLanes; lanes++)
            {
                int count = lanes;
                CreateButton($"{lanes}   {(lanes == 1 ? "corsia" : "corsie")}", root,
                    new Vector2((lanes - 3) * 240f, 40f), new Vector2(210f, 100f), () => GameSession.StartLanes(count));
            }
            halfBeatLabel = CreateButton("", root, new Vector2(0f, -120f), new Vector2(560f, 100f), ToggleHalfBeat);
            RefreshHalfBeatLabel();
        }

        void ToggleHalfBeat()
        {
            GameSession.AllowHalfBeatLetters = !HalfBeatLetters;
            RefreshHalfBeatLabel();
        }

        void RefreshHalfBeatLabel() =>
            halfBeatLabel.text = $"H   Lettere sui mezzi beat: {(HalfBeatLetters ? "SÌ" : "NO")}";

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) GameSession.StartLanes(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) GameSession.StartLanes(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) GameSession.StartLanes(3);
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) GameSession.StartLanes(4);
            else if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) GameSession.StartLanes(5);
            else if (keyboard.hKey.wasPressedThisFrame)
                ToggleHalfBeat();
        }

        Text CreateButton(string label, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(label, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.34f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateText("Label", rect, Vector2.zero, size - new Vector2(20f, 0f), 40);
            text.text = label;
            text.raycastTarget = false;
            return text;
        }

        RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var text = CreateRect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }
    }
}
