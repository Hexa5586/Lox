using UnityEngine;

[DisallowMultipleComponent]
public class LaserPrism : MonoBehaviour
{
    public const int ComponentR = 0;
    public const int ComponentG = 1;
    public const int ComponentB = 2;

    [SerializeField] private float baseWidth = 0.16f;
    [SerializeField] private float height = 0.138564f;
    [SerializeField] private float depth = 0.10f;
    [SerializeField] private float iorR = 1.42f;
    [SerializeField] private float iorG = 1.52f;
    [SerializeField] private float iorB = 1.62f;

    public float BaseWidth => baseWidth;
    public float Height => height;
    public float Depth => depth;

    public void Configure(
        float width,
        float prismHeight,
        float prismDepth,
        float redIndex,
        float greenIndex,
        float blueIndex)
    {
        baseWidth = width > 0f ? width : 0.16f;
        height = prismHeight > 0f ? prismHeight : 0.138564f;
        depth = prismDepth > 0f ? prismDepth : 0.10f;
        iorR = redIndex > 1f ? redIndex : 1.42f;
        iorG = greenIndex > 1f ? greenIndex : 1.52f;
        iorB = blueIndex > 1f ? blueIndex : 1.62f;
    }

    public float IndexFor(int component)
    {
        switch (component)
        {
            case ComponentR:
                return iorR;

            case ComponentG:
                return iorG;

            default:
                return iorB;
        }
    }

    /// <summary>
    /// Returns the nearest crossing of the prism's triangular cross-section.
    /// The normal points out of the solid so the caller can decide whether the
    /// beam is entering or leaving.
    /// </summary>
    public bool TryIntersect(
        Vector2 origin,
        Vector2 direction,
        out float distance,
        out Vector2 outwardNormal)
    {
        distance = 0f;
        outwardNormal = Vector2.up;

        Quaternion inverseRotation = Quaternion.Inverse(transform.localRotation);
        Vector3 localOrigin3 = inverseRotation * (new Vector3(origin.x, origin.y, 0f) - transform.localPosition);
        Vector3 localDirection3 = inverseRotation * new Vector3(direction.x, direction.y, 0f);

        var localOrigin = new Vector2(localOrigin3.x, localOrigin3.y);
        var localDirection = new Vector2(localDirection3.x, localDirection3.y);

        if (localDirection.sqrMagnitude < 0.0000001f)
        {
            return false;
        }

        float halfWidth = baseWidth * 0.5f;

        // Equilateral triangle centered on its centroid/circumcenter so the
        // transform position matches the prism's center.
        var a = new Vector2(-halfWidth, -height / 3f);
        var b = new Vector2(halfWidth, -height / 3f);
        var c = new Vector2(0f, height * 2f / 3f);

        float nearest = float.PositiveInfinity;
        Vector2 nearestNormal = Vector2.up;

        TryEdgeHit(localOrigin, localDirection, a, b, ref nearest, ref nearestNormal);
        TryEdgeHit(localOrigin, localDirection, b, c, ref nearest, ref nearestNormal);
        TryEdgeHit(localOrigin, localDirection, c, a, ref nearest, ref nearestNormal);

        if (float.IsInfinity(nearest))
        {
            return false;
        }

        Vector3 worldNormal3 = transform.localRotation * new Vector3(nearestNormal.x, nearestNormal.y, 0f);
        outwardNormal = new Vector2(worldNormal3.x, worldNormal3.y).normalized;
        distance = nearest;
        return true;
    }

    public static int ComponentIndex(LightColor color)
    {
        if (color.R >= color.G && color.R >= color.B)
        {
            return ComponentR;
        }

        return color.G >= color.B ? ComponentG : ComponentB;
    }

    public static LightColor ComponentColor(LightColor color, int component)
    {
        switch (component)
        {
            case ComponentR:
                return new LightColor(color.R, 0f, 0f);

            case ComponentG:
                return new LightColor(0f, color.G, 0f);

            default:
                return new LightColor(0f, 0f, color.B);
        }
    }

    /// <summary>
    /// Snell refraction. The normal must point from the interface into the
    /// medium the incident ray is leaving (dot(incident, normal) &lt;= 0).
    /// Returns false for total internal reflection.
    /// </summary>
    public static bool Refract(
        Vector2 incident,
        Vector2 normalAgainstIncident,
        float eta,
        out Vector2 outgoing)
    {
        float cosI = -Vector2.Dot(incident, normalAgainstIncident);

        if (cosI < 0f)
        {
            outgoing = incident;
            return false;
        }

        float sin2T = eta * eta * (1f - cosI * cosI);
        outgoing = incident;

        if (sin2T > 1f)
        {
            return false;
        }

        float cosT = Mathf.Sqrt(Mathf.Max(0f, 1f - sin2T));
        Vector2 refracted = eta * incident + (eta * cosI - cosT) * normalAgainstIncident;
        outgoing = refracted.normalized;
        return true;
    }

    private static bool TryEdgeHit(
        Vector2 origin,
        Vector2 direction,
        Vector2 a,
        Vector2 b,
        ref float nearest,
        ref Vector2 nearestNormal)
    {
        Vector2 edge = b - a;
        float denominator = Cross(direction, edge);

        if (Mathf.Abs(denominator) < 0.0000001f)
        {
            return false;
        }

        Vector2 toEdge = a - origin;
        float t = Cross(toEdge, edge) / denominator;
        float u = Cross(toEdge, direction) / denominator;

        if (t <= 0.00001f || u < -1e-5f || u > 1f + 1e-5f || t >= nearest)
        {
            return false;
        }

        // The triangle is counter-clockwise in local XY, so (edge.y, -edge.x)
        // points away from the solid.
        Vector2 outward = new Vector2(edge.y, -edge.x).normalized;
        nearest = t;
        nearestNormal = outward;
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}
