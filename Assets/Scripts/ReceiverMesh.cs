using System.Collections.Generic;
using UnityEngine;

public static class ReceiverMesh
{
    // The mesh is built in face-local space: the acceptance plane is local XY and the
    // extrusion goes along local Z. Radius and depth are both one unit so the loader can
    // drive the final size through the object's local scale.
    private const float Radius = 0.5f;
    private const float HalfDepth = 0.5f;

    public static Mesh CreateHalfCylinder(Vector2 pivot, float inwardDegrees, int segments = 16)
    {
        return Create(pivot, inwardDegrees - 90f, 180f, "ReceiverHalfCylinder", segments);
    }

    public static Mesh CreateQuarterCylinder(Vector2 pivot, float inwardDegrees, int segments = 12)
    {
        return Create(pivot, inwardDegrees - 45f, 90f, "ReceiverQuarterCylinder", segments);
    }

    public static Mesh CreateFullCylinder(int segments = 24)
    {
        segments = Mathf.Max(3, segments);

        var vertices = new List<Vector3>((segments + 1) * 2 + 2);
        var triangles = new List<int>(segments * 12 + 12);

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            float x = Mathf.Cos(angle) * Radius;
            float y = Mathf.Sin(angle) * Radius;
            vertices.Add(new Vector3(x, y, HalfDepth));
            vertices.Add(new Vector3(x, y, -HalfDepth));
        }

        int frontCenter = (segments + 1) * 2;
        int backCenter = frontCenter + 1;
        vertices.Add(new Vector3(0f, 0f, HalfDepth));
        vertices.Add(new Vector3(0f, 0f, -HalfDepth));

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            int b = (i + 1) * 2;

            triangles.Add(frontCenter);
            triangles.Add(a);
            triangles.Add(b);

            triangles.Add(backCenter);
            triangles.Add(b + 1);
            triangles.Add(a + 1);

            AddQuad(triangles, a, a + 1, b + 1, b);
        }

        var mesh = new Mesh { name = "ReceiverFullCylinder" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh Create(
        Vector2 pivot,
        float startDegrees,
        float sweepDegrees,
        string name,
        int segments)
    {
        segments = Mathf.Max(2, segments);

        var ring = new List<Vector2>(segments + 1);

        for (int i = 0; i <= segments; i++)
        {
            float angle = (startDegrees + sweepDegrees * i / segments) * Mathf.Deg2Rad;
            ring.Add(pivot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius);
        }

        int count = ring.Count;
        int frontPivot = count * 2;
        int backPivot = frontPivot + 1;

        var vertices = new List<Vector3>(count * 2 + 2);
        var triangles = new List<int>(segments * 12 + 18);

        for (int i = 0; i < count; i++)
        {
            vertices.Add(new Vector3(ring[i].x, ring[i].y, HalfDepth));
            vertices.Add(new Vector3(ring[i].x, ring[i].y, -HalfDepth));
        }

        vertices.Add(new Vector3(pivot.x, pivot.y, HalfDepth));
        vertices.Add(new Vector3(pivot.x, pivot.y, -HalfDepth));

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            int b = (i + 1) * 2;

            // Front and back caps (local +Z is the visible face).
            triangles.Add(frontPivot);
            triangles.Add(a);
            triangles.Add(b);

            triangles.Add(backPivot);
            triangles.Add(b + 1);
            triangles.Add(a + 1);

            // Curved shell.
            AddQuad(triangles, a, a + 1, b + 1, b);
        }

        // Straight sides: the arc endpoints down to the pivot. For a half cylinder the
        // two segments are collinear and together form the flat face against the wall.
        AddQuad(triangles, (count - 1) * 2, (count - 1) * 2 + 1, backPivot, frontPivot);
        AddQuad(triangles, frontPivot, backPivot, 1, 0);

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }
}
