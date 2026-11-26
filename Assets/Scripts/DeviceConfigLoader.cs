using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class LevelPosition
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class LevelSize
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class DeviceConfigEntry
{
    public string id;
    public string type;
    public string face;
    public string color;
    public string lamp;
    public LevelPosition position;
    public LevelPosition aim;
    public LevelPosition mountPivot;
    public float rotation;
    public LevelSize size;
    public bool castShadows;
    public bool movable;
    public bool freeMove;
    public float moveMin;
    public float moveMax;
    public bool rotatable;
    public float rotationMin;
    public float rotationMax;
    public string[] unlockRequires;
    public float focalLength;
    public float aperture;
    public float radius;
    public float thickness;
    public float iorR;
    public float iorG;
    public float iorB;
}

[Serializable]
public class WallConfigEntry
{
    public string id;
    public string name;
    public string face;
    public LevelPosition position;
    public float rotation;
    public LevelSize size;
    public bool castShadows;
}

[Serializable]
public class MirrorConfigEntry
{
    public string id;
    public string name;
    public string face;
    public LevelPosition position;
    public float rotation;
    public LevelSize size;
    public bool castShadows;
}

[Serializable]
public class GoalConfigEntry
{
    public string face;
    public LevelPosition position;
    public float baseRadius;
    public float baseHeight;
    public float baseZ;
    public float ringRadius;
    public string[] lamps;
    public string centerLamp;
}

[Serializable]
public class DeviceConfigFile
{
    public int version;
    public DeviceConfigEntry[] devices;
    public WallConfigEntry[] walls;
    public MirrorConfigEntry[] mirrors;
    public GoalConfigEntry goal;
}

[DisallowMultipleComponent]
public class DeviceConfigLoader : MonoBehaviour
{
    private const float DefaultDeviceZ = 0.05f;
    private const float FaceBound = 0.43f;
    private const float WallMountThreshold = 0.06f;

    private static readonly string[] FaceNames =
    {
        "front", "back", "left", "right", "top", "bottom"
    };

    [SerializeField] private string fileName = "level_01.json";
    [SerializeField] private LevelAssets levelAssets;

    private readonly Dictionary<string, Transform> faceRoots = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Transform> puzzleRoots = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private readonly Dictionary<string, TopLockLamp> lamps = new Dictionary<string, TopLockLamp>();

    private Material selectionMaterial;
    private Material prismMaterial;
    private bool built;

    public bool IsBuilt => built;
    public int DeviceCount { get; private set; }
    public int WallCount { get; private set; }
    public int MirrorCount { get; private set; }
    public TopLockLamp[] GoalLamps { get; private set; } = new TopLockLamp[0];

    private void Start()
    {
        BuildLevel();
    }

    public void BuildLevel()
    {
        if (built)
        {
            return;
        }

        built = true;

        string path = Path.Combine(Application.streamingAssetsPath, "Levels", fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[DeviceConfigLoader] Config file not found: {path}");
            return;
        }

        DeviceConfigFile config;

        try
        {
            config = JsonUtility.FromJson<DeviceConfigFile>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DeviceConfigLoader] Failed to read {path}: {exception.Message}");
            return;
        }

        if (config == null || config.devices == null)
        {
            Debug.LogError($"[DeviceConfigLoader] Invalid config: {path}");
            return;
        }

        if (config.version != 1)
        {
            Debug.LogWarning($"[DeviceConfigLoader] Unexpected config version: {config.version}");
        }

        if (!ResolveAssets())
        {
            return;
        }

        if (!CacheFaceRoots())
        {
            return;
        }

        ClearGeneratedGeometry();
        BuildGoal(config.goal);
        BuildBeams(config.devices);
        BuildWalls(config.walls);
        BuildMirrors(config.mirrors);
        BuildStatelessDevices(config.devices);
        BuildReceivers(config.devices);
        BuildEmitters(config.devices);

