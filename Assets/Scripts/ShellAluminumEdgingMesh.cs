using System.Collections.Generic;
using UnityEngine;

public static class ShellAluminumEdgingMesh
{
    // Creates the soft-metal edging with rounded outer edges and rounded window
    // corners. The acrylic shell itself stays inside the wrap; its sharp edges
    // are hidden by the rounded metal profile.
    public static Mesh Create(
        float halfExtent,
        float bandWidth,
        float thickness,
        float edgeRadius = 0.018f,
        float innerCornerRadius = 0.03f)
    {
        float h = Mathf.Max(0.01f, halfExtent);
        float t = Mathf.Max(0.001f, thickness);
        float band = Mathf.Clamp(bandWidth, 0.001f, h * 0.45f);
        float radius = Mathf.Clamp(edgeRadius, t * 0.5f, band * 0.45f);
        float corner = Mathf.Clamp(innerCornerRadius, 0.001f, band * 0.7f);

        float innerHalf = h - Mathf.Max(0.001f, band - t);
        float outerHalf = h + t - radius;
        const int ringSegments = 96;

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        // Six flat face frames with rounded window corners.
        for (int axis = 0; axis < 3; axis++)
        {
            int uAxis = (axis + 1) % 3;
            int vAxis = (axis + 2) % 3;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                AddFaceRing(
                    vertices, normals, triangles,
                    axis, sign, uAxis, vAxis,
                    h, innerHalf, outerHalf, t, corner, ringSegments);
            }
        }

        // Twelve rounded edge bars.
        for (int a = 0; a < 3; a++)
        {
            for (int b = a + 1; b < 3; b++)
            {
                int edgeAxis = 3 - a - b;

                for (int signA = -1; signA <= 1; signA += 2)
                {
                    for (int signB = -1; signB <= 1; signB += 2)
                    {
                        AddEdgeBar(
                            vertices, normals, triangles,
                            a, b, edgeAxis,
                            signA, signB, outerHalf, radius);
                    }
                }
            }
        }

