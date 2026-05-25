using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Collectibles;

namespace Nightwalk.Godot.Collectibles;

/// <summary>
/// Generates procedural meshes for all five Platonic solids with optional thick edge cylinders.
/// Oriented to sit upright with a vertex pointing up.
/// </summary>
public static class PolyhedronMeshGenerator
{
    // Cache keyed by (type, radius) for face meshes
    private static readonly Dictionary<(PolyhedronType, float), ArrayMesh> FaceMeshCache = new();

    // Cache keyed by (type, radius, thickness) for edge meshes
    private static readonly Dictionary<(PolyhedronType, float, float), ArrayMesh> EdgeMeshCache = new();

    // Cached oriented vertices per type
    private static readonly Dictionary<PolyhedronType, Vector3[]> OrientedVerticesCache = new();

    // Golden ratio
    private static readonly float Phi = (1f + Mathf.Sqrt(5f)) / 2f;

    #region Polyhedron Data

    /// <summary>
    /// Raw vertices and face/edge definitions for each polyhedron type.
    /// </summary>
    private static class PolyhedronData
    {
        // ============================================================
        // TETRAHEDRON (4 faces, 4 vertices, 6 edges)
        // ============================================================
        public static readonly Vector3[] TetrahedronVertices =
        [
            new Vector3( 1,  1,  1),
            new Vector3( 1, -1, -1),
            new Vector3(-1,  1, -1),
            new Vector3(-1, -1,  1)
        ];

        // 4 triangular faces
        public static readonly int[][] TetrahedronFaces =
        [
            [0, 1, 2],
            [0, 2, 3],
            [0, 3, 1],
            [1, 3, 2]
        ];

        public static readonly int[][] TetrahedronEdges =
        [
            [0, 1], [0, 2], [0, 3],
            [1, 2], [1, 3], [2, 3]
        ];

        // ============================================================
        // CUBE (6 faces, 8 vertices, 12 edges)
        // ============================================================
        public static readonly Vector3[] CubeVertices =
        [
            new Vector3(-1, -1, -1), // 0
            new Vector3( 1, -1, -1), // 1
            new Vector3( 1,  1, -1), // 2
            new Vector3(-1,  1, -1), // 3
            new Vector3(-1, -1,  1), // 4
            new Vector3( 1, -1,  1), // 5
            new Vector3( 1,  1,  1), // 6
            new Vector3(-1,  1,  1)  // 7
        ];

        // 6 quad faces (will be triangulated as fans)
        public static readonly int[][] CubeFaces =
        [
            [0, 3, 2, 1], // back
            [4, 5, 6, 7], // front
            [0, 1, 5, 4], // bottom
            [2, 3, 7, 6], // top
            [0, 4, 7, 3], // left
            [1, 2, 6, 5]  // right
        ];

        public static readonly int[][] CubeEdges =
        [
            [0, 1], [1, 2], [2, 3], [3, 0], // back face
            [4, 5], [5, 6], [6, 7], [7, 4], // front face
            [0, 4], [1, 5], [2, 6], [3, 7]  // connecting
        ];

        // ============================================================
        // OCTAHEDRON (8 faces, 6 vertices, 12 edges)
        // ============================================================
        public static readonly Vector3[] OctahedronVertices =
        [
            new Vector3( 1,  0,  0), // +X
            new Vector3(-1,  0,  0), // -X
            new Vector3( 0,  1,  0), // +Y
            new Vector3( 0, -1,  0), // -Y
            new Vector3( 0,  0,  1), // +Z
            new Vector3( 0,  0, -1)  // -Z
        ];

        // 8 triangular faces
        public static readonly int[][] OctahedronFaces =
        [
            [2, 0, 4], [2, 4, 1], [2, 1, 5], [2, 5, 0], // top 4
            [3, 4, 0], [3, 1, 4], [3, 5, 1], [3, 0, 5]  // bottom 4
        ];

        public static readonly int[][] OctahedronEdges =
        [
            [0, 2], [0, 3], [0, 4], [0, 5],
            [1, 2], [1, 3], [1, 4], [1, 5],
            [2, 4], [2, 5], [3, 4], [3, 5]
        ];