        Debug.Log(
            $"[DeviceConfigLoader] Built level from {fileName}: " +
            $"{DeviceCount} devices, {WallCount} walls, {MirrorCount} mirrors, {lamps.Count} lamps.");
    }

    private bool ResolveAssets()
    {
        if (levelAssets == null)
        {
            levelAssets = Resources.Load<LevelAssets>("LevelAssets");
        }

        if (levelAssets == null)
        {
            Debug.LogError("[DeviceConfigLoader] LevelAssets asset not found in Resources.");
            return false;
        }

        materials.Clear();

        if (levelAssets.materials != null)
        {
            foreach (Material material in levelAssets.materials)
            {
                if (material != null && !materials.ContainsKey(material.name))
                {
                    materials.Add(material.name, material);
                }
            }
        }

        selectionMaterial = FindMaterial("SelectionGlow");

        if (selectionMaterial == null)
        {
            Debug.LogError("[DeviceConfigLoader] SelectionGlow material is missing.");
        }

        Material lensMaterial = FindMaterial("Lens");

        if (lensMaterial != null)
        {
            // The prism is a thick closed solid, so the shared lens material's
            // alpha accumulates over several faces and reads too gray. Keep the
            // same acrylic shader and tint, but make the solid clearer and give
            // its edges a slightly stronger glass highlight.
            prismMaterial = new Material(lensMaterial)
            {
                name = "Prism_Acrylic"
            };
            prismMaterial.SetColor("_Color", new Color(0.68f, 0.90f, 1f, 1f));
            prismMaterial.SetFloat("_BaseAlpha", 0.06f);
            prismMaterial.SetFloat("_EdgeAlpha", 0.16f);
            prismMaterial.SetFloat("_FresnelAlpha", 0.28f);
        }
        else
        {
            Debug.LogWarning("[DeviceConfigLoader] Lens material is missing; prism will use the fallback.");
            prismMaterial = null;
        }

        return true;
    }

    private bool CacheFaceRoots()
    {
        GameObject boxRoot = GameObject.Find("BoxRoot");
        Transform facesRoot = boxRoot != null ? boxRoot.transform.Find("Faces") : null;

        if (facesRoot == null)
        {
            Debug.LogError("[DeviceConfigLoader] BoxRoot/Faces was not found in the scene.");
            return false;
        }

        faceRoots.Clear();
        puzzleRoots.Clear();

        foreach (string faceName in FaceNames)
        {
            Transform face = facesRoot.Find(faceName);

            if (face == null)
            {
                Debug.LogError($"[DeviceConfigLoader] Face '{faceName}' was not found.");
                return false;
            }

            faceRoots[faceName] = face;
            puzzleRoots[faceName] = EnsurePuzzle(face);
        }

        return true;
    }

    private static Transform EnsurePuzzle(Transform face)
    {
        Transform puzzle = face.Find("Puzzle");

        if (puzzle != null)
        {
            return puzzle;
        }

        var puzzleObject = new GameObject("Puzzle");
        puzzle = puzzleObject.transform;
        puzzle.SetParent(face, false);
        puzzle.localPosition = Vector3.zero;
        puzzle.localRotation = Quaternion.identity;
        puzzle.localScale = Vector3.one;
        return puzzle;
    }

    private void ClearGeneratedGeometry()
    {
        foreach (Transform face in faceRoots.Values)
        {
            Transform puzzle = face.Find("Puzzle");

            if (puzzle != null)
            {
                for (int i = puzzle.childCount - 1; i >= 0; i--)
                {
                    DestroyStale(puzzle.GetChild(i).gameObject);
                }
            }

            Transform topLock = face.Find("TopLock");

            if (topLock != null)
            {
                DestroyStale(topLock.gameObject);
            }
        }
    }

    private static void DestroyStale(GameObject target)
    {
        // Move the old level geometry out of the face hierarchy before deactivating it.
        // The loader builds replacements in the same frame; inactive stale objects must
        // not be found by the new emitters when they collect their optics.
        target.transform.SetParent(null, false);
        target.SetActive(false);
        target.name = "Stale_" + target.name;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void BuildGoal(GoalConfigEntry goal)
    {
        lamps.Clear();
        GoalLamps = new TopLockLamp[0];

        if (goal == null || string.IsNullOrEmpty(goal.face))
        {
            Debug.LogWarning("[DeviceConfigLoader] No goal geometry configured.");
            return;
        }

        Transform face = GetFace(goal.face);

        if (face == null)
        {
            return;
        }

        var rootObject = new GameObject("TopLock");
        Transform root = rootObject.transform;
        root.SetParent(face, false);
        Vector3 rootPosition = PositionOf(goal.position, 0f);
        root.localPosition = new Vector3(rootPosition.x, rootPosition.y, 0f);
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        float baseRadius = goal.baseRadius > 0f ? goal.baseRadius : 0.225f;
        float baseHeight = goal.baseHeight > 0f ? goal.baseHeight : 0.02f;
        float baseZ = goal.baseZ;

        GameObject baseObject = CreateMeshObject(
            "Base",
            root,
            BuiltinMesh("Cylinder"),
            FindMaterial("LockBase"),
            new Vector3(0f, 0f, baseZ),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(baseRadius, baseHeight * 0.5f, baseRadius),
            false);
        baseObject.SetActive(true);

        string[] lampIds = goal.lamps != null && goal.lamps.Length > 0
            ? goal.lamps
            : new[] { "R", "G", "B", "Y", "C" };
        float ringRadius = goal.ringRadius > 0f ? goal.ringRadius : 0.165f;
        float lampBaseRadius = baseRadius * 0.2f;
        float lampBaseZ = baseZ + baseHeight * 0.5f;
        Material lampMaterial = FindMaterial("Lamp");

        for (int i = 0; i < lampIds.Length; i++)
        {
            string colorId = lampIds[i];
            float angle = Mathf.PI * 0.5f + Mathf.PI * 2f * i / lampIds.Length;

            GameObject lampObject = CreateMeshObject(
                "Lamp_" + colorId,
                root,
                SphericalCapMesh.Unit,
                lampMaterial,
                new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    Mathf.Sin(angle) * ringRadius,
                    lampBaseZ),
                Quaternion.identity,
                Vector3.one * lampBaseRadius,
                false);

            var lamp = lampObject.AddComponent<TopLockLamp>();
            lamp.Configure(colorId, DisplayColor(colorId));
            lamp.MarkAsGoalLamp();
            lampObject.SetActive(true);
            lamps[colorId] = lamp;
        }

        if (!string.IsNullOrEmpty(goal.centerLamp))
        {
            string centerColorId = goal.centerLamp;
            GameObject centerObject = CreateMeshObject(
                "CenterLamp_" + centerColorId,
                root,
                SphericalCapMesh.Unit,
                lampMaterial,
                new Vector3(0f, 0f, lampBaseZ),
                Quaternion.identity,
                Vector3.one * lampBaseRadius,
                false);

            var centerLamp = centerObject.AddComponent<TopLockLamp>();
            centerLamp.Configure(centerColorId, DisplayColor(centerColorId));
            centerLamp.MarkAsGoalLamp();
            centerObject.SetActive(true);
            lamps[centerColorId] = centerLamp;
        }

        GoalLamps = new TopLockLamp[lamps.Count];
        lamps.Values.CopyTo(GoalLamps, 0);
    }

    private void BuildBeams(DeviceConfigEntry[] devices)
    {
        foreach (DeviceConfigEntry entry in devices)
        {
            if (entry == null || entry.type != "emitter" || string.IsNullOrEmpty(entry.color))
            {
                continue;
            }

            Transform puzzle = GetPuzzle(entry.face);

            if (puzzle == null)
            {
                continue;
            }

            var beamObject = new GameObject("Beam_" + IdSuffix(entry.id));
            Transform beamTransform = beamObject.transform;
            beamTransform.SetParent(puzzle, false);
            beamTransform.localPosition = Vector3.zero;
            beamTransform.localRotation = Quaternion.identity;
            beamTransform.localScale = Vector3.one;

            var line = beamObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.startWidth = 0.03f;
            line.endWidth = 0.03f;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.sharedMaterial = FindMaterial("Beam_" + entry.color);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
        }
    }

    private void BuildWalls(WallConfigEntry[] walls)
    {
        WallCount = 0;

        if (walls == null)
        {
            return;
        }

        foreach (WallConfigEntry entry in walls)
        {
            if (entry == null)
            {
                continue;
            }

            Transform puzzle = GetPuzzle(entry.face);

            if (puzzle == null)
            {
                continue;
            }

            string objectName = string.IsNullOrEmpty(entry.name)
                ? NameFromId(entry.id)
                : entry.name;
            GameObject wallObject = CreateMeshObject(
                objectName,
                puzzle,
                BuiltinMesh("Cube"),
                FindMaterial("SatinGlassWall"),
                PositionOf(entry.position, DefaultDeviceZ),
                Quaternion.Euler(0f, 0f, entry.rotation),
                SizeOf(entry.size, new Vector3(0.02f, 0.4f, 0.1f)),
                entry.castShadows);

            var collider = wallObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            wallObject.AddComponent<LaserWall>();
            wallObject.SetActive(true);
            WallCount++;
        }
    }

    private void BuildMirrors(MirrorConfigEntry[] mirrors)
    {
        MirrorCount = 0;

        if (mirrors == null)
        {
            return;
        }

        foreach (MirrorConfigEntry entry in mirrors)
        {
            if (entry == null)
            {
                continue;
            }

            Transform puzzle = GetPuzzle(entry.face);

            if (puzzle == null)
            {
                continue;
            }

            string objectName = string.IsNullOrEmpty(entry.name)
                ? NameFromId(entry.id)
                : entry.name;
            GameObject mirrorObject = CreateMeshObject(
                objectName,
                puzzle,
                BuiltinMesh("Cube"),
                FindMaterial("Mirror"),
                PositionOf(entry.position, DefaultDeviceZ),
                Quaternion.Euler(0f, 0f, entry.rotation),
                SizeOf(entry.size, new Vector3(0.02f, 0.1f, 0.1f)),
                entry.castShadows);

            var collider = mirrorObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            mirrorObject.AddComponent<LaserMirror>();
            mirrorObject.SetActive(true);
            MirrorCount++;
        }
    }

    private void BuildStatelessDevices(DeviceConfigEntry[] devices)
    {
        foreach (DeviceConfigEntry entry in devices)
        {
            if (entry == null
                || (entry.type != "cylindrical_lens"
                    && entry.type != "filter"
                    && entry.type != "mirror"
                    && entry.type != "prism"
                    && entry.type != "concave_mirror"))
            {
                continue;
            }

            GameObject deviceObject = CreateDeviceObject(entry);

            if (deviceObject == null)
            {
                continue;
            }

            if (entry.type == "cylindrical_lens")
            {
                var lens = deviceObject.AddComponent<LaserLens>();
                float aperture = entry.aperture > 0f ? entry.aperture : 0.215f;
                float focal = entry.focalLength > 0f ? entry.focalLength : 0.1f;
                lens.Configure(focal, aperture);
            }
            else if (entry.type == "filter")
            {
                var filter = deviceObject.AddComponent<LaserFilter>();
                filter.Configure(entry.color);
            }
            else if (entry.type == "mirror")
            {
                deviceObject.AddComponent<LaserMirror>();
            }
            else if (entry.type == "prism")
            {
                Vector3 prismSize = PrismSizeOf(entry);
                var prism = deviceObject.AddComponent<LaserPrism>();
                prism.Configure(
                    prismSize.x,
                    prismSize.y,
                    prismSize.z,
                    entry.iorR,
                    entry.iorG,
                    entry.iorB);
            }
            else
            {
                var mirror = deviceObject.AddComponent<LaserConcaveMirror>();
                mirror.Configure(RadiusOf(entry), ApertureOf(entry));
            }

            AddLock(deviceObject, entry);
            deviceObject.SetActive(true);
            DeviceCount++;
        }
    }

    private void BuildReceivers(DeviceConfigEntry[] devices)
    {
        var pending = new List<GameObject>();

        foreach (DeviceConfigEntry entry in devices)
        {
            if (entry == null || entry.type != "receiver")
            {
                continue;
            }

            GameObject deviceObject = CreateDeviceObject(entry);

            if (deviceObject == null)
            {
                continue;
            }

            string lampId = string.IsNullOrEmpty(entry.lamp) ? null : entry.lamp;
            TopLockLamp targetLamp = null;

            if (lampId != null && !lamps.TryGetValue(lampId, out targetLamp))
            {
                Debug.LogWarning(
                    $"[DeviceConfigLoader] Receiver {entry.id} references missing lamp '{lampId}'.");
            }

            var receiver = deviceObject.AddComponent<LaserReceiver>();
            receiver.Configure(entry.color, targetLamp, ReceiverFaceRadius(entry), ReceiverPivot(entry));
            AddLock(deviceObject, entry);
            pending.Add(deviceObject);
            DeviceCount++;
        }

        foreach (GameObject deviceObject in pending)
        {
            deviceObject.SetActive(true);
        }
    }

    private void BuildEmitters(DeviceConfigEntry[] devices)
    {
        var pending = new List<GameObject>();

        foreach (DeviceConfigEntry entry in devices)
        {
            if (entry == null || entry.type != "emitter")
            {
                continue;
            }

            GameObject deviceObject = CreateDeviceObject(entry);

            if (deviceObject == null)
            {
                continue;
            }

            var emitter = deviceObject.AddComponent<LaserEmitter>();
            Vector2? aim = entry.aim != null
                ? new Vector2(entry.aim.x, entry.aim.y)
                : (Vector2?)null;
            emitter.Configure(entry.color, IdSuffix(entry.id), aim);
            AddLock(deviceObject, entry);
            pending.Add(deviceObject);
            DeviceCount++;
        }

        // Emitters collect the optics of the face in Awake. Activate every emitter
        // object only after all of them (and their locks) exist, then refresh the aim
        // so the JSON placement is the final word.
        foreach (GameObject deviceObject in pending)
        {
            deviceObject.SetActive(true);
        }

        foreach (GameObject deviceObject in pending)
        {
            deviceObject.GetComponent<LaserEmitter>().RebaseAim();
        }
    }

    private GameObject CreateDeviceObject(DeviceConfigEntry entry)
    {
        Transform puzzle = GetPuzzle(entry.face);

        if (puzzle == null)
        {
            return null;
        }

        Quaternion localRotation = DeviceRotation(entry);
        GameObject deviceObject = CreateMeshObject(
            DeviceName(entry),
            puzzle,
            MeshFor(entry),
            MaterialFor(entry),
            PositionOf(entry.position, DefaultDeviceZ),
            localRotation,
            ScaleFor(entry),
            entry.castShadows);

        if (entry.type == "concave_mirror")
        {
            var mirrorRenderer = deviceObject.GetComponent<MeshRenderer>();

            if (mirrorRenderer != null && mirrorRenderer.sharedMaterial != null)
            {
                var material = new Material(mirrorRenderer.sharedMaterial);
                material.SetFloat("_Cull", (float)CullMode.Off);
                mirrorRenderer.sharedMaterial = material;
            }
        }

        AddDeviceCollider(deviceObject, entry);

        var selectable = deviceObject.AddComponent<DeviceSelectable>();
        selectable.Configure(entry.id, selectionMaterial);
        selectable.ApplyPlacement(deviceObject.transform.localPosition, localRotation);
        selectable.ApplyConfig(entry);
        return deviceObject;
    }

    private static Quaternion DeviceRotation(DeviceConfigEntry entry)
    {
        Quaternion faceRotation = Quaternion.Euler(0f, 0f, entry.rotation);

        if (entry.type == "receiver")
        {
            // The receiver mesh already lives in face-local space with its flat side
            // against the wall, so no extra tilt is needed. JSON rotation is ignored.
            return Quaternion.identity;
        }

        return faceRotation;
    }

    private static void AddDeviceCollider(GameObject deviceObject, DeviceConfigEntry entry)
    {
        switch (entry.type)
        {
            case "emitter":
            case "receiver":
                var capsule = deviceObject.AddComponent<CapsuleCollider>();
                capsule.radius = 0.6f;
                capsule.height = 2.2f;
                capsule.direction = 1;
                capsule.center = Vector3.zero;
                break;

            case "cylindrical_lens":
                var lensBox = deviceObject.AddComponent<BoxCollider>();
                lensBox.center = Vector3.zero;
                lensBox.size = new Vector3(0.05f, 0.215f, 0.08f);
                break;

            case "filter":
                var filterBox = deviceObject.AddComponent<BoxCollider>();
                filterBox.center = Vector3.zero;
                filterBox.size = Vector3.one;
                break;

            case "mirror":
                var deviceMirrorBox = deviceObject.AddComponent<BoxCollider>();
                deviceMirrorBox.center = Vector3.zero;
                deviceMirrorBox.size = Vector3.one;
                break;

            case "prism":
                Vector3 prismSize = PrismSizeOf(entry);
                var prismBox = deviceObject.AddComponent<BoxCollider>();
                prismBox.center = Vector3.zero;
                prismBox.size = new Vector3(prismSize.x, prismSize.y, prismSize.z);
                break;

            case "concave_mirror":
                float radius = RadiusOf(entry);
                float aperture = ApertureOf(entry);
                float thickness = ThicknessOf(entry);
                float depth = DepthOf(entry);
                float halfAperture = Mathf.Min(aperture * 0.5f, radius * 0.98f);
                float sagitta = radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - halfAperture * halfAperture));
                var mirrorBox = deviceObject.AddComponent<BoxCollider>();
                mirrorBox.size = new Vector3(sagitta + thickness, aperture, depth);
                mirrorBox.center = new Vector3((thickness - sagitta) * 0.5f, 0f, 0f);
                break;
        }
    }

    private void AddLock(GameObject deviceObject, DeviceConfigEntry entry)
    {
        if (entry.unlockRequires == null || entry.unlockRequires.Length == 0)
        {
            return;
        }

        var laserLock = deviceObject.AddComponent<LaserLock>();
        laserLock.ConfigureStyle(FindMaterial("LockBase"), FindMaterial("Lamp"));
        laserLock.Configure(entry.unlockRequires);
    }

    private GameObject CreateMeshObject(
        string objectName,
        Transform parent,
        Mesh mesh,
        Material material,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        bool castShadows)
    {
        var target = new GameObject(objectName);
        Transform targetTransform = target.transform;
        targetTransform.SetParent(parent, false);
        targetTransform.localPosition = localPosition;
        targetTransform.localRotation = localRotation;
        targetTransform.localScale = localScale;
        target.SetActive(false);

        var meshFilter = target.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var meshRenderer = target.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = castShadows
            ? ShadowCastingMode.On
            : ShadowCastingMode.Off;
        meshRenderer.receiveShadows = castShadows;
        return target;
    }

    private Mesh MeshFor(DeviceConfigEntry entry)
    {
        switch (entry.type)
        {
            case "emitter":
                return BuiltinMesh("Cylinder");

            case "receiver":
                return ReceiverMeshFor(entry);

            case "cylindrical_lens":
                if (levelAssets.lensMesh != null)
                {
                    return levelAssets.lensMesh;
                }

                Debug.LogError("[DeviceConfigLoader] LevelAssets.lensMesh is missing.");
                return BuiltinMesh("Cube");

            case "filter":
                return BuiltinMesh("Cube");

            case "mirror":
                return BuiltinMesh("Cube");

            case "prism":
                Vector3 prismSize = PrismSizeOf(entry);
                return PrismMesh.Create(prismSize.x, prismSize.y, prismSize.z);

            case "concave_mirror":
                return ConcaveMirrorMesh.Create(
                    RadiusOf(entry),
                    ApertureOf(entry),
                    ThicknessOf(entry),
                    DepthOf(entry));

            default:
                Debug.LogError($"[DeviceConfigLoader] Unknown device type: {entry.type} ({entry.id})");
                return BuiltinMesh("Cube");
        }
    }

    private static Mesh ReceiverMeshFor(DeviceConfigEntry entry)
    {
        Vector2 pivot = ReceiverPivot(entry);
        float diameter = ReceiverFaceRadius(entry) * 2f;
        Vector2 pivotUnit = diameter > 0.0001f ? pivot / diameter : Vector2.zero;
        float inwardDegrees = Mathf.Atan2(-pivot.y, -pivot.x) * Mathf.Rad2Deg;
        bool corner = Mathf.Abs(pivot.x) > 0f && Mathf.Abs(pivot.y) > 0f;
        bool freeStanding = Mathf.Abs(pivot.x) < 0.00001f && Mathf.Abs(pivot.y) < 0.00001f;

        if (freeStanding)
        {
            return ReceiverMesh.CreateFullCylinder();
        }

        return corner
            ? ReceiverMesh.CreateQuarterCylinder(pivotUnit, inwardDegrees)
            : ReceiverMesh.CreateHalfCylinder(pivotUnit, inwardDegrees);
    }

    private static Vector2 ReceiverPivot(DeviceConfigEntry entry)
    {
        // JsonUtility may leave a default instance for a missing nested object on
        // some runtimes; a zero pivot means "not a custom mount point", so fall back
        // to the automatic wall/corner pivot in that case.
        if (entry.mountPivot != null
            && (Mathf.Abs(entry.mountPivot.x) > 0.000001f
                || Mathf.Abs(entry.mountPivot.y) > 0.000001f))
        {
            return new Vector2(entry.mountPivot.x, entry.mountPivot.y);
        }

        float x = entry.position != null ? entry.position.x : 0f;
        float y = entry.position != null ? entry.position.y : 0f;
        float gapX = FaceBound - Mathf.Abs(x);
        float gapY = FaceBound - Mathf.Abs(y);
        bool onX = gapX < WallMountThreshold;
        bool onY = gapY < WallMountThreshold;
        float pivotX = onX ? Mathf.Sign(x) * gapX : 0f;
        float pivotY = onY ? Mathf.Sign(y) * gapY : 0f;

        return new Vector2(pivotX, pivotY);
    }

    private static float ReceiverFaceRadius(DeviceConfigEntry entry)
    {
        var fallback = new Vector3(0.066f, 0.066f, 0.066f);
        return SizeOf(entry.size, fallback).x * 0.5f;
    }

    private Material MaterialFor(DeviceConfigEntry entry)
    {
        switch (entry.type)
        {
            case "emitter":
            case "receiver":
                return FindMaterial("Device_" + entry.color);

            case "cylindrical_lens":
                return FindMaterial("Lens");

            case "filter":
                return FindMaterial("Filter_" + entry.color);

            case "mirror":
            case "concave_mirror":
                return FindMaterial("Mirror");

            case "prism":
                return prismMaterial != null ? prismMaterial : FindMaterial("Lens");

            default:
                return FindMaterial("Device_W");
        }
    }

    private static Vector3 ScaleFor(DeviceConfigEntry entry)
    {
        if (entry.type == "concave_mirror" || entry.type == "prism")
        {
            return Vector3.one;
        }

        if (entry.type == "receiver")
        {
            // The receiver mesh uses a unit radius/unit depth, so scale X is the
            // diameter, Y is also the diameter (in-plane), and Z is the bump depth.
            var fallback = new Vector3(0.066f, 0.066f, 0.066f);
            Vector3 receiverSize = SizeOf(entry.size, fallback);
            float diameter = receiverSize.x > 0f ? receiverSize.x : 0.066f;
            float depth = receiverSize.z > 0f ? receiverSize.z : 0.066f;
            return new Vector3(diameter, diameter, depth);
        }

        Vector3 scale;

        switch (entry.type)
        {
            case "emitter":
                scale = Vector3.one * 0.033f;
                break;

            case "receiver":
                scale = new Vector3(0.044f, 0.022f, 0.044f);
                break;

            case "cylindrical_lens":
                float aperture = entry.aperture > 0f ? entry.aperture : 0.215f;
                scale = new Vector3(1f, aperture / 0.215f, 1f);
                break;

            case "filter":
                scale = new Vector3(0.02f, 0.215f, 0.08f);
                break;

            case "mirror":
                scale = new Vector3(0.02f, 0.1075f, 0.1f);
                break;

            default:
                scale = Vector3.one * 0.033f;
                break;
        }

        if (entry.size != null)
        {
            if (entry.size.x > 0f)
            {
                scale.x = entry.size.x;
            }

            if (entry.size.y > 0f)
            {
                scale.y = entry.size.y;
            }

            if (entry.size.z > 0f)
            {
                scale.z = entry.size.z;
            }
        }

        return scale;
    }

    private static Vector3 SizeOf(LevelSize size, Vector3 fallback)
    {
        if (size == null)
        {
            return fallback;
        }

        return new Vector3(
            size.x > 0f ? size.x : fallback.x,
            size.y > 0f ? size.y : fallback.y,
            size.z > 0f ? size.z : fallback.z);
    }

    private static Vector3 PrismSizeOf(DeviceConfigEntry entry)
    {
        return SizeOf(entry.size, new Vector3(0.16f, 0.138564f, 0.1f));
    }

    private static Vector3 PositionOf(LevelPosition position, float defaultZ)
    {
        if (position == null)
        {
            return new Vector3(0f, 0f, defaultZ);
        }

        float z = Mathf.Abs(position.z) > 0.000001f ? position.z : defaultZ;
        return new Vector3(position.x, position.y, z);
    }

    private static string DeviceName(DeviceConfigEntry entry)
    {
        string suffix = IdSuffix(entry.id);

        switch (entry.type)
        {
            case "emitter":
                return "Emitter_" + suffix;

            case "receiver":
                return "Receiver_" + suffix;

            case "cylindrical_lens":
                return "Lens_" + suffix;

            case "filter":
                return "Filter_" + suffix;

            case "mirror":
                return "Mirror_" + suffix;

            case "prism":
                return "Prism_" + suffix;

            case "concave_mirror":
                return "ConcaveMirror_" + suffix;

            default:
                return NameFromId(entry.id);
        }
    }

    private static float RadiusOf(DeviceConfigEntry entry)
    {
        return entry.radius > 0f ? entry.radius : 0.18f;
    }

    private static float ApertureOf(DeviceConfigEntry entry)
    {
        return entry.aperture > 0f ? entry.aperture : 0.2f;
    }

    private static float ThicknessOf(DeviceConfigEntry entry)
    {
        return entry.thickness > 0f ? entry.thickness : 0.02f;
    }

    private static float DepthOf(DeviceConfigEntry entry)
    {
        return entry.size != null && entry.size.z > 0f ? entry.size.z : 0.1f;
    }

    private static string IdSuffix(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return "Unknown";
        }

        return id.Contains(".")
            ? id.Substring(id.LastIndexOf('.') + 1)
            : id;
    }

    private static string NameFromId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return "LevelElement";
        }

        return id.Replace('.', '_').Replace(' ', '_');
    }

    private Transform GetFace(string faceName)
    {
        if (!string.IsNullOrEmpty(faceName) && faceRoots.TryGetValue(faceName, out Transform face))
        {
            return face;
        }

        Debug.LogError($"[DeviceConfigLoader] Unknown face: {faceName}");
        return null;
    }

    private Transform GetPuzzle(string faceName)
    {
        if (!string.IsNullOrEmpty(faceName) && puzzleRoots.TryGetValue(faceName, out Transform puzzle))
        {
            return puzzle;
        }

        Debug.LogError($"[DeviceConfigLoader] Unknown face: {faceName}");
        return null;
    }

    private Material FindMaterial(string materialName)
    {
        if (materials.TryGetValue(materialName, out Material material) && material != null)
        {
            return material;
        }

        Debug.LogError($"[DeviceConfigLoader] Material not found: {materialName}");
        return null;
    }

    private static Mesh BuiltinMesh(string meshName)
    {
        return Resources.GetBuiltinResource<Mesh>(meshName + ".fbx");
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
}

