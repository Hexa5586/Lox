using UnityEngine;

[DisallowMultipleComponent]
public class LaserMirror : MonoBehaviour
{
    public bool TryIntersect(
        Vector2 origin,
        Vector2 direction,
        out float distance,
        out Vector2 normal)
    {
        distance = 0f;
        normal = Vector2.up;

        Quaternion inverseRotation = Quaternion.Inverse(transform.localRotation);
        Vector3 localOrigin3 = inverseRotation * (new Vector3(origin.x, origin.y, 0f) - transform.localPosition);
        Vector3 localDirection3 = inverseRotation * new Vector3(direction.x, direction.y, 0f);

        Vector2 localOrigin = new Vector2(localOrigin3.x, localOrigin3.y);
        Vector2 localDirection = new Vector2(localDirection3.x, localDirection3.y);

        if (Mathf.Abs(localDirection.x) < 0.000001f)
        {
            return false;
        }

        float t = -localOrigin.x / localDirection.x;

        if (t <= 0.0001f)
        {
            return false;
        }

        float hitY = localOrigin.y + localDirection.y * t;
        float halfLength = transform.localScale.y * 0.5f;

        if (Mathf.Abs(hitY) > halfLength)
        {
            return false;
        }

        Vector3 localNormal3 = new Vector3(1f, 0f, 0f);
        Vector3 worldNormal3 = transform.localRotation * localNormal3;
        Vector2 worldNormal = new Vector2(worldNormal3.x, worldNormal3.y);

        if (Vector2.Dot(direction, worldNormal) > 0f)
        {
            worldNormal = -worldNormal;
        }

        distance = t;
        normal = worldNormal;
        return true;
    }
}
