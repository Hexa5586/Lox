using UnityEngine;

[DisallowMultipleComponent]
public class TopLockLamp : MonoBehaviour
{
    private const float GlowShellScale = 1.5f;
    private const float GlowAlpha = 0.3f;

    private static bool goalLampsOff;

    [SerializeField] private string colorId;
    [SerializeField] private Color litColor = Color.white;
    [SerializeField] private Color offColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    [SerializeField] private bool goalLamp;

    private static Material glowMaterial;
    private static bool warnedNoGlow;

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Transform glowTransform;
    private Renderer glowRenderer;
    private MaterialPropertyBlock glowBlock;
    private bool isLit;

    public string ColorId => colorId;
    public bool IsLit => isLit;

    public void Configure(string id, Color color)
    {
        colorId = id;
        litColor = color;

        if (targetRenderer != null)
        {
            ApplyColor();
        }
    }

    public void MarkAsGoalLamp()
    {
        goalLamp = true;
        isLit = !goalLampsOff;

        if (targetRenderer != null)
        {
            ApplyColor();
        }
    }

    public static void SetGoalLampsOff(bool off)
    {
        goalLampsOff = off;

        if (!off)
        {
            return;
        }

        foreach (TopLockLamp lamp in FindObjectsOfType<TopLockLamp>())
        {
            if (lamp != null && lamp.goalLamp)
            {
                lamp.SetLit(false);
            }
        }
    }

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        EnsureGlow();
        ApplyColor();
    }

    private void EnsureGlow()
    {
        if (glowTransform != null)
        {
            return;
        }

        if (glowMaterial == null)
        {
            var assets = Resources.Load<LevelAssets>("LevelAssets");
            glowMaterial = assets == null ? null : assets.FindMaterial("LampGlow");
        }

        if (glowMaterial == null)
        {
            if (!warnedNoGlow)
            {
                warnedNoGlow = true;
                Debug.LogWarning("[TopLockLamp] LampGlow material not found in LevelAssets.");
            }

            return;
        }

        var lampFilter = GetComponent<MeshFilter>();

        if (lampFilter == null || lampFilter.sharedMesh == null)
        {
            return;
        }

        var glowObject = new GameObject("Glow");
        glowTransform = glowObject.transform;
        glowTransform.SetParent(transform, false);
        glowTransform.localPosition = Vector3.zero;
        glowTransform.localRotation = Quaternion.identity;
        glowTransform.localScale = Vector3.one * GlowShellScale;

        var filter = glowObject.AddComponent<MeshFilter>();
        filter.sharedMesh = lampFilter.sharedMesh;

        glowRenderer = glowObject.AddComponent<MeshRenderer>();
        glowRenderer.sharedMaterial = glowMaterial;
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowRenderer.enabled = false;
        glowBlock = new MaterialPropertyBlock();
    }

    public void SetLit(bool lit)
    {
        if (goalLamp && goalLampsOff)
        {
            lit = false;
        }

        isLit = lit;

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        ApplyColor();
    }

    /// <summary>
    /// Requirement lamps show what is still missing: on while the receiver is
    /// inactive, off once it is active.
    /// </summary>
    public void SetRequirementMet(bool met)
    {
        SetLit(!met);
    }

    private void ApplyColor()
    {
        if (targetRenderer == null || propertyBlock == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", isLit ? litColor : offColor);
        targetRenderer.SetPropertyBlock(propertyBlock);

        if (glowRenderer == null)
        {
            return;
        }

        if (!isLit)
        {
            glowRenderer.enabled = false;
            return;
        }

        if (glowBlock == null)
        {
            glowBlock = new MaterialPropertyBlock();
        }

        glowRenderer.enabled = true;
        glowRenderer.GetPropertyBlock(glowBlock);
        Color glowColor = litColor;
        glowColor.a = GlowAlpha;
        glowBlock.SetColor("_Color", glowColor);
        glowRenderer.SetPropertyBlock(glowBlock);
    }
}