        // Eight rounded outer corners.
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddCornerBall(
                        vertices, normals, triangles,
                        new Vector3(sx * outerHalf, sy * outerHalf, sz * outerHalf),
                        radius);
                }
            }
        }

        // Fill the small trihedral recesses where the three face frames meet
        // inside each cube corner.
        float cornerSize = Mathf.Max(0.001f, outerHalf - innerHalf);
        float cornerCenter = (outerHalf + innerHalf) * 0.5f;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddSolidBox(
                        vertices, normals, triangles,
                        new Vector3(sx * cornerCenter, sy * cornerCenter, sz * cornerCenter),
                        Vector3.one * cornerSize);
                }
            }
        }

        var mesh = new Mesh { name = "ShellAluminumEdging" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddFaceRing(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        int axis,
        int sign,
        int uAxis,
        int vAxis,
        float halfExtent,
        float innerHalf,
        float outerHalf,
        float thickness,
        float cornerRadius,
        int segments)
    {
        Vector3 faceNormal = Vector3.zero;
        faceNormal[axis] = sign;

        float topPlane = sign * (halfExtent + thickness);
        float bottomPlane = sign * halfExtent;

        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.PI * 2f * i / segments;
            float a1 = Mathf.PI * 2f * (i + 1) / segments;

            Vector2 outer0 = BoundaryPoint(outerHalf, outerHalf, 0f, a0);
            Vector2 outer1 = BoundaryPoint(outerHalf, outerHalf, 0f, a1);
            Vector2 inner0 = BoundaryPoint(innerHalf, innerHalf, cornerRadius, a0);
            Vector2 inner1 = BoundaryPoint(innerHalf, innerHalf, cornerRadius, a1);

            Vector3 outerTop0 = Map(axis, uAxis, vAxis, topPlane, outer0);
            Vector3 outerTop1 = Map(axis, uAxis, vAxis, topPlane, outer1);
            Vector3 innerTop0 = Map(axis, uAxis, vAxis, topPlane, inner0);
            Vector3 innerTop1 = Map(axis, uAxis, vAxis, topPlane, inner1);
            Vector3 outerBottom0 = Map(axis, uAxis, vAxis, bottomPlane, outer0);
            Vector3 outerBottom1 = Map(axis, uAxis, vAxis, bottomPlane, outer1);
            Vector3 innerBottom0 = Map(axis, uAxis, vAxis, bottomPlane, inner0);
            Vector3 innerBottom1 = Map(axis, uAxis, vAxis, bottomPlane, inner1);

            AddQuad(vertices, normals, triangles,
                outerTop0, faceNormal,
                outerTop1, faceNormal,
                innerTop1, faceNormal,
                innerTop0, faceNormal);

            Vector3 innerNormal0 = MapNormal(uAxis, vAxis,
                -BoundaryNormal(innerHalf, innerHalf, cornerRadius, a0));
            Vector3 innerNormal1 = MapNormal(uAxis, vAxis,
                -BoundaryNormal(innerHalf, innerHalf, cornerRadius, a1));

            AddQuad(vertices, normals, triangles,
                innerTop0, innerNormal0,
                innerTop1, innerNormal1,
                innerBottom1, innerNormal1,
                innerBottom0, innerNormal0);

            Vector3 outerNormal0 = MapNormal(uAxis, vAxis,
                BoundaryNormal(outerHalf, outerHalf, 0f, a0));
            Vector3 outerNormal1 = MapNormal(uAxis, vAxis,
                BoundaryNormal(outerHalf, outerHalf, 0f, a1));

            AddQuad(vertices, normals, triangles,
                outerTop0, outerNormal0,
                outerTop1, outerNormal1,
                outerBottom1, outerNormal1,
                outerBottom0, outerNormal0);
        }
    }

    private static void AddEdgeBar(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        int axisA,
        int axisB,
        int edgeAxis,
        int signA,
        int signB,
        float outerHalf,
        float radius)
    {
        const int segments = 24;
        Vector3 center = Vector3.zero;
        center[axisA] = signA * outerHalf;
        center[axisB] = signB * outerHalf;

        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.PI * 2f * i / segments;
            float a1 = Mathf.PI * 2f * (i + 1) / segments;

            Vector3 normal0 = Vector3.zero;
            Vector3 normal1 = Vector3.zero;
            normal0[axisA] = Mathf.Cos(a0);
            normal0[axisB] = Mathf.Sin(a0);
            normal1[axisA] = Mathf.Cos(a1);
            normal1[axisB] = Mathf.Sin(a1);

            Vector3 low0 = center + normal0 * radius;
            Vector3 low1 = center + normal1 * radius;
            Vector3 high0 = low0;
            Vector3 high1 = low1;
            low0[edgeAxis] = -outerHalf;
            low1[edgeAxis] = -outerHalf;
            high0[edgeAxis] = outerHalf;
            high1[edgeAxis] = outerHalf;

            AddQuad(vertices, normals, triangles,
                low0, normal0,
                high0, normal0,
                high1, normal1,
                low1, normal1);
        }
    }

    private static void AddCornerBall(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        Vector3 center,
        float radius)
    {
        const int longitude = 16;
        const int latitude = 10;

        for (int lat = 0; lat < latitude; lat++)
        {
            float v0 = Mathf.PI * lat / latitude - Mathf.PI * 0.5f;
            float v1 = Mathf.PI * (lat + 1) / latitude - Mathf.PI * 0.5f;

            for (int lon = 0; lon < longitude; lon++)
            {
                float u0 = Mathf.PI * 2f * lon / longitude;
                float u1 = Mathf.PI * 2f * (lon + 1) / longitude;

                Vector3 n00 = SphericalNormal(v0, u0);
                Vector3 n01 = SphericalNormal(v0, u1);
                Vector3 n10 = SphericalNormal(v1, u0);
                Vector3 n11 = SphericalNormal(v1, u1);

                AddQuad(vertices, normals, triangles,
                    center + n00 * radius, n00,
                    center + n10 * radius, n10,
                    center + n11 * radius, n11,
                    center + n01 * radius, n01);
            }
        }
    }

    private static Vector3 SphericalNormal(float latitude, float longitude)
    {
        float cosLat = Mathf.Cos(latitude);
        return new Vector3(
            cosLat * Mathf.Cos(longitude),
            Mathf.Sin(latitude),
            cosLat * Mathf.Sin(longitude));
    }

    private static Vector3 Map(int axis, int uAxis, int vAxis, float plane, Vector2 point)
    {
        Vector3 result = Vector3.zero;
        result[axis] = plane;
        result[uAxis] = point.x;
        result[vAxis] = point.y;
        return result;
    }

    private static Vector3 MapNormal(int uAxis, int vAxis, Vector2 normal)
    {
        Vector3 result = Vector3.zero;
        result[uAxis] = normal.x;
        result[vAxis] = normal.y;
        return result;
    }

    private static Vector2 BoundaryPoint(float ex, float ey, float radius, float angle)
    {
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        if (radius <= 0.0001f)
        {
            float ax = Mathf.Abs(direction.x);
            float ay = Mathf.Abs(direction.y);
            float tx = ax > 1e-6f ? ex / ax : float.MaxValue;
            float ty = ay > 1e-6f ? ey / ay : float.MaxValue;
            return direction * Mathf.Min(tx, ty);
        }

        float coreX = ex - radius;
        float coreY = ey - radius;
        float lo = 0f;
        float hi = Mathf.Sqrt(ex * ex + ey * ey) + radius;

        for (int i = 0; i < 24; i++)
        {
            float mid = (lo + hi) * 0.5f;
            Vector2 point = direction * mid;
            Vector2 core = new Vector2(
                Mathf.Clamp(point.x, -coreX, coreX),
                Mathf.Clamp(point.y, -coreY, coreY));
            float distance = (point - core).magnitude;

            if (distance < radius)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return direction * ((lo + hi) * 0.5f);
    }

    private static Vector2 BoundaryNormal(float ex, float ey, float radius, float angle)
    {
        Vector2 point = BoundaryPoint(ex, ey, radius, angle);

        if (radius <= 0.0001f)
        {
            return point.sqrMagnitude > 1e-8f ? point.normalized : new Vector2(1f, 0f);
        }

        float coreX = ex - radius;
        float coreY = ey - radius;
        Vector2 core = new Vector2(
            Mathf.Clamp(point.x, -coreX, coreX),
            Mathf.Clamp(point.y, -coreY, coreY));
        Vector2 normal = point - core;
        return normal.sqrMagnitude > 1e-8f ? normal.normalized : point.normalized;
    }

    private static void AddSolidBox(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        Vector3 center,
        Vector3 size)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;

        AddQuad(vertices, normals, triangles,
            center + new Vector3(hx, -hy, -hz), Vector3.right,
            center + new Vector3(hx, hy, -hz), Vector3.right,
            center + new Vector3(hx, hy, hz), Vector3.right,
            center + new Vector3(hx, -hy, hz), Vector3.right);

        AddQuad(vertices, normals, triangles,
            center + new Vector3(-hx, -hy, hz), Vector3.left,
            center + new Vector3(-hx, hy, hz), Vector3.left,
            center + new Vector3(-hx, hy, -hz), Vector3.left,
            center + new Vector3(-hx, -hy, -hz), Vector3.left);

        AddQuad(vertices, normals, triangles,
            center + new Vector3(-hx, hy, -hz), Vector3.up,
            center + new Vector3(-hx, hy, hz), Vector3.up,
            center + new Vector3(hx, hy, hz), Vector3.up,
            center + new Vector3(hx, hy, -hz), Vector3.up);

        AddQuad(vertices, normals, triangles,
            center + new Vector3(-hx, -hy, hz), Vector3.down,
            center + new Vector3(-hx, -hy, -hz), Vector3.down,
            center + new Vector3(hx, -hy, -hz), Vector3.down,
            center + new Vector3(hx, -hy, hz), Vector3.down);

        AddQuad(vertices, normals, triangles,
            center + new Vector3(-hx, -hy, hz), Vector3.forward,
            center + new Vector3(hx, -hy, hz), Vector3.forward,
            center + new Vector3(hx, hy, hz), Vector3.forward,
            center + new Vector3(-hx, hy, hz), Vector3.forward);

        AddQuad(vertices, normals, triangles,
            center + new Vector3(hx, -hy, -hz), Vector3.back,
            center + new Vector3(-hx, -hy, -hz), Vector3.back,
            center + new Vector3(-hx, hy, -hz), Vector3.back,
            center + new Vector3(hx, hy, -hz), Vector3.back);
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        Vector3 p0,
        Vector3 n0,
        Vector3 p1,
        Vector3 n1,
        Vector3 p2,
        Vector3 n2,
        Vector3 p3,
        Vector3 n3)
    {
        int start = vertices.Count;
        vertices.Add(p0);
        vertices.Add(p1);
        vertices.Add(p2);
        vertices.Add(p3);
        normals.Add(n0);
        normals.Add(n1);
        normals.Add(n2);
        normals.Add(n3);

        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }
}