        // ============================================================
        // DODECAHEDRON (12 faces, 20 vertices, 30 edges)
        // ============================================================
        public static readonly Vector3[] DodecahedronVertices =
        [
            // Cube vertices (+-1, +-1, +-1)
            new Vector3( 1,  1,  1),  // 0
            new Vector3( 1,  1, -1),  // 1
            new Vector3( 1, -1,  1),  // 2
            new Vector3( 1, -1, -1),  // 3
            new Vector3(-1,  1,  1),  // 4
            new Vector3(-1,  1, -1),  // 5
            new Vector3(-1, -1,  1),  // 6
            new Vector3(-1, -1, -1),  // 7

            // Green rectangle vertices (0, +-phi, +-1/phi)
            new Vector3( 0,  Phi,  1f/Phi),  // 8
            new Vector3( 0,  Phi, -1f/Phi),  // 9
            new Vector3( 0, -Phi,  1f/Phi),  // 10
            new Vector3( 0, -Phi, -1f/Phi),  // 11

            // Blue rectangle vertices (+-1/phi, 0, +-phi)
            new Vector3( 1f/Phi,  0,  Phi),  // 12
            new Vector3( 1f/Phi,  0, -Phi),  // 13
            new Vector3(-1f/Phi,  0,  Phi),  // 14
            new Vector3(-1f/Phi,  0, -Phi),  // 15

            // Red rectangle vertices (+-phi, +-1/phi, 0)
            new Vector3( Phi,  1f/Phi,  0),  // 16
            new Vector3( Phi, -1f/Phi,  0),  // 17
            new Vector3(-Phi,  1f/Phi,  0),  // 18
            new Vector3(-Phi, -1f/Phi,  0),  // 19
        ];

        // 12 pentagonal faces
        public static readonly int[][] DodecahedronFaces =
        [
            [0, 12, 14, 4, 8],
            [0, 8, 9, 1, 16],
            [0, 16, 17, 2, 12],
            [1, 9, 5, 15, 13],
            [1, 13, 3, 17, 16],
            [2, 17, 3, 11, 10],
            [2, 10, 6, 14, 12],
            [4, 14, 6, 19, 18],
            [4, 18, 5, 9, 8],
            [5, 18, 19, 7, 15],
            [6, 10, 11, 7, 19],
            [3, 13, 15, 7, 11],
        ];

        public static readonly int[][] DodecahedronEdges =
        [
            // Top pentagon edges
            [8, 9], [9, 5], [5, 18], [18, 4], [4, 8],
            // Bottom pentagon edges
            [10, 11], [11, 7], [7, 19], [19, 6], [6, 10],
            // Upper band edges
            [0, 8], [0, 12], [0, 16],
            [1, 9], [1, 13], [1, 16],
            [4, 14], [4, 18],
            [5, 15],
            // Lower band edges
            [2, 10], [2, 12], [2, 17],
            [3, 11], [3, 13], [3, 17],
            [6, 14], [6, 19],
            [7, 15],
            // Vertical connecting edges
            [12, 14], [13, 15], [16, 17], [18, 19],
        ];

        // ============================================================
        // ICOSAHEDRON (20 faces, 12 vertices, 30 edges)
        // ============================================================
        public static readonly Vector3[] IcosahedronVertices =
        [
            new Vector3( 0,  1,  Phi), // 0
            new Vector3( 0,  1, -Phi), // 1
            new Vector3( 0, -1,  Phi), // 2
            new Vector3( 0, -1, -Phi), // 3
            new Vector3( 1,  Phi,  0), // 4
            new Vector3( 1, -Phi,  0), // 5
            new Vector3(-1,  Phi,  0), // 6
            new Vector3(-1, -Phi,  0), // 7
            new Vector3( Phi,  0,  1), // 8
            new Vector3( Phi,  0, -1), // 9
            new Vector3(-Phi,  0,  1), // 10
            new Vector3(-Phi,  0, -1)  // 11
        ];

        // 20 triangular faces
        public static readonly int[][] IcosahedronFaces =
        [
            [0, 2, 8], [0, 8, 4], [0, 4, 6], [0, 6, 10], [0, 10, 2],
            [2, 5, 8], [8, 5, 9], [8, 9, 4], [4, 9, 1], [4, 1, 6],
            [6, 1, 11], [6, 11, 10], [10, 11, 7], [10, 7, 2], [2, 7, 5],
            [3, 5, 7], [3, 9, 5], [3, 1, 9], [3, 11, 1], [3, 7, 11]
        ];

