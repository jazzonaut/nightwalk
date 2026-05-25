using Godot;
namespace Nightwalk.Godot.Physics;

/// <summary>
/// Result of a raycast operation.
/// </summary>
public readonly struct RaycastResult
{
    /// <summary>
    /// World position where the ray hit.
    /// </summary>
    public Vector3 Position { get; init; }

    /// <summary>
    /// Surface normal at the hit point.
    /// </summary>
    public Vector3 Normal { get; init; }

    /// <summary>
    /// The collider that was hit.
    /// </summary>
    public GodotObject Collider { get; init; }

    /// <summary>
    /// Instance ID of the collider.
    /// </summary>
    public ulong ColliderId { get; init; }
}

/// <summary>
/// Utility class for performing raycasts.
/// </summary>
public static class RaycastHelper
{
    /// <summary>
    /// Performs a raycast from camera center into the scene.
    /// </summary>
    /// <param name="camera">The camera to cast from.</param>
    /// <param name="spaceState">The physics space state.</param>
    /// <param name="maxDistance">Maximum raycast distance.</param>
    /// <param name="collisionMask">Collision layer mask (default: all layers).</param>
    /// <returns>Raycast result, or null if nothing was hit.</returns>
    public static RaycastResult? CameraRaycast(
        Camera3D camera,
        PhysicsDirectSpaceState3D spaceState,
        float maxDistance,
        uint collisionMask = uint.MaxValue)
    {
        var from = camera.GlobalPosition;
        var forward = -camera.GlobalTransform.Basis.Z;
        var to = from + forward * maxDistance;

        return Raycast(spaceState, from, to, collisionMask);
    }

    /// <summary>
    /// Performs a raycast from camera center into the scene, excluding specific RIDs.
    /// </summary>
    public static RaycastResult? CameraRaycastExclude(
        Camera3D camera,
        PhysicsDirectSpaceState3D spaceState,
        float maxDistance,
        global::Godot.Collections.Array<Rid> exclude,
        uint collisionMask = uint.MaxValue)
    {
        var from = camera.GlobalPosition;
        var forward = -camera.GlobalTransform.Basis.Z;
        var to = from + forward * maxDistance;

        return RaycastExclude(spaceState, from, to, exclude, collisionMask);
    }

    /// <summary>
    /// Performs a raycast between two points.
    /// </summary>
    /// <param name="spaceState">The physics space state.</param>
    /// <param name="from">Ray origin.</param>
    /// <param name="to">Ray end point.</param>
    /// <param name="collisionMask">Collision layer mask (default: all layers).</param>
    /// <returns>Raycast result, or null if nothing was hit.</returns>
    public static RaycastResult? Raycast(
        PhysicsDirectSpaceState3D spaceState,
        Vector3 from,
        Vector3 to,
        uint collisionMask = uint.MaxValue)
    {
        var query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        var result = spaceState.IntersectRay(query);

        if (result == null || result.Count == 0) return null;

        return new RaycastResult
        {
            Position = (Vector3)result["position"],
            Normal = (Vector3)result["normal"],
            Collider = (GodotObject)result["collider"],
            ColliderId = (ulong)result["collider_id"]
        };
    }

    /// <summary>
    /// Performs a raycast between two points, excluding specific RIDs.
    /// </summary>
    public static RaycastResult? RaycastExclude(
        PhysicsDirectSpaceState3D spaceState,
        Vector3 from,
        Vector3 to,
        global::Godot.Collections.Array<Rid> exclude,
        uint collisionMask = uint.MaxValue)
    {
        var query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;
        query.Exclude = exclude;

        var result = spaceState.IntersectRay(query);

        if (result == null || result.Count == 0) return null;

        return new RaycastResult
        {
            Position = (Vector3)result["position"],
            Normal = (Vector3)result["normal"],
            Collider = (GodotObject)result["collider"],
            ColliderId = (ulong)result["collider_id"]
        };
    }

    /// <summary>
    /// Recursively collects all collision RIDs from a node and its descendants.
    /// </summary>
    /// <param name="node">The root node to collect from.</param>
    /// <param name="rids">The collection to add RIDs to.</param>
    public static void CollectCollisionRids(Node node, global::Godot.Collections.Array<Rid> rids)
    {
        if (node is CollisionObject3D collisionObject)
        {
            rids.Add(collisionObject.GetRid());
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                CollectCollisionRids(childNode, rids);
            }
        }
    }

    /// <summary>
    /// Calculates the distance from a point to a ray, and the parameter t along the ray.
    /// </summary>
    /// <param name="rayOrigin">Ray origin.</param>
    /// <param name="rayDir">Ray direction (must be normalized).</param>
    /// <param name="point">Target point.</param>
    /// <param name="maxT">Maximum t value (for segment bounds). Use float.MaxValue for infinite ray.</param>
    /// <returns>(distance to ray, t parameter along ray)</returns>
    public static (float distance, float t) PointToRayDistance(
        Vector3 rayOrigin, Vector3 rayDir, Vector3 point, float maxT = float.MaxValue)
    {
        var toPoint = point - rayOrigin;
        var t = Mathf.Clamp(toPoint.Dot(rayDir), 0f, maxT);
        var closestPoint = rayOrigin + rayDir * t;
        return (point.DistanceTo(closestPoint), t);
    }

    /// <summary>
    /// Calculates the shortest distance between two line segments in 3D.
    /// Returns the distance and the closest point on the second segment.
    /// </summary>
    /// <remarks>
    /// Handles degenerate segments and clamps both segment parameters to [0,1] for stability.
    /// </remarks>
    public static float SegmentToSegmentDistance(
        Vector3 p1,
        Vector3 q1,
        Vector3 p2,
        Vector3 q2,
        out Vector3 closestOnSegment2)
    {
        // Shortest distance between two segments in 3D, with clamped parameters and degenerate handling.
        // Returns the distance and the closest point on the second segment.
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r = p1 - p2;

        var a = d1.Dot(d1);
        var e = d2.Dot(d2);
        var f = d2.Dot(r);

        float s;
        float t;

        const float epsilon = 0.000001f;

        if (a <= epsilon && e <= epsilon)
        {
            // Both segments are points.
            s = 0f;
            t = 0f;
        }
        else if (a <= epsilon)
        {
            // First segment is a point; project onto second segment.
            s = 0f;
            t = Mathf.Clamp(f / e, 0f, 1f);
        }
        else
        {
            var c = d1.Dot(r);
            if (e <= epsilon)
            {
                // Second segment is a point; project onto first segment.
                t = 0f;
                s = Mathf.Clamp(-c / a, 0f, 1f);
            }
            else
            {
                var b = d1.Dot(d2);
                var denom = a * e - b * b;
                // Solve for s (closest point on first segment), then clamp to [0,1].
                s = denom != 0f ? Mathf.Clamp((b * f - c * e) / denom, 0f, 1f) : 0f;

                var tNom = b * s + f;
                if (tNom < 0f)
                {
                    // Closest point on second segment is its start.
                    t = 0f;
                    s = Mathf.Clamp(-c / a, 0f, 1f);
                }
                else if (tNom > e)
                {
                    // Closest point on second segment is its end.
                    t = 1f;
                    s = Mathf.Clamp((b - c) / a, 0f, 1f);
                }
                else
                {
                    // Closest point on second segment is inside it.
                    t = tNom / e;
                }
            }
        }

        var closest1 = p1 + d1 * s;
        var closest2 = p2 + d2 * t;
        closestOnSegment2 = closest2;
        return closest1.DistanceTo(closest2);
    }
}
