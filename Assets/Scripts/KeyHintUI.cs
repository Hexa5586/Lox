using UnityEngine;

[DisallowMultipleComponent]
public class KeyHintUI : MonoBehaviour
{
    private static readonly Color HintColor = new Color(119f / 255f, 119f / 255f, 119f / 255f, 1f);

    private GUIStyle textStyle;
    private Texture2D background;
    private Texture2D mouseIcon;
    private Texture2D keyW;
    private Texture2D keyA;
    private Texture2D keyS;
    private Texture2D keyD;
    private Texture2D arrowLeft;
    private Texture2D arrowRight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<KeyHintUI>() != null)
        {
            return;
        }

        new GameObject("KeyHintUI").AddComponent<KeyHintUI>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        background = CreateSolidTexture(new Color(0f, 0f, 0f, 0.7f));
        mouseIcon = LoadIcon("Icons/MouseLeft");
        keyW = LoadIcon("Icons/W");
        keyA = LoadIcon("Icons/A");
        keyS = LoadIcon("Icons/S");
        keyD = LoadIcon("Icons/D");
        arrowLeft = LoadIcon("Icons/ArrowLeft");
        arrowRight = LoadIcon("Icons/ArrowRight");

        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "Arial" },
            18);

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        textStyle = new GUIStyle
        {
            font = font,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            richText = false
        };
        textStyle.normal.textColor = HintColor;
    }

    private void OnGUI()
    {
        GUI.depth = -100;

        DeviceSelectable selected = DeviceSelector.Instance != null
            ? DeviceSelector.Instance.Selected
            : null;
        bool hasSelection = selected != null;
        bool showMouse = true;
        bool showMove = hasSelection && selected.CanMove;
        bool showRotate = hasSelection && selected.CanRotate;
        int lineCount = 1 + (showMove ? 1 : 0) + (showRotate ? 1 : 0);

        if (lineCount == 0)
        {
            return;
        }

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
        float margin = 24f * scale;
        float width = 430f * scale;
        float lineHeight = 56f * scale;
        float height = (24f + lineHeight / scale * lineCount) * scale;

        var panel = new Rect(
            margin,
            Screen.height - height - margin,
            width,
            height);

        GUI.DrawTexture(panel, background);

        textStyle.fontSize = Mathf.RoundToInt(18f * scale);

        float iconX = panel.x + 16f * scale;
        float textX = panel.x + 120f * scale;
        float textWidth = panel.width - 134f * scale;
        float top = panel.y + 12f * scale;
        int lineIndex = 0;

        if (showMouse)
        {
            float lineTop = top + lineHeight * lineIndex;
            DrawTexture(
                new Rect(
                    iconX + 23f * scale,
                    lineTop + 13f * scale,
                    30f * scale,
                    30f * scale),
                mouseIcon);
            GUI.Label(new Rect(textX, lineTop, textWidth, lineHeight), "选中元件；旋转盒子", textStyle);
            lineIndex++;
        }

        if (showMove)
        {
            float lineTop = top + lineHeight * lineIndex;
            float keySize = 20f * scale;
            float keyStart = iconX + 7f * scale;
            float keyGap = 1f * scale;
            float secondRow = lineTop + 28f * scale;

            DrawTexture(new Rect(keyStart + keySize + keyGap, lineTop + 7f * scale, keySize, keySize), keyW);
            DrawTexture(new Rect(keyStart, secondRow, keySize, keySize), keyA);
            DrawTexture(new Rect(keyStart + keySize + keyGap, secondRow, keySize, keySize), keyS);
            DrawTexture(new Rect(keyStart + (keySize + keyGap) * 2f, secondRow, keySize, keySize), keyD);
            GUI.Label(new Rect(textX, lineTop, textWidth, lineHeight), "平移元件", textStyle);
            lineIndex++;
        }

        if (showRotate)
        {
            float lineTop = top + lineHeight * lineIndex;
            float arrowSize = 24f * scale;

            DrawTexture(
                new Rect(iconX + 12f * scale, lineTop + 16f * scale, arrowSize, arrowSize),
                arrowLeft);
            DrawTexture(
                new Rect(iconX + 42f * scale, lineTop + 16f * scale, arrowSize, arrowSize),
                arrowRight);
            GUI.Label(new Rect(textX, lineTop, textWidth, lineHeight), "旋转元件", textStyle);
        }
    }

    private static void DrawTexture(Rect rect, Texture2D texture)
    {
        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }
    }

    private static Texture2D LoadIcon(string path)
    {
        var texture = Resources.Load<Texture2D>(path);

        if (texture == null)
        {
            Debug.LogWarning($"[KeyHintUI] Icon not found: Resources/{path}");
        }

        return texture;
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