        public static readonly int[][] IcosahedronEdges =
        [
            // Top cap
            [0, 2], [0, 8], [0, 4], [0, 6], [0, 10],
            // Upper ring
            [2, 8], [8, 4], [4, 6], [6, 10], [10, 2],
            // Middle connections
            [2, 5], [8, 5], [8, 9], [4, 9], [4, 1], [6, 1], [6, 11], [10, 11], [10, 7], [2, 7],
            // Lower ring
            [5, 9], [9, 1], [1, 11], [11, 7], [7, 5],
            // Bottom cap
            [3, 5], [3, 9], [3, 1], [3, 11], [3, 7]
        ];
    }

    #endregion

    /// <summary>
    /// Gets a shared face mesh for the specified polyhedron type.
    /// </summary>
    public static ArrayMesh GetMesh(PolyhedronType type, float radius = 1f)
    {
        var key = (type, radius);
        if (FaceMeshCache.TryGetValue(key, out var cached))
            return cached;

        var mesh = CreateFaceMesh(type, radius);
        FaceMeshCache[key] = mesh;
        return mesh;
    }

    /// <summary>
    /// Gets a shared edge mesh for the specified polyhedron type with thick cylinders.
    /// </summary>
    public static ArrayMesh GetEdgeMesh(PolyhedronType type, float radius = 1f, float thickness = 0.04f)
    {
        var key = (type, radius, thickness);
        if (EdgeMeshCache.TryGetValue(key, out var cached))
            return cached;

        var mesh = CreateEdgeMesh(type, radius, thickness);
        EdgeMeshCache[key] = mesh;
        return mesh;
    }

    private static Vector3[] GetRawVertices(PolyhedronType type) => type switch
    {
        PolyhedronType.Tetrahedron => PolyhedronData.TetrahedronVertices,
        PolyhedronType.Cube => PolyhedronData.CubeVertices,
        PolyhedronType.Octahedron => PolyhedronData.OctahedronVertices,
        PolyhedronType.Dodecahedron => PolyhedronData.DodecahedronVertices,
        PolyhedronType.Icosahedron => PolyhedronData.IcosahedronVertices,
        _ => PolyhedronData.DodecahedronVertices
    };

    private static int[][] GetFaces(PolyhedronType type) => type switch
    {
        PolyhedronType.Tetrahedron => PolyhedronData.TetrahedronFaces,
        PolyhedronType.Cube => PolyhedronData.CubeFaces,
        PolyhedronType.Octahedron => PolyhedronData.OctahedronFaces,
        PolyhedronType.Dodecahedron => PolyhedronData.DodecahedronFaces,
        PolyhedronType.Icosahedron => PolyhedronData.IcosahedronFaces,
        _ => PolyhedronData.DodecahedronFaces
    };

    private static int[][] GetEdges(PolyhedronType type) => type switch
    {
        PolyhedronType.Tetrahedron => PolyhedronData.TetrahedronEdges,
        PolyhedronType.Cube => PolyhedronData.CubeEdges,
        PolyhedronType.Octahedron => PolyhedronData.OctahedronEdges,
        PolyhedronType.Dodecahedron => PolyhedronData.DodecahedronEdges,
        PolyhedronType.Icosahedron => PolyhedronData.IcosahedronEdges,
        _ => PolyhedronData.DodecahedronEdges
    };

    private static Vector3[] GetOrientedVertices(PolyhedronType type)
    {
        if (OrientedVerticesCache.TryGetValue(type, out var cached))
            return cached;

        var rawVertices = GetRawVertices(type);

        // Normalize raw vertices
        var normalized = new Vector3[rawVertices.Length];
        for (int i = 0; i < rawVertices.Length; i++)
        {
            normalized[i] = rawVertices[i].Normalized();
        }

        // Find the topmost vertex to point straight up (vertex-up orientation)
        int topVertexIndex = 0;
        float maxY = float.MinValue;
        for (int i = 0; i < normalized.Length; i++)
        {
            if (normalized[i].Y > maxY)
            {
                maxY = normalized[i].Y;
                topVertexIndex = i;
            }
        }

        // Calculate rotation to align top vertex direction with +Y
        Vector3 topVertexDir = normalized[topVertexIndex].Normalized();
        Vector3 targetUp = Vector3.Up;
        var rotation = GetRotationBetweenVectors(topVertexDir, targetUp);

        // Apply rotation to all vertices
        var orientedVertices = new Vector3[normalized.Length];
        for (int i = 0; i < normalized.Length; i++)
        {
            orientedVertices[i] = rotation * normalized[i];
        }

        OrientedVerticesCache[type] = orientedVertices;
        return orientedVertices;
    }

