using System.Collections.Generic;
using UnityEngine;

namespace TechArtLab.LoFi.Editor
{
    // Authoring only: create indexed meshes first, then duplicate exactly the same
    // triangles for flat shading. Normal comparisons never move a vertex.
    public static class LoFiMeshes
    {
        public static Mesh Rounded(int subdivision, bool rock)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var vertices = new List<Vector3> {
                new(-1,t,0),new(1,t,0),new(-1,-t,0),new(1,-t,0),
                new(0,-1,t),new(0,1,t),new(0,-1,-t),new(0,1,-t),
                new(t,0,-1),new(t,0,1),new(-t,0,-1),new(-t,0,1)
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
            var triangles = new List<int> {0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1};
            for (int level = 0; level < subdivision; level++)
            {
                var edges = new Dictionary<long, int>(); var next = new List<int>();
                int Middle(int a, int b)
                {
                    long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    if (edges.TryGetValue(key, out int index)) return index;
                    index = vertices.Count; vertices.Add((vertices[a] + vertices[b]).normalized);
                    edges.Add(key, index); return index;
                }
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    next.AddRange(new[] {a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca});
                }
                triangles = next;
            }
            if (rock) for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 n = vertices[i];
                float radius = 1 + .12f * Mathf.Sin(3 * n.x + 1.2f * n.z) + .08f * n.y * n.z;
                vertices[i] = Vector3.Scale(n * radius, new Vector3(1.1f, .7f, .9f));
            }
            var mesh = new Mesh {name = (rock ? "Rock" : "Canopy") + "_" + subdivision + "_Smooth"};
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        public static Mesh Trunk(int segments)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (int ring = 0; ring < 2; ring++) for (int i = 0; i < segments; i++)
            {
                float theta = i * 2 * Mathf.PI / segments, radius = ring == 0 ? .19f : .12f;
                vertices.Add(new Vector3(Mathf.Cos(theta) * radius, ring, Mathf.Sin(theta) * radius));
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                indices.AddRange(new[] {i, i+segments, j, j, i+segments, j+segments});
            }
            // Separate cap vertices keep cap/side creases even in smooth mode.
            for (int cap = 0; cap < 2; cap++)
            {
                int center = vertices.Count; vertices.Add(new Vector3(0, cap, 0));
                for (int i = 0; i < segments; i++) vertices.Add(vertices[cap * segments + i]);
                for (int i = 0; i < segments; i++)
                {
                    int a = center + 1 + i, b = center + 1 + (i + 1) % segments;
                    indices.AddRange(cap == 0 ? new[] {center, a, b} : new[] {center, b, a});
                }
            }
            var mesh = new Mesh {name = "Trunk_" + segments + "_Smooth"};
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        public static Mesh Flat(Mesh indexed)
        {
            var original = indexed.vertices; var source = indexed.triangles;
            var vertices = new Vector3[source.Length]; var normals = new Vector3[source.Length]; var indices = new int[source.Length];
            for (int i = 0; i < source.Length; i += 3)
            {
                Vector3 a = original[source[i]], b = original[source[i+1]], c = original[source[i+2]];
                Vector3 normal = Vector3.Cross(b-a, c-a).normalized;
                for (int j = 0; j < 3; j++) { vertices[i+j] = original[source[i+j]]; normals[i+j] = normal; indices[i+j] = i+j; }
            }
            return new Mesh {name = indexed.name.Replace("Smooth", "Flat"), vertices = vertices, normals = normals, triangles = indices};
        }
    }
}
