using UnityEngine;

[DisallowMultipleComponent]
public class LaserConcaveMirror : MonoBehaviour
{
    [SerializeField] private float radius = 0.18f;
    [SerializeField] private float aperture = 0.2f;

    public void Configure(float curveRadius, float opening)
    {
        radius = Mathf.Max(0.01f, curveRadius);
        aperture = Mathf.Max(0.001f, opening);
    }

    public bool TryIntersect(
        Vector2 origin,
        Vector2 direction,
        out float distance,
        out Vector2 normal,
        out bool reflective)
    {
        distance = 0f;
        normal = Vector2.right;
        reflective = false;

        if (radius <= 1e-6f || aperture <= 1e-6f)
        {
            return false;
        }

        Quaternion inverseRotation = Quaternion.Inverse(transform.localRotation);
        Vector3 localOrigin3 = inverseRotation * (new Vector3(origin.x, origin.y, 0f) - transform.localPosition);
        Vector3 localDirection3 = inverseRotation * new Vector3(direction.x, direction.y, 0f);

        Vector2 localOrigin = new Vector2(localOrigin3.x, localOrigin3.y);
        Vector2 localDirection = new Vector2(localDirection3.x, localDirection3.y).normalized;
        Vector2 center = new Vector2(-radius, 0f);
        Vector2 toOrigin = localOrigin - center;

        float b = Vector2.Dot(toOrigin, localDirection);
        float c = toOrigin.sqrMagnitude - radius * radius;
        float discriminant = b * b - c;

        if (discriminant < 0f)
        {
            return false;
        }

        float root = Mathf.Sqrt(discriminant);
        float tMinus = -b - root;
        float tPlus = -b + root;
        float halfAperture = aperture * 0.5f;

        bool found = false;
        float bestX = float.NegativeInfinity;
        Vector2 bestPoint = Vector2.zero;

        TryCandidate(localOrigin, localDirection, tMinus, halfAperture, ref found, ref bestX, ref distance, ref bestPoint);
        TryCandidate(localOrigin, localDirection, tPlus, halfAperture, ref found, ref bestX, ref distance, ref bestPoint);

        if (!found)
        {
            return false;
        }

        Vector2 point = bestPoint;
        Vector2 localNormal = (center - point) / radius;
        Vector3 normal3 = transform.localRotation * new Vector3(localNormal.x, localNormal.y, 0f);
        normal = new Vector2(normal3.x, normal3.y).normalized;
        reflective = Vector2.Dot(direction, normal) < 0f;
        return true;
    }

    private static void TryCandidate(
        Vector2 origin,
        Vector2 direction,
        float t,
        float halfAperture,
        ref bool found,
        ref float bestX,
        ref float distance,
        ref Vector2 point)
    {
        Vector2 candidate = origin + direction * t;

        if (t <= 0.0001f || Mathf.Abs(candidate.y) > halfAperture)
        {
            return;
        }

        if (!found || candidate.x > bestX)
        {
            found = true;
            bestX = candidate.x;
            distance = t;
            point = candidate;
        }
    }
}
