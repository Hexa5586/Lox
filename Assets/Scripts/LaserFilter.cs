using UnityEngine;

[DisallowMultipleComponent]
public class LaserFilter : MonoBehaviour
{
    [SerializeField] private string filterColorId = "G";

    private LightColor filterColor = Palette.W;

    public string FilterColorId => filterColorId;
    public LightColor FilterColor => filterColor;

    public void Configure(string id)
    {
        filterColorId = id;
        ResolveColor();
    }

    private void Awake()
    {
        ResolveColor();
    }

    private void ResolveColor()
    {
        if (!Palette.TryGet(filterColorId, out filterColor))
        {
            filterColor = Palette.W;
            Debug.LogWarning($"[LaserFilter] Unknown filter color id: {filterColorId}");
        }
    }

    /// <summary>
    /// A thin plate with the broad faces along its local Y axis. A beam that
    /// enters through a broad face keeps its filtered components; a beam that
    /// hits the narrow end is blocked.
    /// </summary>
    public bool TryFilter(
        Vector2 origin,
        Vector2 direction,
        LightColor beam,
        out float distance,
        out LightColor outgoing,
        out bool blocked)
    {
        distance = 0f;
        outgoing = beam;
        blocked = false;

        float halfX = Mathf.Abs(transform.localScale.x) * 0.5f;
        float halfY = Mathf.Abs(transform.localScale.y) * 0.5f;

        if (halfX <= 1e-6f || halfY <= 1e-6f)
        {
            return false;
        }

        Quaternion inverseRotation = Quaternion.Inverse(transform.localRotation);
        Vector3 localOrigin3 = inverseRotation * (new Vector3(origin.x, origin.y, 0f) - transform.localPosition);
        Vector3 localDirection3 = inverseRotation * new Vector3(direction.x, direction.y, 0f);

        Vector2 localOrigin = new Vector2(localOrigin3.x, localOrigin3.y);
        Vector2 localDirection = new Vector2(localDirection3.x, localDirection3.y);

        if (Mathf.Abs(localOrigin.x) < halfX && Mathf.Abs(localOrigin.y) < halfY)
        {
            return false;
        }

        float tMin = 0f;
        float tMax = float.PositiveInfinity;

        if (!Slab(localOrigin.x, localDirection.x, -halfX, halfX, ref tMin, ref tMax))
        {
            return false;
        }

        if (!Slab(localOrigin.y, localDirection.y, -halfY, halfY, ref tMin, ref tMax))
        {
            return false;
        }

        if (tMax < 0f || tMin > tMax)
        {
            return false;
        }

        float t = tMin > 0f ? tMin : tMax;

        if (t <= 0.0001f)
        {
            return false;
        }

        distance = t;
        Vector2 localHit = localOrigin + localDirection * t;

        if (Mathf.Abs(localHit.y) >= halfY - 1e-5f)
        {
            blocked = true;
            return true;
        }

        LightColor filtered = ColorMath.ApplyFilter(beam, filterColor);
        outgoing = filtered;
        blocked = filtered.Total <= 1e-6f;
        return true;
    }

    private static bool Slab(
        float origin,
        float direction,
        float min,
        float max,
        ref float tMin,
        ref float tMax)
    {
        if (Mathf.Abs(direction) < 0.000001f)
        {
            return origin >= min && origin <= max;
        }

        float t1 = (min - origin) / direction;
        float t2 = (max - origin) / direction;

        if (t1 > t2)
        {
            float temp = t1;
            t1 = t2;
            t2 = temp;
        }

        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        return tMin <= tMax;
    }
}
