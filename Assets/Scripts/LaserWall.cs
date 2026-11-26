using UnityEngine;

[DisallowMultipleComponent]
public class LaserWall : MonoBehaviour
{
    public bool TryIntersect(Vector2 origin, Vector2 direction, out float distance)
    {
        distance = 0f;

        Quaternion inverseRotation = Quaternion.Inverse(transform.localRotation);
        Vector3 localOrigin3 = inverseRotation * (new Vector3(origin.x, origin.y, 0f) - transform.localPosition);
        Vector3 localDirection3 = inverseRotation * new Vector3(direction.x, direction.y, 0f);

        Vector2 localOrigin = new Vector2(localOrigin3.x, localOrigin3.y);
        Vector2 localDirection = new Vector2(localDirection3.x, localDirection3.y);

        float halfX = transform.localScale.x * 0.5f;
        float halfY = transform.localScale.y * 0.5f;
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

        distance = tMin > 0f ? tMin : tMax;
        return distance > 0.0001f;
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
