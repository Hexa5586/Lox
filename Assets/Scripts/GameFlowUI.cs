using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameFlowUI : MonoBehaviour
{
    private static readonly Color TimerColor = new Color(0x99 / 255f, 0x99 / 255f, 0x99 / 255f, 1f);
    private static readonly Color TouchColor = new Color(0xBB / 255f, 0xBB / 255f, 0xBB / 255f, 1f);
    private static readonly Color DialogTitleColor = new Color(0.93f, 0.93f, 0.93f, 1f);
    private static readonly KeyCode[] CheatSequence =
    {
        KeyCode.UpArrow,
        KeyCode.UpArrow,
        KeyCode.DownArrow,
        KeyCode.DownArrow,
        KeyCode.LeftArrow,
        KeyCode.RightArrow,
        KeyCode.LeftArrow,
        KeyCode.RightArrow,
        KeyCode.B,
        KeyCode.A,
        KeyCode.B,
        KeyCode.A
    };

    private Font uiFont;
    private Texture2D buttonNormal;
    private Texture2D buttonHover;
    private Texture2D buttonActive;
    private Texture2D modalBackground;
    private Texture2D overlay;
    private Texture2D mouseIcon;
    private Texture2D keyIcon;
    private Texture2D restartIcon;

    private GUIStyle timerStyle;
    private GUIStyle restartButtonStyle;
    private GUIStyle hintStyle;
    private GUIStyle touchStyle;
    private GUIStyle dialogTitleStyle;
    private GUIStyle dialogTimeStyle;
    private GUIStyle dialogButtonStyle;

    private DeviceConfigLoader loader;
    private Transform topLock;

    private bool waitingForStart = true;
    private bool timerRunning;
    private bool solved;
    private bool dialogOpen;
    private bool restartPending;
    private int cheatStep;
    private float elapsed;
    private float finalTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<GameFlowUI>() != null)
        {
            return;
        }

        new GameObject("GameFlowUI").AddComponent<GameFlowUI>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        buttonNormal = CreateSolidTexture(new Color(0.13f, 0.13f, 0.13f, 0.88f));
        buttonHover = CreateSolidTexture(new Color(0.22f, 0.22f, 0.22f, 0.95f));
        buttonActive = CreateSolidTexture(new Color(0.30f, 0.30f, 0.30f, 1f));
        modalBackground = CreateSolidTexture(new Color(0.08f, 0.08f, 0.08f, 0.96f));
        overlay = CreateSolidTexture(new Color(0f, 0f, 0f, 0.55f));
        mouseIcon = LoadIcon("Icons/MouseLeft");
        keyIcon = LoadIcon("Icons/W");
        restartIcon = LoadIcon("Icons/Restart");

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "Arial" },
            28);

        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        timerStyle = CreateLabelStyle(TextAnchor.MiddleRight, TimerColor);
        hintStyle = CreateLabelStyle(TextAnchor.MiddleCenter, TimerColor);
        touchStyle = CreateButtonStyle(TextAnchor.MiddleCenter, TouchColor);
        touchStyle.normal.background = null;
        touchStyle.hover.background = null;
        touchStyle.active.background = null;
        touchStyle.focused.background = null;
        dialogTitleStyle = CreateLabelStyle(TextAnchor.MiddleCenter, DialogTitleColor);
        dialogTimeStyle = CreateLabelStyle(TextAnchor.MiddleCenter, TimerColor);
        restartButtonStyle = CreateButtonStyle(TextAnchor.MiddleCenter, TimerColor);
        restartButtonStyle.normal.background = null;
        restartButtonStyle.hover.background = null;
        restartButtonStyle.active.background = null;
        restartButtonStyle.focused.background = null;
        dialogButtonStyle = CreateButtonStyle(TextAnchor.MiddleCenter, TimerColor);

        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetRun();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetRun();
    }

    private void ResetRun()
    {
        loader = FindObjectOfType<DeviceConfigLoader>();
        topLock = null;
        waitingForStart = true;
        timerRunning = false;
        solved = false;
        dialogOpen = false;
        restartPending = false;
        cheatStep = 0;
        elapsed = 0f;
        finalTime = 0f;
        TopLockLamp.SetGoalLampsOff(false);
    }

    private void Update()
    {
        if (restartPending)
        {
            restartPending = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        CheckCheatSequence();

        if (solved)
        {
            return;
        }

        if (!timerRunning && waitingForStart && Input.anyKeyDown)
        {
            waitingForStart = false;
            timerRunning = true;
            elapsed = 0f;
        }

        if (timerRunning)
        {
            elapsed += Time.unscaledDeltaTime;
        }

        if (IsPuzzleSolved())
        {
            solved = true;
            timerRunning = false;
            waitingForStart = false;
            finalTime = elapsed;
        }
    }

    private void CheckCheatSequence()
    {
        if (!Input.anyKeyDown)
        {
            return;
        }

        KeyCode expected = CheatSequence[cheatStep];

        if (Input.GetKeyDown(expected))
        {
            if (AdvanceCheatSequence(expected))
            {
                ActivateCheat();
            }

            return;
        }

        if (Input.GetKeyDown(CheatSequence[0]))
        {
            if (AdvanceCheatSequence(CheatSequence[0]))
            {
                ActivateCheat();
            }

            return;
        }

        cheatStep = 0;
    }

    private bool AdvanceCheatSequence(KeyCode key)
    {
        if (key == CheatSequence[cheatStep])
        {
            cheatStep++;
        }
        else
        {
            cheatStep = key == CheatSequence[0] ? 1 : 0;
        }

        if (cheatStep < CheatSequence.Length)
        {
            return false;
        }

        cheatStep = 0;
        return true;
    }

    private void ActivateCheat()
    {
        TopLockLamp.SetGoalLampsOff(true);
        solved = true;
        waitingForStart = false;
        timerRunning = false;
        finalTime = elapsed;
        Debug.Log("[GameFlowUI] Cheat code accepted: goal lamps extinguished and TOUCH enabled.");
    }

    private bool IsPuzzleSolved()
    {
        if (loader == null)
        {
            loader = FindObjectOfType<DeviceConfigLoader>();
        }

        if (loader == null || !loader.IsBuilt)
        {
            return false;
        }

        TopLockLamp[] lamps = loader.GoalLamps;

        if (lamps == null || lamps.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < lamps.Length; i++)
        {
            TopLockLamp lamp = lamps[i];

            if (lamp == null || lamp.IsLit)
            {
                return false;
            }
        }

        return true;
    }

    private void OnGUI()
    {
        GUI.depth = -110;

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);

        DrawTopBar(scale);

        if (solved && !dialogOpen)
        {
            DrawTouchText(scale);
        }

        if (dialogOpen)
        {
            DrawDialog(scale);
        }
    }

    private void DrawTopBar(float scale)
    {
        float margin = 24f * scale;
        float barHeight = 44f * scale;
        float timerFontSize = 28f * scale;
        float timerWidth = 320f * scale;

        timerStyle.fontSize = Mathf.RoundToInt(timerFontSize);
        var timerRect = new Rect(
            Screen.width - margin - timerWidth,
            margin,
            timerWidth,
            barHeight);
        GUI.Label(timerRect, FormatTime(CurrentTime), timerStyle);

        restartButtonStyle.fontSize = Mathf.RoundToInt(20f * scale);
        var restartRect = new Rect(margin, margin, barHeight, barHeight);

        if (GUI.Button(restartRect, GUIContent.none, restartButtonStyle))
        {
            RequestRestart();
        }

        float restartIconSize = 34f * scale;
        DrawTexture(
            new Rect(
                restartRect.center.x - restartIconSize * 0.5f,
                restartRect.center.y - restartIconSize * 0.5f,
                restartIconSize,
                restartIconSize),
            restartIcon);

        if (!waitingForStart)
        {
            return;
        }

        float iconSize = timerFontSize;
        float iconY = margin + (barHeight - iconSize) * 0.5f;
        float iconX = restartRect.xMax + 18f * scale;
        float slashWidth = 15f * scale;

        hintStyle.fontSize = Mathf.RoundToInt(18f * scale);
        DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), mouseIcon);
        GUI.Label(
            new Rect(iconX + iconSize, margin, slashWidth, barHeight),
            "/",
            hintStyle);
        DrawTexture(
            new Rect(iconX + iconSize + slashWidth, iconY, iconSize, iconSize),
            keyIcon);
    }

    private void DrawTouchText(float scale)
    {
        Camera camera = Camera.main;

        if (camera == null || !TryGetTopLock(out Transform lockTransform))
        {
            return;
        }

        Vector3 toCamera = camera.transform.position - lockTransform.position;

        if (Vector3.Dot(lockTransform.forward, toCamera.normalized) < 0.15f)
        {
            return;
        }

        Vector3 worldPoint = lockTransform.position + lockTransform.forward * 0.025f;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);

        if (screenPoint.z <= 0f)
        {
            return;
        }

        float width = 260f * scale;
        float height = 64f * scale;
        var rect = new Rect(
            screenPoint.x - width * 0.5f,
            Screen.height - screenPoint.y - height * 0.5f,
            width,
            height);

        touchStyle.fontSize = Mathf.RoundToInt(38f * scale);

        if (GUI.Button(rect, "TOUCH", touchStyle))
        {
            dialogOpen = true;
        }
    }

    private bool TryGetTopLock(out Transform lockTransform)
    {
        if (topLock == null)
        {
            GameObject topLockObject = GameObject.Find("TopLock");

            if (topLockObject != null)
            {
                topLock = topLockObject.transform;
            }
        }

        lockTransform = topLock;
        return lockTransform != null;
    }

    private void DrawDialog(float scale)
    {
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            overlay);

        float width = Mathf.Min(560f * scale, Screen.width - 80f * scale);
        float height = Mathf.Min(320f * scale, Screen.height - 80f * scale);
        var panel = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUI.DrawTexture(panel, modalBackground);

        dialogTitleStyle.fontSize = Mathf.RoundToInt(42f * scale);
        GUI.Label(
            new Rect(panel.x + 20f * scale, panel.y + 42f * scale, panel.width - 40f * scale, 64f * scale),
            "你赢了！",
            dialogTitleStyle);

        dialogTimeStyle.fontSize = Mathf.RoundToInt(28f * scale);
        GUI.Label(
            new Rect(panel.x + 20f * scale, panel.y + 130f * scale, panel.width - 40f * scale, 48f * scale),
            "时间  " + FormatTime(finalTime),
            dialogTimeStyle);

        dialogButtonStyle.fontSize = Mathf.RoundToInt(22f * scale);
        var confirmRect = new Rect(
            panel.x + (panel.width - 180f * scale) * 0.5f,
            panel.yMax - 78f * scale,
            180f * scale,
            48f * scale);

        if (GUI.Button(confirmRect, "确定", dialogButtonStyle))
        {
            RequestRestart();
        }
    }

    private void RequestRestart()
    {
        if (restartPending)
        {
            return;
        }

        restartPending = true;
        waitingForStart = true;
        timerRunning = false;
        solved = false;
        dialogOpen = false;
        elapsed = 0f;
        finalTime = 0f;
    }

    private float CurrentTime => solved ? finalTime : elapsed;

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f)
        {
            seconds = 0f;
        }

        int totalHundredths = Mathf.FloorToInt(seconds * 100f);
        int minutes = totalHundredths / 6000;
        int wholeSeconds = totalHundredths / 100 % 60;
        int hundredths = totalHundredths % 100;
        return $"{minutes:00}:{wholeSeconds:00}.{hundredths:00}";
    }

    private GUIStyle CreateLabelStyle(TextAnchor alignment, Color color)
    {
        var style = new GUIStyle
        {
            font = uiFont,
            fontStyle = FontStyle.Bold,
            alignment = alignment,
            wordWrap = false,
            richText = false
        };
        style.normal.textColor = color;
        return style;
    }

    private GUIStyle CreateButtonStyle(TextAnchor alignment, Color color)
    {
        var style = new GUIStyle
        {
            font = uiFont,
            fontStyle = FontStyle.Bold,
            alignment = alignment,
            wordWrap = false,
            richText = false,
            padding = new RectOffset(8, 8, 4, 4)
        };
        style.normal.background = buttonNormal;
        style.hover.background = buttonHover;
        style.active.background = buttonActive;
        style.focused.background = buttonNormal;
        style.normal.textColor = color;
        style.hover.textColor = Color.Lerp(color, Color.white, 0.35f);
        style.active.textColor = Color.white;
        style.focused.textColor = color;
        return style;
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
            Debug.LogWarning($"[GameFlowUI] Icon not found: Resources/{path}");
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
