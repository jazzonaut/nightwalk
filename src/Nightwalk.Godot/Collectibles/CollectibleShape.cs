using System;
using Godot;
using Nightwalk.Core.Collectibles;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Collectibles;

/// <summary>
/// A collectible polyhedron shape that floats above rooftops or other locations.
/// Becomes visible when player is nearby, spins and bobs, and triggers collection on contact.
/// </summary>
public sealed partial class CollectibleShape : Node3D
{
    private const string ShaderPath = "res://shaders/collectible_glow.gdshader";

    private readonly CollectibleId _collectibleId;
    private readonly PolyhedronType _shapeType;
    private readonly int _points;
    private readonly Action<CollectibleId, int> _onCollected;
    private readonly Action? _onCollectedStart;

    private CollectiblesConfig _config;
    private MeshInstance3D? _meshInstance;
    private MeshInstance3D? _edgeMeshInstance;
    private OmniLight3D? _light;
    private Area3D? _collisionArea;
    private CollisionShape3D? _collisionShape;
    private SphereShape3D? _sphereShape;
    private ShaderMaterial? _material;
    private StandardMaterial3D? _edgeMaterial;

    private float _baseY;
    private float _timeOffset;
    private bool _collected;
    private float _currentAlpha = -1f; // Sentinel so first UpdateVisibility call always runs.

    public CollectibleId CollectibleId => _collectibleId;
    public PolyhedronType ShapeType => _shapeType;

    public CollectibleShape(
        CollectibleId collectibleId,
        PolyhedronType shapeType,
        int points,
        CollectiblesConfig config,
        Action<CollectibleId, int> onCollected,
        Action? onCollectedStart = null)
    {
        _collectibleId = collectibleId;
        _shapeType = shapeType;
        _points = points;
        _config = config;
        _onCollected = onCollected;
        _onCollectedStart = onCollectedStart;

        // Random time offset for varied pulsing
        _timeOffset = GD.Randf() * 100f;
    }

    public override void _Ready()
    {
        Name = $"Collectible_{_collectibleId}";
        _baseY = Position.Y;

        // Create the mesh
        CreateMesh();

        // Create the edge lines mesh
        CreateEdgeMesh();

        // Create the point light
        CreateLight();

        // Create collision area
        CreateCollisionArea();

        // Start invisible (will be updated based on proximity)
        UpdateVisibility(0f);
    }

