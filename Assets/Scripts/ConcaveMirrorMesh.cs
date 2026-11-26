using System.Collections.Generic;
using UnityEngine;

public static class ConcaveMirrorMesh
{
    public static Mesh Create(float radius, float aperture, float thickness, float depth, int segments = 24)
    {
        radius = Mathf.Max(0.01f, radius);
        aperture = Mathf.Max(0.001f, aperture);
        thickness = Mathf.Max(0.001f, thickness);
        depth = Mathf.Max(0.001f, depth);
        segments = Mathf.Max(2, segments);

        float halfAperture = Mathf.Min(aperture * 0.5f, radius * 0.98f);
        float beta = Mathf.Asin(halfAperture / radius);
        float halfDepth = depth * 0.5f;
        Vector3 center = new Vector3(-radius, 0f, 0f);

        var vertices = new List<Vector3>((segments + 1) * 4);
        var triangles = new List<int>(segments * 24 + 12);

        for (int i = 0; i <= segments; i++)
        {
            float angle = -beta + 2f * beta * i / segments;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 front = center + radial * radius;
            Vector3 back = center + radial * (radius + thickness);

            vertices.Add(front + Vector3.back * halfDepth);
            vertices.Add(front + Vector3.forward * halfDepth);
            vertices.Add(back + Vector3.back * halfDepth);
            vertices.Add(back + Vector3.forward * halfDepth);
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;

            AddQuad(vertices, triangles, a, b, b + 1, a + 1);
            AddQuad(vertices, triangles, a + 2, a + 3, b + 3, b + 2);
            AddQuad(vertices, triangles, a, a + 2, b + 2, b);
            AddQuad(vertices, triangles, a + 1, b + 1, b + 3, a + 3);
        }

        AddQuad(vertices, triangles, 0, 1, 3, 2);
        int last = segments * 4;
        AddQuad(vertices, triangles, last, last + 2, last + 3, last + 1);

        var mesh = new Mesh { name = "ConcaveMirror" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        int a,
        int b,
        int c,
        int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }
}
