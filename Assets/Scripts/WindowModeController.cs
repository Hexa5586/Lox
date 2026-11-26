using UnityEngine;

[DisallowMultipleComponent]
public class WindowModeController : MonoBehaviour
{
    private const int WindowedWidth = 1600;
    private const int WindowedHeight = 900;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<WindowModeController>() != null)
        {
            return;
        }

        var controllerObject = new GameObject("WindowModeController");
        DontDestroyOnLoad(controllerObject);
        controllerObject.AddComponent<WindowModeController>();
    }

    private void Update()
    {
        bool toggleRequested = Input.GetKeyDown(KeyCode.F11)
            || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                && Input.GetKeyDown(KeyCode.Return));

        if (toggleRequested)
        {
            ToggleFullscreen();
        }
    }

    private static void ToggleFullscreen()
    {
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            Display display = Display.main;
            Screen.SetResolution(
                display.systemWidth,
                display.systemHeight,
                FullScreenMode.FullScreenWindow);
            return;
        }

        Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
    }
}
