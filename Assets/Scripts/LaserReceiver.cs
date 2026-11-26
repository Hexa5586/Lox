using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LaserReceiver : MonoBehaviour
{
    private static readonly List<LaserReceiver> Active = new List<LaserReceiver>();

    private const float HitPadding = 0.004f;

    [SerializeField] private string colorId;
    [SerializeField] private TopLockLamp lamp;
    [SerializeField] private float faceRadius = -1f;
    [SerializeField] private Vector2 hitCenterOffset;

    private readonly List<LightColor> beams = new List<LightColor>(4);
    private bool receiving;

    public string ColorId => colorId;
    public bool IsReceiving => receiving;

    public Vector2 HitCenter
    {
        get
        {
            var center = new Vector2(transform.localPosition.x, transform.localPosition.y);
            return center + hitCenterOffset;
        }
    }

    public float HitRadius
    {
        get
        {
            if (faceRadius > 0f)
            {
                return faceRadius + HitPadding;
            }

            // Fallback for receivers created without an explicit radius: use the actual
            // mesh extent in the face plane.
            var meshFilter = GetComponent<MeshFilter>();
            float radius = Mathf.Abs(transform.localScale.x) * 0.5f;

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds bounds = meshFilter.sharedMesh.bounds;
                radius = Mathf.Max(
                    Mathf.Abs(transform.localScale.x) * bounds.extents.x,
                    Mathf.Abs(transform.localScale.z) * bounds.extents.z);
            }

            return radius + HitPadding;
        }
    }

    public static bool IsColorLit(string targetColorId)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            LaserReceiver receiver = Active[i];

            if (receiver != null && receiver.receiving && receiver.colorId == targetColorId)
            {
                return true;
            }
        }

        return false;
    }

    public void Configure(string id, TopLockLamp targetLamp, float radius = -1f, Vector2 mountPivot = default)
    {
        colorId = id;
        lamp = targetLamp;
        faceRadius = radius;
        hitCenterOffset = mountPivot;
    }

    public void AddBeam(LightColor color)
    {
        beams.Add(color);
    }

    private void OnEnable()
    {
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }

        beams.Clear();

        if (lamp != null)
        {
            lamp.SetRequirementMet(false);
        }
    }

    private void OnDisable()
    {
        Active.Remove(this);
        beams.Clear();
        SetReceiving(false);
    }

    private void LateUpdate()
    {
        MixResult mix = ColorMath.Mix(beams);
        beams.Clear();
        SetReceiving(ColorMath.Matches(mix, colorId));
    }

    private void SetReceiving(bool value)
    {
        if (receiving == value)
        {
            return;
        }

        receiving = value;

        if (lamp != null)
        {
            lamp.SetRequirementMet(value);
        }
    }
}
