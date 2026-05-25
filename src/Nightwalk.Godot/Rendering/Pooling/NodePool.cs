using System.Collections.Generic;
using Godot;
using Nightwalk.Godot.Rendering.Constants;

namespace Nightwalk.Godot.Rendering.Pooling;

/// <summary>
/// Object pool for Godot nodes to reduce allocation/GC pressure during chunk load/unload cycles.
/// </summary>
public sealed class NodePool
{
    private readonly Stack<Node3D> _nodePool = new();
    private readonly Stack<MeshInstance3D> _meshInstancePool = new();

    /// <summary>
    /// Gets a Node3D from the pool or creates a new one.
    /// </summary>
    public Node3D GetNode()
    {
        return _nodePool.Count > 0 ? _nodePool.Pop() : new Node3D();
    }

    /// <summary>
    /// Gets a MeshInstance3D from the pool or creates a new one.
    /// Resets transform to identity.
    /// </summary>
    public MeshInstance3D GetMeshInstance(string name)
    {
        MeshInstance3D instance;
        if (_meshInstancePool.Count > 0)
        {
            instance = _meshInstancePool.Pop();
            instance.Transform = Transform3D.Identity;
        }
        else
        {
            instance = new MeshInstance3D();
        }
        instance.Name = name;
        return instance;
    }

    /// <summary>
    /// Returns a chunk node to the pool, recycling its mesh instances.
    /// </summary>
    public void ReturnNode(Node3D node)
    {
        // Validate node is still valid (may have been freed externally)
        if (!GodotObject.IsInstanceValid(node))
            return;

        node.GetParent()?.RemoveChild(node);

        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D meshInstance)
            {
                ReturnMeshInstance(meshInstance);
            }
            else if (child is Node childNode)
            {
                ReturnNestedMeshInstances(childNode);
                childNode.QueueFree();
            }
        }

        if (_nodePool.Count < RenderingConstants.NodePoolCapacity)
        {
            _nodePool.Push(node);
        }
        else
        {
            node.QueueFree();
        }
    }

    /// <summary>
    /// Returns a mesh instance to the pool.
    /// </summary>
    public void ReturnMeshInstance(MeshInstance3D meshInstance)
    {
        // Validate node is still valid (may have been freed externally)
        if (!GodotObject.IsInstanceValid(meshInstance))
            return;

        meshInstance.GetParent()?.RemoveChild(meshInstance);
        meshInstance.Mesh = null;
        meshInstance.MaterialOverride = null;

        // Reset visibility range (may have been set by neon renderer)
        meshInstance.VisibilityRangeEnd = 0;
        meshInstance.VisibilityRangeEndMargin = 0;
        meshInstance.VisibilityRangeBegin = 0;
        meshInstance.VisibilityRangeBeginMargin = 0;

        if (_meshInstancePool.Count < RenderingConstants.MeshInstancePoolCapacity)
        {
            _meshInstancePool.Push(meshInstance);
        }
        else
        {
            meshInstance.QueueFree();
        }
    }

    /// <summary>
    /// Recursively returns nested mesh instances to the pool.
    /// </summary>
    private void ReturnNestedMeshInstances(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is MeshInstance3D meshInstance)
            {
                ReturnMeshInstance(meshInstance);
            }
            else if (child is Node childNode)
            {
                ReturnNestedMeshInstances(childNode);
            }
        }
    }

    /// <summary>
    /// Cleans up all pooled nodes.
    /// </summary>
    public void Cleanup()
    {
        while (_nodePool.Count > 0)
        {
            _nodePool.Pop().QueueFree();
        }

        while (_meshInstancePool.Count > 0)
        {
            _meshInstancePool.Pop().QueueFree();
        }
    }
}
