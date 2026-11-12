using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JunkyWizard.Prototype
{
    /// <summary>Grey-box boot menu built from code: pick a gameplay mode with the mouse or 1 / 2 / 3.</summary>
    public class BootMenu : MonoBehaviour
    {
        static readonly (string label, GameplayMode mode)[] Entries =
        {
            ("1   Lanes", GameplayMode.Lanes),
            ("2   Grid — frecce", GameplayMode.Grid),
            ("3   Grid — mouse", GameplayMode.GridMouse),
        };

        Font font;

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
                "Clic o tasti 1 / 2 / 3  ·  in gioco Esc torna qui";

            for (int i = 0; i < Entries.Length; i++)
            {
                var mode = Entries[i].mode;
                CreateButton(Entries[i].label, root, new Vector2(0f, 110f - i * 130f), () => GameSession.StartPrototype(mode));
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) GameSession.StartPrototype(Entries[0].mode);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) GameSession.StartPrototype(Entries[1].mode);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) GameSession.StartPrototype(Entries[2].mode);
        }

        void CreateButton(string label, Transform parent, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(label, parent, position, new Vector2(560f, 100f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.34f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateText("Label", rect, Vector2.zero, new Vector2(540f, 100f), 40);
            text.text = label;
            text.raycastTarget = false;
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