    private static Quaternion GetRotationBetweenVectors(Vector3 from, Vector3 to)
    {
        from = from.Normalized();
        to = to.Normalized();

        float dot = from.Dot(to);

        if (dot > 0.9999f)
            return Quaternion.Identity;

        if (dot < -0.9999f)
        {
            // Vectors are opposite, need arbitrary perpendicular axis
            Vector3 axis = Vector3.Right.Cross(from);
            if (axis.LengthSquared() < 0.0001f)
                axis = Vector3.Up.Cross(from);
            return new Quaternion(axis.Normalized(), Mathf.Pi);
        }

        Vector3 cross = from.Cross(to);
        float w = Mathf.Sqrt(from.LengthSquared() * to.LengthSquared()) + dot;

        return new Quaternion(cross.X, cross.Y, cross.Z, w).Normalized();
    }

    private static Vector3[] GetScaledVertices(PolyhedronType type, float radius)
    {
        var oriented = GetOrientedVertices(type);
        var vertices = new Vector3[oriented.Length];
        for (int i = 0; i < oriented.Length; i++)
        {
            vertices[i] = oriented[i] * radius;
        }
        return vertices;
    }

    private static ArrayMesh CreateFaceMesh(PolyhedronType type, float radius)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var vertices = GetScaledVertices(type, radius);
        var faces = GetFaces(type);

        foreach (var face in faces)
        {
            // Calculate face center and normal
            Vector3 center = Vector3.Zero;
            foreach (int idx in face)
                center += vertices[idx];
            center /= face.Length;

            Vector3 normal = center.Normalized();

            // Check winding order
            Vector3 edge1 = vertices[face[1]] - vertices[face[0]];
            Vector3 edge2 = vertices[face[2]] - vertices[face[0]];
            Vector3 cross = edge1.Cross(edge2);
            bool reverse = cross.Dot(normal) < 0;

            // Fan triangulation
            for (int i = 1; i < face.Length - 1; i++)
            {
                st.SetNormal(normal);
                if (reverse)
                {
                    st.AddVertex(vertices[face[0]]);
                    st.AddVertex(vertices[face[i + 1]]);
                    st.AddVertex(vertices[face[i]]);
                }
                else
                {
                    st.AddVertex(vertices[face[0]]);
                    st.AddVertex(vertices[face[i]]);
                    st.AddVertex(vertices[face[i + 1]]);
                }
            }
        }

        return st.Commit();
    }

    private static ArrayMesh CreateEdgeMesh(PolyhedronType type, float radius, float thickness)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var vertices = GetScaledVertices(type, radius);
        var edges = GetEdges(type);
        float cylinderRadius = radius * thickness;
        const int segments = 6; // Hexagonal cross-section for edges

        foreach (var edge in edges)
        {
            Vector3 start = vertices[edge[0]];
            Vector3 end = vertices[edge[1]];
            AddCylinder(st, start, end, cylinderRadius, segments);
        }

        return st.Commit();
    }

    private static void AddCylinder(SurfaceTool st, Vector3 start, Vector3 end, float radius, int segments)
    {
        Vector3 direction = (end - start).Normalized();

        // Find perpendicular vectors for cylinder cross-section
        Vector3 perpendicular = direction.Cross(Vector3.Up);
        if (perpendicular.LengthSquared() < 0.001f)
            perpendicular = direction.Cross(Vector3.Right);
        perpendicular = perpendicular.Normalized();

        Vector3 perpendicular2 = direction.Cross(perpendicular).Normalized();

        // Generate cylinder vertices
        var startRing = new Vector3[segments];
        var endRing = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            Vector3 offset = (perpendicular * Mathf.Cos(angle) + perpendicular2 * Mathf.Sin(angle)) * radius;
            startRing[i] = start + offset;
            endRing[i] = end + offset;
        }

        // Generate cylinder triangles
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            // Calculate normal for this quad (pointing outward)
            Vector3 normal = ((startRing[i] - start) + (startRing[next] - start)).Normalized();

            // First triangle
            st.SetNormal(normal);
            st.AddVertex(startRing[i]);
            st.AddVertex(endRing[i]);
            st.AddVertex(startRing[next]);

            // Second triangle
            st.SetNormal(normal);
            st.AddVertex(startRing[next]);
            st.AddVertex(endRing[i]);
            st.AddVertex(endRing[next]);
        }
    }
}
