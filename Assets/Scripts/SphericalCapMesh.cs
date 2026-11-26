using System.Collections.Generic;
using UnityEngine;

public static class SphericalCapMesh
{
    // The indicator lamp is a shallow spherical cap sitting on the lock surface.
    // Its height is one quarter of a full sphere's height (h = R / 2); with a unit
    // base radius that gives h = 1 / sqrt(3). Scaling the mesh uniformly by the
    // desired base radius therefore also scales the height correctly.
    private const int Segments = 24;
    private const int Rings = 6;

    private static Mesh unit;

    public static Mesh Unit
    {
        get
        {
            if (unit == null)
            {
                unit = CreateUnit();
            }

            return unit;
        }
    }

    private static Mesh CreateUnit()
    {
        float sphereRadius = 2f / Mathf.Sqrt(3f);
        float height = sphereRadius * 0.5f;
        float centerZ = height - sphereRadius;
        float maxPolar = Mathf.Acos((sphereRadius - height) / sphereRadius);

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        int pole = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, height));

        int firstRing = vertices.Count;

        for (int ring = 1; ring <= Rings; ring++)
        {
            float polar = maxPolar * ring / Rings;
            float ringRadius = sphereRadius * Mathf.Sin(polar);
            float z = centerZ + sphereRadius * Mathf.Cos(polar);

            for (int segment = 0; segment < Segments; segment++)
            {
                float angle = Mathf.PI * 2f * segment / Segments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    Mathf.Sin(angle) * ringRadius,
                    z));
            }
        }

        for (int segment = 0; segment < Segments; segment++)
        {
            int current = firstRing + segment;
            int next = firstRing + (segment + 1) % Segments;
            triangles.Add(pole);
            triangles.Add(current);
            triangles.Add(next);
        }

        for (int ring = 0; ring < Rings - 1; ring++)
        {
            int upper = firstRing + ring * Segments;
            int lower = upper + Segments;

            for (int segment = 0; segment < Segments; segment++)
            {
                int a = upper + segment;
                int b = upper + (segment + 1) % Segments;
                int c = lower + (segment + 1) % Segments;
                int d = lower + segment;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(a);
                triangles.Add(d);
                triangles.Add(c);
            }
        }

        var mesh = new Mesh { name = "IndicatorSphericalCap" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
