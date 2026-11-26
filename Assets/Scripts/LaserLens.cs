using UnityEngine;

[DisallowMultipleComponent]
public class LaserLens : MonoBehaviour
{
    [SerializeField] private float focalLength = 0.1f;
    [SerializeField] private float aperture = 0.215f;

    public void Configure(float focal, float height)
    {
        focalLength = focal;
        aperture = height;
    }

    public bool TryRefract(
        Vector2 origin,
        Vector2 direction,
        out float distance,
        out Vector2 outgoingDirection)
    {
        distance = 0f;
        outgoingDirection = direction;

        if (focalLength <= 0.000001f || aperture <= 0.000001f)
        {
            return false;
        }

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

        if (Mathf.Abs(hitY) > aperture * 0.5f)
        {
            return false;
        }

        float sign = Mathf.Sign(localDirection.x);
        float forward = Mathf.Abs(localDirection.x);
        float outgoingSlope = localDirection.y - (hitY / focalLength) * forward;

        Vector2 localOutgoing = new Vector2(sign * forward, outgoingSlope);
        Vector3 worldOutgoing = transform.localRotation * new Vector3(localOutgoing.x, localOutgoing.y, 0f);
        outgoingDirection = new Vector2(worldOutgoing.x, worldOutgoing.y).normalized;

        distance = t;
        return true;
    }
}