    private void CreateMesh()
    {
        var shader = GD.Load<Shader>(ShaderPath);
        _material = new ShaderMaterial { Shader = shader };

        var glowColor = Color.FromHtml(_config.GlowColor);
        _material.SetShaderParameter("glow_color", glowColor);
        _material.SetShaderParameter("emission_strength", _config.GlowIntensity);
        _material.SetShaderParameter("proximity_alpha", 0f);
        _material.SetShaderParameter("time_offset", _timeOffset);

        var mesh = PolyhedronMeshGenerator.GetMesh(_shapeType, _config.Size);

        _meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_meshInstance);
    }

    private void CreateEdgeMesh()
    {
        var edgeColor = Color.FromHtml(_config.EdgeColor);

        _edgeMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = edgeColor
        };

        var edgeMesh = PolyhedronMeshGenerator.GetEdgeMesh(_shapeType, _config.Size, _config.EdgeThickness);

        _edgeMeshInstance = new MeshInstance3D
        {
            Mesh = edgeMesh,
            MaterialOverride = _edgeMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_edgeMeshInstance);
    }

    private void CreateLight()
    {
        var glowColor = Color.FromHtml(_config.GlowColor);

        _light = new OmniLight3D
        {
            LightColor = glowColor,
            LightEnergy = 0f, // Starts invisible
            OmniRange = _config.LightRange,
            OmniAttenuation = 1.5f,
            ShadowEnabled = false
        };
        AddChild(_light);
    }

    private void CreateCollisionArea()
    {
        _collisionArea = new Area3D { Name = "CollisionArea" };

        _sphereShape = new SphereShape3D { Radius = _config.Size * 1.5f };
        _collisionShape = new CollisionShape3D { Shape = _sphereShape };
        _collisionArea.AddChild(_collisionShape);

        // Connect to body_entered signal
        _collisionArea.BodyEntered += OnBodyEntered;

        AddChild(_collisionArea);
    }

    private void OnBodyEntered(Node3D body)
    {
        // Check if it's the player (CharacterBody3D with specific name or type)
        if (_collected) return;

        // Check if the body is a CharacterBody3D (player)
        if (body is CharacterBody3D)
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (_collected) return;
        _collected = true;

        // Disable collision detection (deferred because we're in a signal callback)
        // Use "monitoring" - controls whether this Area detects bodies entering
        _collisionArea?.SetDeferred("monitoring", false);

        // Fire immediate collection hook (e.g., sound) before the effect completes.
        _onCollectedStart?.Invoke();

        // Create lightning effect - collectible hides and beam appears when effect completes
        SpawnCollectionEffect();
    }

    private void SpawnCollectionEffect()
    {
        // Capture everything needed
        var spawnPosition = GlobalPosition;
        var parent = GetParent();
        var collectibleId = _collectibleId;
        var points = _points;
        var onCollected = _onCollected;

        if (parent == null) return;

        // Use effect_color (matches beam color)
        var effectColor = Color.FromHtml(_config.EffectColor);

        // Create effect with completion callback
        var effect = PlasmaGlobeEffect.Create(_config, effectColor, () =>
        {
            // This fires when lightning effect completes - triggers beam spawn
            onCollected?.Invoke(collectibleId, points);
        });

        // Add effect to parent
        parent.AddChild(effect);
        effect.GlobalPosition = spawnPosition;

        // Shrink the shape during the effect
        var shrinkDuration = _config.ParticleDuration * 0.8f;
        var tween = CreateTween();
        tween.SetParallel(true);

        // Shrink meshes
        if (_meshInstance != null)
        {
            tween.TweenProperty(_meshInstance, "scale", Vector3.Zero, shrinkDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
        }
        if (_edgeMeshInstance != null)
        {
            tween.TweenProperty(_edgeMeshInstance, "scale", Vector3.Zero, shrinkDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
        }

        // Fade out the light
        if (_light != null)
        {
            tween.TweenProperty(_light, "light_energy", 0f, shrinkDuration);
        }

        // Queue free after effect completes
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(QueueFree)).SetDelay(_config.ParticleDuration - shrinkDuration);
    }

    /// <summary>
    /// Updates the collectible based on player proximity.
    /// </summary>
    public void UpdateProximity(Vector3 playerPosition)
    {
        if (_collected) return;

        var distance = GlobalPosition.DistanceTo(playerPosition);
        var fadeStart = _config.VisibilityDistance;
        var fadeEnd = fadeStart - _config.VisibilityFadeMargin;

        // Calculate alpha (1 when close, 0 when far)
        float alpha;
        if (distance <= fadeEnd)
            alpha = 1f;
        else if (distance >= fadeStart)
            alpha = 0f;
        else
            alpha = 1f - (distance - fadeEnd) / _config.VisibilityFadeMargin;

        UpdateVisibility(alpha);
    }

    private void UpdateVisibility(float alpha)
    {
        // Skip if alpha hasn't changed. In steady state most collectibles sit at exactly 0.0
        // (out of range) or 1.0 (fully visible) every frame, so this short-circuit eliminates
        // 4 marshalled engine calls per collectible per frame for the common case.
        if (alpha == _currentAlpha) return;
        _currentAlpha = alpha;

        _material?.SetShaderParameter("proximity_alpha", alpha);

        // Edge mesh is solid geometry, so just show/hide based on alpha threshold
        if (_edgeMeshInstance != null)
            _edgeMeshInstance.Visible = alpha > 0.1f;

        if (_light != null)
            _light.LightEnergy = _config.LightEnergy * alpha;

        // Disable collision detection when invisible to avoid surprise collections
        // Monitoring = whether this Area detects bodies (what we want to disable)
        if (_collisionArea != null)
            _collisionArea.Monitoring = alpha > 0.1f;
    }

    public override void _Process(double delta)
    {
        if (_collected) return;

        var dt = (float)delta;
        var time = (float)Time.GetTicksMsec() / 1000f + _timeOffset;

        // Spin
        RotateY(Mathf.DegToRad(_config.SpinSpeed) * dt);

        // Bob up and down
        var bobOffset = Mathf.Sin(time * _config.BobSpeed * Mathf.Tau) * _config.BobAmplitude;
        var pos = Position;
        pos.Y = _baseY + bobOffset;
        Position = pos;
    }

    /// <summary>
    /// Updates materials and properties from new config. Called on hot-reload.
    /// </summary>
    public void UpdateConfig(CollectiblesConfig config)
    {
        var oldSize = _config.Size;
        var oldEdgeThickness = _config.EdgeThickness;
        _config = config;

        // Rebuild meshes if size or edge thickness changed
        if (!Mathf.IsEqualApprox(oldSize, _config.Size) ||
            !Mathf.IsEqualApprox(oldEdgeThickness, _config.EdgeThickness))
        {
            if (_meshInstance != null)
            {
                _meshInstance.Mesh = PolyhedronMeshGenerator.GetMesh(_shapeType, _config.Size);
            }
            if (_edgeMeshInstance != null)
            {
                _edgeMeshInstance.Mesh = PolyhedronMeshGenerator.GetEdgeMesh(_shapeType, _config.Size, _config.EdgeThickness);
            }
        }

        if (_material != null)
        {
            var glowColor = Color.FromHtml(_config.GlowColor);
            _material.SetShaderParameter("glow_color", glowColor);
            _material.SetShaderParameter("emission_strength", _config.GlowIntensity);
        }

        if (_light != null)
        {
            _light.LightColor = Color.FromHtml(_config.GlowColor);
            _light.OmniRange = _config.LightRange;
            // Re-apply current visibility
            _light.LightEnergy = _config.LightEnergy * _currentAlpha;
        }

        if (_sphereShape != null)
        {
            _sphereShape.Radius = _config.Size * 1.5f;
        }

        if (_edgeMaterial != null)
        {
            _edgeMaterial.AlbedoColor = Color.FromHtml(_config.EdgeColor);
        }
    }

    public override void _ExitTree()
    {
        if (_collisionArea != null)
        {
            _collisionArea.BodyEntered -= OnBodyEntered;
        }

        _material?.Dispose();
        _edgeMaterial?.Dispose();
        _sphereShape?.Dispose();
    }
}
