using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class AcrylicShellThickness : MonoBehaviour
{
    [SerializeField] private float innerScale = 0.95f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToShell();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToShell();
    }

    private static void AttachToShell()
    {
        GameObject shell = GameObject.Find("Shell");

        if (shell == null || shell.GetComponent<AcrylicShellThickness>() != null)
        {
            return;
        }

        shell.AddComponent<AcrylicShellThickness>();
    }

    private void Awake()
    {
        BuildInnerShell();
    }

    private void BuildInnerShell()
    {
        if (transform.Find("InnerShell") != null)
        {
            return;
        }

        var outerFilter = GetComponent<MeshFilter>();
        var outerRenderer = GetComponent<MeshRenderer>();

        if (outerFilter == null || outerRenderer == null
            || outerFilter.sharedMesh == null || outerRenderer.sharedMaterial == null)
        {
            return;
        }

        var innerObject = new GameObject("InnerShell");
        innerObject.transform.SetParent(transform, false);
        innerObject.transform.localPosition = Vector3.zero;
        innerObject.transform.localRotation = Quaternion.identity;
        innerObject.transform.localScale = Vector3.one * Mathf.Clamp(innerScale, 0.8f, 0.99f);

        var innerFilter = innerObject.AddComponent<MeshFilter>();
        innerFilter.sharedMesh = outerFilter.sharedMesh;

        var innerRenderer = innerObject.AddComponent<MeshRenderer>();
        innerRenderer.sharedMaterial = outerRenderer.sharedMaterial;
        innerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        innerRenderer.receiveShadows = false;
        innerRenderer.lightProbeUsage = outerRenderer.lightProbeUsage;
        innerRenderer.reflectionProbeUsage = outerRenderer.reflectionProbeUsage;
    }
}
