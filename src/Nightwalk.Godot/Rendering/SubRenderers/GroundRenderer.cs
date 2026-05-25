using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Rendering.Constants;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Renders the ground plane for a chunk.
/// Creates a collision box and visual mesh at the base level.
/// </summary>
public sealed class GroundRenderer : ISubRenderer, IPhasedSubRenderer
{
    public ChunkRenderPhase Phase => ChunkRenderPhase.Ground;

    public void Render(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        var groundBody = new StaticBody3D { Name = "Ground" };

        // Collision shape - box extending below Y=0
        var collision = new CollisionShape3D();
        var shape = new BoxShape3D
        {
            Size = new Vector3(data.Size, RenderingConstants.GroundCollisionBoxHeight, data.Size)
        };
        collision.Shape = shape;
        collision.Position = new Vector3(
            data.Size / 2f,
            -RenderingConstants.GroundCollisionYOffset,
            data.Size / 2f
        );
        groundBody.AddChild(collision);

        // Visual mesh - lowered slightly to prevent Z-fighting with roads
        var meshInstance = context.NodePool.GetMeshInstance("GroundMesh");
        var mesh = new BoxMesh
        {
            Size = new Vector3(data.Size, RenderingConstants.GroundCollisionBoxHeight, data.Size)
        };
        meshInstance.Mesh = mesh;
        meshInstance.Position = new Vector3(
            data.Size / 2f,
            -RenderingConstants.GroundCollisionYOffset - RenderingConstants.GroundZFightingOffset,
            data.Size / 2f
        );
        meshInstance.MaterialOverride = context.GroundMaterial;
        groundBody.AddChild(meshInstance);

        parent.AddChild(groundBody);
    }

    public bool RenderIncremental(ChunkRenderState state, long budgetUsec)
    {
        if (state.ChunkNode == null || state.Context == null) return true;

        Render(state.ChunkNode, state.Data, state.Context);
        return true; // Always completes in one call
    }
}
