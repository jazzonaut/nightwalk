using System;

namespace Nightwalk.Core.Collectibles;

/// <summary>
/// The five regular Platonic solids, with values representing face count.
/// </summary>
public enum PolyhedronType
{
    Tetrahedron = 4,
    Cube = 6,
    Octahedron = 8,
    Dodecahedron = 12,
    Icosahedron = 20
}

/// <summary>
/// Extension methods for PolyhedronType.
/// </summary>
public static class PolyhedronTypeExtensions
{
    private static readonly PolyhedronType[] AllTypes =
    [
        PolyhedronType.Tetrahedron,
        PolyhedronType.Cube,
        PolyhedronType.Octahedron,
        PolyhedronType.Dodecahedron,
        PolyhedronType.Icosahedron
    ];

    /// <summary>
    /// Gets all polyhedron types with face counts in the specified range (inclusive).
    /// </summary>
    public static PolyhedronType[] GetTypesInRange(int minFaces, int maxFaces)
    {
        var count = 0;
        foreach (var type in AllTypes)
        {
            var faces = (int)type;
            if (faces >= minFaces && faces <= maxFaces)
                count++;
        }

        if (count == 0)
            return Array.Empty<PolyhedronType>();

        var result = new PolyhedronType[count];
        var index = 0;
        foreach (var type in AllTypes)
        {
            var faces = (int)type;
            if (faces >= minFaces && faces <= maxFaces)
                result[index++] = type;
        }

        return result;
    }

    /// <summary>
    /// Gets the number of faces for this polyhedron type.
    /// </summary>
    public static int GetFaceCount(this PolyhedronType type) => (int)type;

    /// <summary>
    /// Gets the number of vertices for this polyhedron type.
    /// </summary>
    public static int GetVertexCount(this PolyhedronType type) => type switch
    {
        PolyhedronType.Tetrahedron => 4,
        PolyhedronType.Cube => 8,
        PolyhedronType.Octahedron => 6,
        PolyhedronType.Dodecahedron => 20,
        PolyhedronType.Icosahedron => 12,
        _ => 0
    };

    /// <summary>
    /// Gets the number of edges for this polyhedron type.
    /// </summary>
    public static int GetEdgeCount(this PolyhedronType type) => type switch
    {
        PolyhedronType.Tetrahedron => 6,
        PolyhedronType.Cube => 12,
        PolyhedronType.Octahedron => 12,
        PolyhedronType.Dodecahedron => 30,
        PolyhedronType.Icosahedron => 30,
        _ => 0
    };
}
