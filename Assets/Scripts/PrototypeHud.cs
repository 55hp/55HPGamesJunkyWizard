using UnityEngine;
using UnityEngine.UI;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Grey-box uGUI built from code: beat circle, lives, selected lane, typing buffer, manual panel.
    /// Deliberately shows no timing judgement.
    /// </summary>
    public class PrototypeHud : MonoBehaviour
    {
        static readonly Color LifeFull = new Color(0.9f, 0.25f, 0.3f);
        static readonly Color LifeEmpty = new Color(0.2f, 0.2f, 0.22f, 0.8f);
        static readonly Color LaneOff = new Color(0.2f, 0.2f, 0.22f, 0.8f);
        static readonly Color LaneOn = new Color(0.95f, 0.8f, 0.2f);
        static readonly Color LaneLocked = new Color(0.55f, 0.48f, 0.2f);

        Font font;
        Sprite circleSprite;
        RectTransform beatCircle;
        Image beatCircleImage;
        Image[] lifeCells;
        Image[] laneCells;
        Text laneLabel;
        Text bufferText;
        RectTransform manualPanel;
        Vector2 manualBasePosition;
        Text manualTitle;
        Text manualWord;
        Text manualDescription;
        string[] spellWords;
        string[] spellDescriptions;

        public int PageCount => spellWords.Length;

        public void Build(string[] words, string[] descriptions, int maxLives, int laneCount)
        {
            spellWords = words;
            spellDescriptions = descriptions;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            circleSprite = CreateCircleSprite(128);

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // Beat circle (bottom centre) with a faint static ring as reference.
            var ring = CreateImage("BeatRing", root, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(170f, 170f),
                new Color(1f, 1f, 1f, 0.12f), circleSprite);
            beatCircleImage = CreateImage("BeatCircle", root, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(110f, 110f),
                Color.white, circleSprite);
            beatCircle = beatCircleImage.rectTransform;
            ring.raycastTarget = false;

            // Typing buffer, above the beat circle.
            bufferText = CreateText("Buffer", root, new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(800f, 80f), 56,
                TextAnchor.MiddleCenter);

            // Lives (top left).
            CreateText("LivesLabel", root, new Vector2(0f, 1f), new Vector2(110f, -45f), new Vector2(160f, 40f), 28,
                TextAnchor.MiddleLeft).text = "VITE";
            lifeCells = new Image[maxLives];
            for (int i = 0; i < maxLives; i++)
                lifeCells[i] = CreateImage($"Life{i}", root, new Vector2(0f, 1f), new Vector2(60f + i * 46f, -95f),
                    new Vector2(38f, 38f), LifeFull, null);

            // Selected lane (top centre).
            laneLabel = CreateText("LaneLabel", root, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(400f, 36f), 24,
                TextAnchor.MiddleCenter);
            laneCells = new Image[laneCount];
            for (int i = 0; i < laneCount; i++)
                laneCells[i] = CreateImage($"Lane{i}", root, new Vector2(0.5f, 1f),
                    new Vector2((i - (laneCount - 1) * 0.5f) * 80f, -85f), new Vector2(64f, 26f), LaneOff, null);

            // Controls reminder (bottom left).
            CreateText("Controls", root, new Vector2(0f, 0f), new Vector2(260f, 70f), new Vector2(480f, 110f), 20,
                TextAnchor.LowerLeft).text =
                "Spazio: apri magia  ·  lettere  ·  Invio: lancia\n←/→: corsia (manuale chiuso)\nTab: manuale  ·  ←/→ a tempo: pagina";

            // Manual panel (right side).
            var panelImage = CreateImage("Manual", root, new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(500f, 320f),
                new Color(0.08f, 0.08f, 0.1f, 0.88f), null);
            manualPanel = panelImage.rectTransform;
            manualBasePosition = manualPanel.anchoredPosition;
            manualTitle = CreateText("Title", manualPanel, new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(460f, 40f), 24,
                TextAnchor.MiddleCenter);
            manualWord = CreateText("Word", manualPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(460f, 90f), 72,
                TextAnchor.MiddleCenter);
            manualDescription = CreateText("Description", manualPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                new Vector2(440f, 80f), 26, TextAnchor.MiddleCenter);
            CreateText("Hint", manualPanel, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(460f, 30f), 18,
                TextAnchor.MiddleCenter).text = "← / → a tempo per girare pagina";
            manualPanel.gameObject.SetActive(false);
        }

        public void Tick(double beatPosition, int lives, int selectedLane, bool laneLocked,
            bool manualOpen, int manualPage, bool bufferOpen, string buffer)
        {
            float pulse = PrototypeWorld.Pulse(beatPosition);
            int beatInBar = (int)(((long)System.Math.Floor(beatPosition) % 4 + 4) % 4);

            beatCircle.localScale = Vector3.one * (1f + 0.55f * pulse);
            var circleColor = beatInBar == 0 ? new Color(1f, 0.85f, 0.3f) : Color.white;
            circleColor.a = 0.35f + 0.65f * pulse;
            beatCircleImage.color = circleColor;

            for (int i = 0; i < lifeCells.Length; i++)
                lifeCells[i].color = i < lives ? LifeFull : LifeEmpty;

            for (int i = 0; i < laneCells.Length; i++)
                laneCells[i].color = i != selectedLane ? LaneOff : laneLocked ? LaneLocked : LaneOn;
            laneLabel.text = laneLocked ? "CORSIA (bloccata)" : "CORSIA";

            bufferText.text = bufferOpen ? "> " + buffer + "_" : "";

            manualPanel.gameObject.SetActive(manualOpen);
            if (manualOpen)
            {
                manualPanel.localScale = Vector3.one * (1f + 0.035f * pulse);
                manualPanel.anchoredPosition = manualBasePosition + new Vector2(0f, 10f * pulse);
                manualTitle.text = $"MANUALE  {manualPage + 1}/{PageCount}";
                manualWord.text = spellWords[manualPage];
                manualDescription.text = spellDescriptions[manualPage];
            }
        }

        Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
