using UnityEngine;

public static class PrismMesh
{
    // Isosceles triangle in local XY, extruded along local Z. The base sits at
    // y = -height/2 and the apex at y = +height/2, so the mesh is centered on
    // its bounding box; the prism's transform pivot is the triangle's middle.
    public static Mesh Create(float baseWidth, float height, float depth)
    {
        baseWidth = Mathf.Max(0.01f, baseWidth);
        height = Mathf.Max(0.01f, height);
        depth = Mathf.Max(0.01f, depth);

        float halfWidth = baseWidth * 0.5f;
        float halfDepth = depth * 0.5f;

        var a0 = new Vector3(-halfWidth, -height / 3f, -halfDepth);
        var b0 = new Vector3(halfWidth, -height / 3f, -halfDepth);
        var c0 = new Vector3(0f, height * 2f / 3f, -halfDepth);
        var a1 = new Vector3(-halfWidth, -height / 3f, halfDepth);
        var b1 = new Vector3(halfWidth, -height / 3f, halfDepth);
        var c1 = new Vector3(0f, height * 2f / 3f, halfDepth);

        var vertices = new[]
        {
            // Front cap
            a0, b0, c0,
            // Back cap
            a1, c1, b1,
            // Side AB
            a0, a1, b0,
            b0, a1, b1,
            // Side BC
            b0, b1, c0,
            c0, b1, c1,
            // Side CA
            c0, c1, a0,
            a0, c1, a1
        };

        var triangles = new int[vertices.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = i;
        }

        var mesh = new Mesh { name = "Prism" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
