using UnityEngine;

[DisallowMultipleComponent]
public class DeviceSelectable : MonoBehaviour
{
    [SerializeField] private string deviceId;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private float moveSpeed = 0.25f;
    [SerializeField] private float rotateSpeed = 45f;

    [Header("Runtime config (from level JSON)")]
    [SerializeField] private bool canMove;
    [SerializeField] private bool freeMove;
    [SerializeField] private float moveMin = -0.43f;
    [SerializeField] private float moveMax = 0.43f;
    [SerializeField] private bool canRotate;
    [SerializeField] private float rotationMin = -360f;
    [SerializeField] private float rotationMax = 360f;

    private GameObject outlineObject;
    private bool wallAxisX;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private float currentYaw;

    public string DeviceId => deviceId;
    public bool CanMove => canMove;
    public bool FreeMove => freeMove;
    public bool CanRotate => canRotate;
    public float MoveSpeed => moveSpeed;
    public float RotateSpeed => rotateSpeed;

    public void Configure(string id, Material material)
    {
        deviceId = id;
        glowMaterial = material;

        if (isActiveAndEnabled && outlineObject == null)
        {
            CreateOutline();
        }
    }

    public void ApplyPlacement(Vector3 localPosition, float rotationDegrees)
    {
        ApplyPlacement(localPosition, Quaternion.Euler(0f, 0f, rotationDegrees));
    }

    public void ApplyPlacement(Vector3 localPosition, Quaternion localRotation)
    {
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = localRotation;
        currentYaw = 0f;
        wallAxisX = Mathf.Abs(initialLocalPosition.y) > Mathf.Abs(initialLocalPosition.x);
    }

    public void ApplyConfig(DeviceConfigEntry config)
    {
        if (config == null)
        {
            return;
        }

        canMove = config.movable;
        freeMove = config.freeMove;
        moveMin = Mathf.Min(config.moveMin, config.moveMax);
        moveMax = Mathf.Max(config.moveMin, config.moveMax);

        canRotate = config.rotatable;
        rotationMin = Mathf.Min(config.rotationMin, config.rotationMax);
        rotationMax = Mathf.Max(config.rotationMin, config.rotationMax);
    }

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        currentYaw = 0f;
        wallAxisX = Mathf.Abs(initialLocalPosition.y) > Mathf.Abs(initialLocalPosition.x);
        CreateOutline();
    }

    private void CreateOutline()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || glowMaterial == null)
        {
            return;
        }

        // Same mesh and transform as the device: the material renders a stencil
        // mask first, then a screen-space expanded silhouette around it.
        outlineObject = new GameObject("SelectionOutline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one;

        var outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = meshFilter.sharedMesh;

        var outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = glowMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;

        outlineObject.SetActive(false);
    }

    public void SetSelected(bool value)
    {
        if (outlineObject != null)
        {
            outlineObject.SetActive(value);
        }
    }

    public void MoveAlongWall(Vector2 screenInput, float deltaTime)
    {
        if (!canMove || deltaTime <= 0f || screenInput.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Camera camera = Camera.main;

        if (camera == null || transform.parent == null)
        {
            return;
        }

        if (freeMove)
        {
            MoveFreely(screenInput, deltaTime, camera);
            return;
        }

        // Most wall-mounted devices slide along one face-local axis. Project that axis onto
        // the camera's right/up plane so the keys follow what the player sees, no matter how
        // the box is oriented on screen.
        Vector3 worldAxis = transform.parent.TransformDirection(wallAxisX ? Vector3.right : Vector3.up);
        Vector2 screenAxis = new Vector2(
            Vector3.Dot(worldAxis, camera.transform.right),
            Vector3.Dot(worldAxis, camera.transform.up));

        // Edge-on face: the axis points at the camera, so a slide would not be visible.
        if (screenAxis.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float amount = Mathf.Clamp(Vector2.Dot(screenInput, screenAxis.normalized), -1f, 1f);
        float localDelta = amount * moveSpeed * deltaTime;

        if (Mathf.Abs(localDelta) < 0.000001f)
        {
            return;
        }

        Vector3 position = transform.localPosition;

        if (wallAxisX)
        {
            position.x = Mathf.Clamp(position.x + localDelta, moveMin, moveMax);
        }
        else
        {
            position.y = Mathf.Clamp(position.y + localDelta, moveMin, moveMax);
        }

        transform.localPosition = position;
    }

    private void MoveFreely(Vector2 screenInput, float deltaTime, Camera camera)
    {
        Vector2 input = screenInput.sqrMagnitude > 1f ? screenInput.normalized : screenInput;
        Vector3 worldX = transform.parent.TransformDirection(Vector3.right);
        Vector3 worldY = transform.parent.TransformDirection(Vector3.up);
        var screenX = new Vector2(
            Vector3.Dot(worldX, camera.transform.right),
            Vector3.Dot(worldX, camera.transform.up));
        var screenY = new Vector2(
            Vector3.Dot(worldY, camera.transform.right),
            Vector3.Dot(worldY, camera.transform.up));

        Vector3 position = transform.localPosition;

        if (screenX.sqrMagnitude > 0.0001f)
        {
            float amount = Mathf.Clamp(Vector2.Dot(input, screenX.normalized), -1f, 1f);
            position.x = Mathf.Clamp(position.x + amount * moveSpeed * deltaTime, moveMin, moveMax);
        }

        if (screenY.sqrMagnitude > 0.0001f)
        {
            float amount = Mathf.Clamp(Vector2.Dot(input, screenY.normalized), -1f, 1f);
            position.y = Mathf.Clamp(position.y + amount * moveSpeed * deltaTime, moveMin, moveMax);
        }

        transform.localPosition = position;
    }

    public void Rotate(float screenDegrees)
    {
        if (!canRotate || Mathf.Abs(screenDegrees) < 0.000001f)
        {
            return;
        }

        Camera camera = Camera.main;
        float localSign = 1f;

        if (camera != null && transform.parent != null)
        {
            // Positive local yaw turns clockwise on screen while the outside of the face
            // faces the camera, and counter-clockwise when the camera sees it from behind.
            // Flipping the sign keeps the arrows aligned with the player's view.
            float facing = Vector3.Dot(transform.parent.forward, camera.transform.forward);

            if (facing < 0f)
            {
                localSign = -1f;
            }
        }

        currentYaw = Mathf.Clamp(
            currentYaw + screenDegrees * localSign,
            rotationMin,
            rotationMax);

        transform.localRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, currentYaw);
    }
}
