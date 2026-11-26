using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class AcrylicShellAluminumEdging : MonoBehaviour
{
    // The boundary walls are 0.1 in BoxRoot space; the shell itself is scaled
    // 1.1, so the matching total band in shell-mesh space is 0.1 / 1.1.
    [SerializeField] private float bandWidth = 0.0909f;
    [SerializeField] private float sheetThickness = 0.012f;
    [SerializeField] private float edgeRadius = 0.018f;
    [SerializeField] private float innerCornerRadius = 0.03f;

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

        if (shell == null || shell.GetComponent<AcrylicShellAluminumEdging>() != null)
        {
            return;
        }

        shell.AddComponent<AcrylicShellAluminumEdging>();
    }

    private void Awake()
    {
        BuildEdging();
    }

    private void BuildEdging()
    {
        if (transform.Find("AluminumEdging") != null)
        {
            return;
        }

        var assets = Resources.Load<LevelAssets>("LevelAssets");
        Material aluminumMaterial = assets == null ? null : assets.FindMaterial("FrostedAluminumEdge");

        if (aluminumMaterial == null)
        {
            Debug.LogWarning("[AcrylicShellAluminumEdging] FrostedAluminumEdge material not found in LevelAssets.");
            return;
        }

        var edgingObject = new GameObject("AluminumEdging");
        edgingObject.transform.SetParent(transform, false);
        edgingObject.transform.localPosition = Vector3.zero;
        edgingObject.transform.localRotation = Quaternion.identity;
        edgingObject.transform.localScale = Vector3.one;

        var filter = edgingObject.AddComponent<MeshFilter>();
        filter.sharedMesh = ShellAluminumEdgingMesh.Create(
            0.5f, bandWidth, sheetThickness, edgeRadius, innerCornerRadius);

        var renderer = edgingObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = aluminumMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }
}





