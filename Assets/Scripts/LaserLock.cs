using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class LaserLock : MonoBehaviour
{
    private const float BlockPadding = 0.005f;
    private const int CollarSegments = 32;

    [SerializeField] private string[] unlockRequires;
    [SerializeField] private Material bodyMaterial;
    [SerializeField] private Material lampMaterial;
    [SerializeField] private float outerRadius = 0.036f;
    [SerializeField] private float innerRadius = 0.027f;
    [SerializeField] private float height = 0.04f;
    [SerializeField] private float bottomZ = 0.048f;
    [SerializeField] private float sinkDistance = 0.058f;

    private MeshRenderer deviceRenderer;
    private Collider deviceCollider;
    private Transform visualRoot;
    private Transform collar;
    private BoxCollider collarCollider;
    private GameObject lid;
    private Material bodyInstance;
    private readonly List<TopLockLamp> lamps = new List<TopLockLamp>();
    private string[] lampColorIds = new string[0];
    private bool lampsBuilt;
    private bool open;
    private bool warnedNoColors;

    public bool IsBlocking => !open;
    public bool IsOpen => open;

    public void RebaseVisual()
    {
        if (visualRoot == null)
        {
            return;
        }

        Vector3 position = transform.localPosition;
        visualRoot.localPosition = new Vector3(position.x, position.y, 0f);
    }

    public void Configure(string[] colors)
    {
        unlockRequires = colors != null ? (string[])colors.Clone() : new string[0];

        if (lampsBuilt)
        {
            RebuildLamps();
        }
    }

    public void ConfigureStyle(Material body, Material lamp)
    {
        bodyMaterial = body;
        lampMaterial = lamp;

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (visualRoot == null)
        {
            BuildVisuals();
        }
        else if (lampsBuilt)
        {
            RebuildLamps();
        }
    }

    private void Awake()
    {
        deviceRenderer = GetComponent<MeshRenderer>();
        deviceCollider = GetComponent<Collider>();

        if (deviceRenderer != null)
        {
            deviceRenderer.enabled = false;
        }

        if (deviceCollider != null)
        {
            deviceCollider.enabled = false;
        }

        EnlargeToCoverDevice();
        BuildVisuals();
    }

    private void EnlargeToCoverDevice()
    {
        var meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Bounds bounds = meshFilter.sharedMesh.bounds;
        Vector3 scale = transform.localScale;

        // The mesh is not always centered on its transform origin (for example the
        // prism's apex and the receiver's wall pivot). Sampling the actual vertices
        // gives a circle that fully covers the silhouette, instead of only using the
        // largest bounding-box half extent.
        Vector3[] vertices = meshFilter.sharedMesh.vertices;
        float deviceRadius = 0f;

        if (vertices != null && vertices.Length > 0)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                float x = vertex.x * scale.x;
                float y = vertex.y * scale.y;
                float radius = Mathf.Sqrt(x * x + y * y);

                if (radius > deviceRadius)
                {
                    deviceRadius = radius;
                }
            }
        }
        else
        {
            deviceRadius = Mathf.Max(
                Mathf.Abs(scale.x) * bounds.extents.x,
                Mathf.Max(
                    Mathf.Abs(scale.y) * bounds.extents.y,
                    Mathf.Abs(scale.z) * bounds.extents.z));
        }

        outerRadius = Mathf.Max(outerRadius, deviceRadius + 0.004f);
        innerRadius = Mathf.Max(innerRadius, outerRadius - 0.009f);
    }

    private void LateUpdate()
    {
        if (!lampsBuilt)
        {
            BuildLamps();
        }

        if (open)
        {
            return;
        }

        if (lampColorIds.Length == 0)
        {
            if (!warnedNoColors)
            {
                warnedNoColors = true;
                Debug.LogWarning($"[LaserLock] {name} has no unlock colors; the lock will never open.");
            }

            return;
        }

        bool allLit = true;

        for (int i = 0; i < lampColorIds.Length; i++)
        {
            bool lit = LaserReceiver.IsColorLit(lampColorIds[i]);
            lamps[i].SetRequirementMet(lit);

            if (!lit)
            {
                allLit = false;
            }
        }

        if (allLit)
        {
            Open();
        }
    }

    public bool TryIntersect(Vector2 origin, Vector2 direction, out float distance)
    {
        distance = 0f;

        if (open)
        {
            return false;
        }

        Vector2 center = new Vector2(transform.localPosition.x, transform.localPosition.y);
        float radius = outerRadius + BlockPadding;
        Vector2 toCenter = center - origin;

        float projection = Vector2.Dot(toCenter, direction);

        if (projection <= 0f)
        {
            return false;
        }

        float perpendicularSq = toCenter.sqrMagnitude - projection * projection;

        if (perpendicularSq > radius * radius)
        {
            return false;
        }

        float offset = Mathf.Sqrt(radius * radius - perpendicularSq);
        float near = projection - offset;

        if (near <= 0f)
        {
            distance = 0.0001f;
        }
        else
        {
            distance = near;
        }

        return true;
    }

    private void BuildVisuals()
    {
        if (transform.parent == null || bodyMaterial == null)
        {
            return;
        }

        bodyInstance = new Material(bodyMaterial);
        bodyInstance.SetFloat("_Cull", (float)CullMode.Off);

        visualRoot = new GameObject(name + "Lock").transform;
        visualRoot.SetParent(transform.parent, false);

        Vector3 devicePosition = transform.localPosition;
        visualRoot.localPosition = new Vector3(devicePosition.x, devicePosition.y, 0f);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        var collarObject = new GameObject("Collar");
        collar = collarObject.transform;
        collar.SetParent(visualRoot, false);
        collar.localPosition = new Vector3(0f, 0f, bottomZ);

        var collarFilter = collarObject.AddComponent<MeshFilter>();
        collarFilter.sharedMesh = CreateCollarMesh();

        var collarRenderer = collarObject.AddComponent<MeshRenderer>();
        collarRenderer.sharedMaterial = bodyInstance;
        collarRenderer.shadowCastingMode = ShadowCastingMode.Off;
        collarRenderer.receiveShadows = false;

        collarCollider = collarObject.AddComponent<BoxCollider>();
        collarCollider.size = new Vector3(outerRadius * 2f, outerRadius * 2f, height);
        collarCollider.center = new Vector3(0f, 0f, height * 0.5f);

        lid = new GameObject("Lid");
        lid.transform.SetParent(visualRoot, false);
        lid.transform.localPosition = new Vector3(0f, 0f, bottomZ + height);

        var lidPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lidPrimitive.name = "LidDisc";
        lidPrimitive.transform.SetParent(lid.transform, false);
        lidPrimitive.transform.localPosition = Vector3.zero;
        lidPrimitive.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        lidPrimitive.transform.localScale = new Vector3(outerRadius * 2f, 0.003f, outerRadius * 2f);

        var lidRenderer = lidPrimitive.GetComponent<MeshRenderer>();
        lidRenderer.sharedMaterial = bodyInstance;
        lidRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lidRenderer.receiveShadows = false;
        DestroySafe(lidPrimitive.GetComponent<Collider>());
    }

    private void BuildLamps()
    {
        lampsBuilt = true;
        lampColorIds = unlockRequires != null ? (string[])unlockRequires.Clone() : new string[0];

        if (lid == null || lampMaterial == null)
        {
            return;
        }

        // Small locks use colored requirement lamps on the lid instead of the
        // master lock's lamp ring. Each lamp is a shallow spherical cap sitting on
        // the lid so it cannot clip into the lock body.
        int count = lampColorIds.Length;
        float lampBaseRadius = outerRadius * 0.2f;
        float spacing = lampBaseRadius * 2.8f;
        float startX = -(count - 1) * spacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var lampObject = new GameObject("RequirementDot_" + lampColorIds[i]);
            lampObject.transform.SetParent(lid.transform, false);
            lampObject.transform.localPosition = new Vector3(
                startX + i * spacing,
                0f,
                0.003f);
            lampObject.transform.localRotation = Quaternion.identity;
            lampObject.transform.localScale = Vector3.one * lampBaseRadius;

            var filter = lampObject.AddComponent<MeshFilter>();
            filter.sharedMesh = SphericalCapMesh.Unit;

            var renderer = lampObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = lampMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var lamp = lampObject.AddComponent<TopLockLamp>();
            lamp.Configure(lampColorIds[i], DisplayColor(lampColorIds[i]));
            lamps.Add(lamp);
        }
    }

    private void RebuildLamps()
    {
        foreach (TopLockLamp lamp in lamps)
        {
            if (lamp != null)
            {
                DestroySafe(lamp.gameObject);
            }
        }

        lamps.Clear();
        lampsBuilt = false;
        BuildLamps();
    }

    private void Open()
    {
        open = true;

        if (deviceRenderer != null)
        {
            deviceRenderer.enabled = true;
        }

        if (deviceCollider != null)
        {
            deviceCollider.enabled = true;
        }

        if (lid != null)
        {
            lid.SetActive(false);
        }

        if (collarCollider != null)
        {
            collarCollider.enabled = false;
        }

        if (collar != null)
        {
            collar.localPosition = new Vector3(0f, 0f, bottomZ - sinkDistance);
        }
    }

    private Mesh CreateCollarMesh()
    {
        var vertices = new List<Vector3>(CollarSegments * 4);
        var triangles = new List<int>(CollarSegments * 12);

        float step = Mathf.PI * 2f / CollarSegments;

        for (int i = 0; i < CollarSegments; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;

            Vector3 outer0 = new Vector3(Mathf.Cos(a0) * outerRadius, Mathf.Sin(a0) * outerRadius, 0f);
            Vector3 outer1 = new Vector3(Mathf.Cos(a1) * outerRadius, Mathf.Sin(a1) * outerRadius, 0f);
            Vector3 inner0 = new Vector3(Mathf.Cos(a0) * innerRadius, Mathf.Sin(a0) * innerRadius, 0f);
            Vector3 inner1 = new Vector3(Mathf.Cos(a1) * innerRadius, Mathf.Sin(a1) * innerRadius, 0f);

            Vector3 outer0Top = outer0 + Vector3.forward * height;
            Vector3 outer1Top = outer1 + Vector3.forward * height;
            Vector3 inner0Top = inner0 + Vector3.forward * height;
            Vector3 inner1Top = inner1 + Vector3.forward * height;

            AddQuad(vertices, triangles, outer0, outer1, outer1Top, outer0Top);
            AddQuad(vertices, triangles, inner1, inner0, inner0Top, inner1Top);
            AddQuad(vertices, triangles, outer0Top, outer1Top, inner1Top, inner0Top);
            AddQuad(vertices, triangles, inner0, inner1, outer1, outer0);
        }

        var mesh = new Mesh { name = "LockCollar" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private static Color DisplayColor(string colorId)
    {
        LightColor color = Palette.Get(colorId);
        float max = Mathf.Max(color.R, Mathf.Max(color.G, color.B));

        if (max <= 1e-6f)
        {
            return Color.white;
        }

        return new Color(color.R / max, color.G / max, color.B / max, 1f);
    }



    private void OnDestroy()
    {
        DestroySafe(visualRoot != null ? visualRoot.gameObject : null);
        DestroySafe(bodyInstance);
    }

    private static void DestroySafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
