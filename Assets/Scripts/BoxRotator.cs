using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BoxRotator : MonoBehaviour
{
    [SerializeField] private float rotationSensitivity = 0.2f;
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField] private float turnDuration = 0.2f;

    private static readonly List<Quaternion> LocalSnapOrientations = BuildSnapOrientations();

    private Camera rotationCamera;
    private bool dragging;
    private Vector3 lastMousePosition;

    private bool isSnapping;
    private Quaternion snapStart;
    private Quaternion snapTarget;
    private float snapElapsed;

    private bool isTurning;
    private Quaternion turnStart;
    private Quaternion turnTarget;
    private float turnElapsed;

    private void Awake()
    {
        rotationCamera = Camera.main;

        if (LocalSnapOrientations.Count != 24)
        {
            Debug.LogWarning($"[BoxRotator] Expected 24 snap orientations, got {LocalSnapOrientations.Count}.");
        }
    }

    private void Start()
    {
        transform.rotation = GetNearestWorldSnap(transform.rotation);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CancelTurn();
            CancelSnap();

            if (DevicePicker.Pick(Input.mousePosition, rotationCamera) != null)
            {
                dragging = false;
                return;
            }

            dragging = true;
            lastMousePosition = Input.mousePosition;
            return;
        }

        if (TryHandleNumpad())
        {
            return;
        }

        if (dragging)
        {
            if (!Input.GetMouseButton(0))
            {
                dragging = false;
                BeginSnap();
                return;
            }

            HandleDrag();
            return;
        }

        if (isTurning)
        {
            UpdateTurn();
            return;
        }

        if (isSnapping)
        {
            UpdateSnap();
        }
    }

    private bool TryHandleNumpad()
    {
        // Rubik's notation, interpreted against the camera reference frame.
        Vector3 rightAxis = rotationCamera != null ? rotationCamera.transform.right : Vector3.right;
        Vector3 upAxis = rotationCamera != null ? rotationCamera.transform.up : Vector3.up;
        Vector3 forwardAxis = rotationCamera != null ? rotationCamera.transform.forward : Vector3.forward;

        if (Input.GetKeyDown(KeyCode.Keypad8))
        {
            StartTurn(rightAxis, -90f);   // x
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            StartTurn(rightAxis, 90f);    // x'
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Keypad4))
        {
            StartTurn(upAxis, -90f);      // y'
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Keypad6))
        {
            StartTurn(upAxis, 90f);       // y
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            StartTurn(forwardAxis, 90f);  // z'
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            StartTurn(forwardAxis, -90f); // z
            return true;
        }

        return false;
    }

    private void StartTurn(Vector3 worldAxis, float angle)
    {
        if (isTurning)
        {
            transform.rotation = turnTarget;
            isTurning = false;
        }

        if (isSnapping)
        {
            transform.rotation = snapTarget;
            isSnapping = false;
        }

        if (dragging)
        {
            dragging = false;
            transform.rotation = GetNearestWorldSnap(transform.rotation);
        }

        isTurning = true;
        turnStart = transform.rotation;
        turnTarget = Quaternion.AngleAxis(angle, worldAxis) * turnStart;
        turnElapsed = 0f;
    }

    private void UpdateTurn()
    {
        turnElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(turnElapsed / Mathf.Max(0.0001f, turnDuration));
        float eased = Mathf.SmoothStep(0f, 1f, t);

        transform.rotation = Quaternion.Slerp(turnStart, turnTarget, eased);

        if (t >= 1f)
        {
            transform.rotation = turnTarget;
            isTurning = false;
        }
    }

    private void HandleDrag()
    {
        Vector3 delta = Input.mousePosition - lastMousePosition;
        lastMousePosition = Input.mousePosition;

        if (delta.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 upAxis = rotationCamera != null ? rotationCamera.transform.up : Vector3.up;
        Vector3 rightAxis = rotationCamera != null ? rotationCamera.transform.right : Vector3.right;

        transform.Rotate(upAxis, -delta.x * rotationSensitivity, Space.World);
        transform.Rotate(rightAxis, delta.y * rotationSensitivity, Space.World);
    }

    private void BeginSnap()
    {
        snapStart = transform.rotation;
        snapTarget = GetNearestWorldSnap(snapStart);
        snapElapsed = 0f;
        isSnapping = true;

        if (Quaternion.Angle(snapStart, snapTarget) < 0.01f)
        {
            transform.rotation = snapTarget;
            isSnapping = false;
        }
    }

    private void UpdateSnap()
    {
        snapElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(snapElapsed / Mathf.Max(0.0001f, snapDuration));
        float eased = Mathf.SmoothStep(0f, 1f, t);

        transform.rotation = Quaternion.Slerp(snapStart, snapTarget, eased);

        if (t >= 1f)
        {
            transform.rotation = snapTarget;
            isSnapping = false;
        }
    }

    private void CancelTurn()
    {
        isTurning = false;
    }

    private void CancelSnap()
    {
        isSnapping = false;
    }

    private Quaternion GetNearestWorldSnap(Quaternion worldRotation)
    {
        Quaternion cameraRotation = rotationCamera != null
            ? rotationCamera.transform.rotation
            : Quaternion.identity;

        Quaternion localRotation = Quaternion.Inverse(cameraRotation) * worldRotation;
        Quaternion bestLocal = LocalSnapOrientations[0];
        float bestAngle = Quaternion.Angle(localRotation, bestLocal);

        for (int i = 1; i < LocalSnapOrientations.Count; i++)
        {
            float angle = Quaternion.Angle(localRotation, LocalSnapOrientations[i]);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestLocal = LocalSnapOrientations[i];
            }
        }

        return cameraRotation * bestLocal;
    }

    private static List<Quaternion> BuildSnapOrientations()
    {
        var orientations = new List<Quaternion>(24);

        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                for (int z = 0; z < 4; z++)
                {
                    Quaternion candidate = Quaternion.Euler(90f * x, 90f * y, 90f * z);

                    bool alreadyExists = false;
                    foreach (Quaternion existing in orientations)
                    {
                        if (Mathf.Abs(Quaternion.Dot(candidate, existing)) > 0.9995f)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        orientations.Add(candidate);
                    }
                }
            }
        }

        return orientations;
    }
}
