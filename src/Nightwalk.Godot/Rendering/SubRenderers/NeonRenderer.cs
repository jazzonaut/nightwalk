using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.World.Neon;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Rendering.Constants;
using Nightwalk.Godot.Rendering.Materials;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Renders neon signs and lights on buildings.
/// Handles various neon shapes including kanji signs, billboards, and corner strips.
/// Supports incremental rendering to spread work across multiple frames.
/// </summary>
public sealed class NeonRenderer : ISubRenderer, IPhasedSubRenderer
{
    public ChunkRenderPhase Phase => ChunkRenderPhase.Neons;

    private readonly NeonConfig _config;
    private readonly float _lodParallaxDistanceSq;
    private readonly float _lodLightCreationDistanceSq;

    public NeonRenderer(NeonConfig config)
    {
        _config = config;
        // Pre-compute squared distances to avoid sqrt in hot path
        _lodParallaxDistanceSq = config.LodParallaxDistance * config.LodParallaxDistance;
        // Don't create lights beyond fade end (distance + fade length)
        var lightCreationDist = config.LodLightDistance + config.LodLightFadeLength;
        _lodLightCreationDistanceSq = lightCreationDist * lightCreationDist;
    }

    public void Render(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        if (data.Neons.Count == 0) return;

        var neonsNode = new Node3D { Name = "Neons" };

        foreach (var neon in data.Neons)
        {
            CreateNeonLight(neonsNode, neon, context);
        }

        parent.AddChild(neonsNode);
    }

    public bool RenderIncremental(ChunkRenderState state, long budgetUsec)
    {
        if (state.ChunkNode == null || state.Context == null) return true;
        if (state.Data.Neons.Count == 0) return true;

        var context = state.Context;
        var startTime = Time.GetTicksUsec();

        // Create neons container on first call
        if (state.NeonIndex == 0)
        {
            state.NeonsNode = new Node3D { Name = "Neons" };
            state.ChunkNode.AddChild(state.NeonsNode);
        }

        if (state.NeonsNode == null) return true;

        // Process neons incrementally
        while (state.NeonIndex < state.Data.Neons.Count)
        {
            var neon = state.Data.Neons[state.NeonIndex];
            CreateNeonLight(state.NeonsNode, neon, context);
            state.NeonIndex++;

            // Check time budget every few neons
            if (state.NeonIndex % 5 == 0)
            {
                var elapsed = (long)(Time.GetTicksUsec() - startTime);
                if (elapsed >= budgetUsec)
                    return false; // More work needed
            }
        }

        return true; // Phase complete
    }

    private void CreateNeonLight(Node3D parent, NeonLightData neon, ChunkRenderContext context)
    {
        var localX = neon.X - context.ChunkWorldX;
        var localZ = neon.Z - context.ChunkWorldZ;

        switch (neon.Shape)
        {
            case NeonShape.CornerStrip:
                CreateCornerStripMesh(parent, neon, localX, localZ, context);
                break;
            case NeonShape.KanjiSign:
                CreateKanjiSign(parent, neon, localX, localZ, context);
                break;
            case NeonShape.Billboard:
                CreateBillboard(parent, neon, localX, localZ, context);
                break;
            case NeonShape.Outline:
                CreateOutlineNeon(parent, neon, localX, localZ, context);
                break;
            default:
                CreateSimpleNeon(parent, neon, localX, localZ, context);
                break;
        }
    }

    private void CreateSimpleNeon(Node3D parent, NeonLightData neon, float localX, float localZ, ChunkRenderContext context)
    {
        var meshInstance = new MeshInstance3D
        {
            Position = new Vector3(localX, neon.Y, localZ),
            RotationDegrees = new Vector3(0, Mathf.RadToDeg(neon.RotationY), 0)
        };

        var mesh = new BoxMesh { Size = GetNeonSize(neon) };
        meshInstance.Mesh = mesh;
        meshInstance.MaterialOverride = context.MaterialFactory.GetOrCreateNeonMaterial(neon.Color, neon.Intensity);

        // Apply visibility range to small elements
        if (IsSmallElement(neon.Shape))
        {
            ApplyVisibilityRange(meshInstance);
        }

        parent.AddChild(meshInstance);
    }

    private void CreateOutlineNeon(Node3D parent, NeonLightData neon, float localX, float localZ, ChunkRenderContext context)
    {
        var outlineNode = new Node3D
        {
            Position = new Vector3(localX, neon.Y, localZ),
            RotationDegrees = new Vector3(0, Mathf.RadToDeg(neon.RotationY), 0)
        };

        var material = context.MaterialFactory.GetOrCreateNeonMaterial(neon.Color, neon.Intensity);
        var barThickness = RenderingConstants.NeonOutlineThickness;
        var depth = RenderingConstants.NeonDepthSmall;

        // Create 4 bars forming a hollow rectangle frame
        // Top bar
        var topBar = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(neon.Width, barThickness, depth) },
            Position = new Vector3(0, neon.Height / 2f - barThickness / 2f, 0),
            MaterialOverride = material
        };

        // Bottom bar
        var bottomBar = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(neon.Width, barThickness, depth) },
            Position = new Vector3(0, -neon.Height / 2f + barThickness / 2f, 0),
            MaterialOverride = material
        };

        // Left bar (height excludes top/bottom bar overlap)
        var innerHeight = neon.Height - barThickness * 2f;
        var leftBar = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(barThickness, innerHeight, depth) },
            Position = new Vector3(-neon.Width / 2f + barThickness / 2f, 0, 0),
            MaterialOverride = material
        };

        // Right bar
        var rightBar = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(barThickness, innerHeight, depth) },
            Position = new Vector3(neon.Width / 2f - barThickness / 2f, 0, 0),
            MaterialOverride = material
        };

        ApplyVisibilityRange(topBar);
        ApplyVisibilityRange(bottomBar);
        ApplyVisibilityRange(leftBar);
        ApplyVisibilityRange(rightBar);

        outlineNode.AddChild(topBar);
        outlineNode.AddChild(bottomBar);
        outlineNode.AddChild(leftBar);
        outlineNode.AddChild(rightBar);

        parent.AddChild(outlineNode);
    }

    private static Vector3 GetNeonSize(NeonLightData neon)
    {
        return neon.Shape switch
        {
            NeonShape.HorizontalBar => new Vector3(neon.Width, neon.Height, RenderingConstants.NeonDepthSmall),
            NeonShape.VerticalStrip or NeonShape.FullHeightStrip => new Vector3(neon.Width, neon.Height, RenderingConstants.NeonDepthSmall),
            NeonShape.Outline or NeonShape.Sign => new Vector3(neon.Width, neon.Height, RenderingConstants.NeonDepthMedium),
            NeonShape.LargeSign => new Vector3(neon.Width, neon.Height, RenderingConstants.NeonDepthLarge),
            _ => new Vector3(neon.Width, neon.Height, RenderingConstants.NeonDepthSmall)
        };
    }

    private static bool IsSmallElement(NeonShape shape)
    {
        return shape is NeonShape.HorizontalBar or NeonShape.Sign or NeonShape.VerticalStrip or NeonShape.LargeSign;
    }

    private void ApplyVisibilityRange(MeshInstance3D meshInstance)
    {
        meshInstance.VisibilityRangeEnd = _config.LodSmallElementVisibilityRange;
        meshInstance.VisibilityRangeEndMargin = _config.LodSmallElementVisibilityMargin;
        meshInstance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
    }

    private void CreateKanjiSign(Node3D parent, NeonLightData neon, float localX, float localZ, ChunkRenderContext context)
    {
        var signNode = new Node3D
        {
            Position = new Vector3(localX, neon.Y, localZ),
            RotationDegrees = new Vector3(0, Mathf.RadToDeg(neon.RotationY), 0)
        };

        // LOD: Reduce parallax layers for distant signs
        var worldPos = new Vector3(context.ChunkWorldX + localX, neon.Y, context.ChunkWorldZ + localZ);
        var distanceSq = worldPos.DistanceSquaredTo(context.CameraPosition);
        var requestedLayers = neon.ParallaxLayers > 0 ? neon.ParallaxLayers : 1;
        var layers = distanceSq > _lodParallaxDistanceSq ? 1 : requestedLayers;
        var layerSpacing = layers > 1 ? neon.ParallaxDepth / (layers - 1) : 0f;

        var template = context.MaterialFactory.GetKanjiMaterialTemplate();

        // Randomly select a flicker pattern based on seed
        var flickerPattern = neon.Seed % 6;

        for (int i = 0; i < layers; i++)
        {
            var meshInstance = new MeshInstance3D();

            float zOffset = layers > 1 ? (i - (layers - 1) / 2f) * layerSpacing : 0f;
            meshInstance.Position = new Vector3(0, 0, zOffset);

            var mesh = new QuadMesh { Size = new Vector2(neon.Width, neon.Height) };
            meshInstance.Mesh = mesh;

            // Use shared template with per-instance parameters (avoids material duplication)
            meshInstance.MaterialOverride = template;
            meshInstance.SetInstanceShaderParameter("sign_color", Color.FromHtml(neon.Color));
            meshInstance.SetInstanceShaderParameter("emission_strength", neon.Intensity);
            meshInstance.SetInstanceShaderParameter("glyph_seed", (float)(neon.Seed + i * RenderingConstants.KanjiLayerSeedOffset));
            meshInstance.SetInstanceShaderParameter("glyph_columns", neon.Width > RenderingConstants.KanjiColumnWidthThreshold ? 2 : 1);
            meshInstance.SetInstanceShaderParameter("flicker_pattern", flickerPattern);
            meshInstance.SetInstanceShaderParameter("is_broken", neon.IsBroken);
            meshInstance.SetInstanceShaderParameter("broken_intensity", neon.BrokenIntensity);
            meshInstance.SetInstanceShaderParameter("current_layer", i);
            meshInstance.SetInstanceShaderParameter("total_layers", layers);

            ApplyVisibilityRange(meshInstance);
            signNode.AddChild(meshInstance);
        }

        // Add dynamic light for broken signs (skip if too far - light would be fully faded anyway)
        if (neon.IsBroken && neon.BrokenIntensity > 0.3f && _config.NeonLightCastingEnabled
            && distanceSq <= _lodLightCreationDistanceSq)
        {
            AddBrokenSignLight(signNode, neon);
        }

        parent.AddChild(signNode);
    }

    private void AddBrokenSignLight(Node3D parent, NeonLightData neon)
    {
        var light = new OmniLight3D
        {
            Position = new Vector3(0, 0, 0.5f), // Slightly in front of sign
            LightColor = Color.FromHtml(neon.Color),
            LightEnergy = neon.Intensity * neon.BrokenIntensity * _config.NeonLightEnergy * 0.3f,
            OmniRange = Mathf.Max(neon.Width, neon.Height) * _config.NeonLightRange * 0.15f,
            OmniAttenuation = 2.0f,
            ShadowEnabled = false,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = _config.LodLightDistance,
            DistanceFadeLength = _config.LodLightFadeLength
        };
        parent.AddChild(light);
    }

    private void CreateBillboard(Node3D parent, NeonLightData neon, float localX, float localZ, ChunkRenderContext context)
    {
        var signNode = new Node3D
        {
            Position = new Vector3(localX, neon.Y, localZ),
            RotationDegrees = new Vector3(0, Mathf.RadToDeg(neon.RotationY), 0)
        };

        // LOD: Reduce parallax layers for distant billboards
        var worldPos = new Vector3(context.ChunkWorldX + localX, neon.Y, context.ChunkWorldZ + localZ);
        var distanceSq = worldPos.DistanceSquaredTo(context.CameraPosition);
        var requestedLayers = neon.ParallaxLayers > 0 ? neon.ParallaxLayers : 1;
        var layers = distanceSq > _lodParallaxDistanceSq ? 1 : requestedLayers;
        var layerSpacing = layers > 1 ? neon.ParallaxDepth / (layers - 1) : 0f;

        var template = context.MaterialFactory.GetBillboardMaterialTemplate();

        // Randomly select an animation style based on seed
        var animationStyle = neon.Seed % 5;

        for (int i = 0; i < layers; i++)
        {
            var meshInstance = new MeshInstance3D();

            float zOffset = layers > 1 ? (i - (layers - 1) / 2f) * layerSpacing : 0f;
            meshInstance.Position = new Vector3(0, 0, zOffset);

            var mesh = new QuadMesh { Size = new Vector2(neon.Width, neon.Height) };
            meshInstance.Mesh = mesh;

            // Use shared template with per-instance parameters (avoids material duplication)
            meshInstance.MaterialOverride = template;
            meshInstance.SetInstanceShaderParameter("primary_color", Color.FromHtml(neon.Color));
            meshInstance.SetInstanceShaderParameter("secondary_color", Color.FromHtml(neon.SecondaryColor ?? neon.Color));
            meshInstance.SetInstanceShaderParameter("emission_strength", neon.Intensity);
            meshInstance.SetInstanceShaderParameter("billboard_seed", (float)(neon.Seed + i * RenderingConstants.BillboardLayerSeedOffset));
            meshInstance.SetInstanceShaderParameter("animation_style", animationStyle);
            meshInstance.SetInstanceShaderParameter("is_broken", neon.IsBroken);
            meshInstance.SetInstanceShaderParameter("broken_intensity", neon.BrokenIntensity);
            meshInstance.SetInstanceShaderParameter("current_layer", i);
            meshInstance.SetInstanceShaderParameter("total_layers", layers);

            ApplyVisibilityRange(meshInstance);
            signNode.AddChild(meshInstance);
        }

        // Add dynamic light for broken billboards (skip if too far - light would be fully faded anyway)
        if (neon.IsBroken && neon.BrokenIntensity > 0.3f && _config.NeonLightCastingEnabled
            && distanceSq <= _lodLightCreationDistanceSq)
        {
            AddBrokenSignLight(signNode, neon);
        }

        parent.AddChild(signNode);
    }

    private void CreateCornerStripMesh(Node3D parent, NeonLightData neon, float localX, float localZ, ChunkRenderContext context)
    {
        var material = context.MaterialFactory.GetOrCreateNeonMaterial(neon.Color, neon.Intensity);

        switch (neon.CornerStyle)
        {
            case 0: // L-shape (original)
                CreateLShapeStrip(parent, neon, localX, localZ, material);
                break;
            case 1: // T-shape
                CreateTShapeStrip(parent, neon, localX, localZ, material);
                break;
            case 2: // Cross shape
                CreateCrossStrip(parent, neon, localX, localZ, material);
                break;
            case 3: // Bracket shape
                CreateBracketStrip(parent, neon, localX, localZ, material);
                break;
            default:
                CreateLShapeStrip(parent, neon, localX, localZ, material);
                break;
        }
    }

    private void CreateLShapeStrip(Node3D parent, NeonLightData neon, float localX, float localZ, Material material)
    {
        var stripWidth = neon.Width;
        var stripHeight = neon.Height;

        var strip1 = new MeshInstance3D();
        var strip2 = new MeshInstance3D();

        var mesh1 = new BoxMesh { Size = new Vector3(stripWidth * RenderingConstants.CornerStripLengthMultiplier, stripHeight, RenderingConstants.CornerStripDepth) };
        var mesh2 = new BoxMesh { Size = new Vector3(RenderingConstants.CornerStripDepth, stripHeight, stripWidth * RenderingConstants.CornerStripLengthMultiplier) };

        strip1.Mesh = mesh1;
        strip2.Mesh = mesh2;
        strip1.MaterialOverride = material;
        strip2.MaterialOverride = material;

        strip1.Position = new Vector3(localX, neon.Y, localZ + stripWidth * RenderingConstants.CornerStripPositionMultiplier);
        strip2.Position = new Vector3(localX + stripWidth * RenderingConstants.CornerStripPositionMultiplier, neon.Y, localZ);

        parent.AddChild(strip1);
        parent.AddChild(strip2);
    }

    private void CreateTShapeStrip(Node3D parent, NeonLightData neon, float localX, float localZ, Material material)
    {
        var stripWidth = neon.Width;
        var stripHeight = neon.Height;
        var lengthMult = RenderingConstants.CornerStripLengthMultiplier;
        var posMult = RenderingConstants.CornerStripPositionMultiplier;

        // Main vertical strip (longer)
        var strip1 = new MeshInstance3D();
        var mesh1 = new BoxMesh { Size = new Vector3(RenderingConstants.CornerStripDepth, stripHeight, stripWidth * lengthMult * 1.5f) };
        strip1.Mesh = mesh1;
        strip1.MaterialOverride = material;
        strip1.Position = new Vector3(localX + stripWidth * posMult, neon.Y, localZ);

        // Horizontal strip (T crossbar)
        var strip2 = new MeshInstance3D();
        var mesh2 = new BoxMesh { Size = new Vector3(stripWidth * lengthMult, stripHeight, RenderingConstants.CornerStripDepth) };
        strip2.Mesh = mesh2;
        strip2.MaterialOverride = material;
        strip2.Position = new Vector3(localX, neon.Y, localZ + stripWidth * posMult);

        // Second arm of T
        var strip3 = new MeshInstance3D();
        var mesh3 = new BoxMesh { Size = new Vector3(stripWidth * lengthMult * 0.6f, stripHeight, RenderingConstants.CornerStripDepth) };
        strip3.Mesh = mesh3;
        strip3.MaterialOverride = material;
        strip3.Position = new Vector3(localX + stripWidth * posMult * 1.5f, neon.Y, localZ + stripWidth * posMult);

        parent.AddChild(strip1);
        parent.AddChild(strip2);
        parent.AddChild(strip3);
    }

    private void CreateCrossStrip(Node3D parent, NeonLightData neon, float localX, float localZ, Material material)
    {
        var stripWidth = neon.Width;
        var stripHeight = neon.Height;
        var lengthMult = RenderingConstants.CornerStripLengthMultiplier;

        // Horizontal strip (full)
        var strip1 = new MeshInstance3D();
        var mesh1 = new BoxMesh { Size = new Vector3(stripWidth * lengthMult * 2f, stripHeight, RenderingConstants.CornerStripDepth) };
        strip1.Mesh = mesh1;
        strip1.MaterialOverride = material;
        strip1.Position = new Vector3(localX, neon.Y, localZ);

        // Vertical strip (full)
        var strip2 = new MeshInstance3D();
        var mesh2 = new BoxMesh { Size = new Vector3(RenderingConstants.CornerStripDepth, stripHeight, stripWidth * lengthMult * 2f) };
        strip2.Mesh = mesh2;
        strip2.MaterialOverride = material;
        strip2.Position = new Vector3(localX, neon.Y, localZ);

        parent.AddChild(strip1);
        parent.AddChild(strip2);
    }

    private void CreateBracketStrip(Node3D parent, NeonLightData neon, float localX, float localZ, Material material)
    {
        var stripWidth = neon.Width;
        var stripHeight = neon.Height;
        var lengthMult = RenderingConstants.CornerStripLengthMultiplier;
        var posMult = RenderingConstants.CornerStripPositionMultiplier;
        var bracketArmLength = stripWidth * lengthMult * 0.5f;

        // Main vertical strip
        var mainStrip = new MeshInstance3D();
        var mainMesh = new BoxMesh { Size = new Vector3(RenderingConstants.CornerStripDepth, stripHeight, stripWidth * lengthMult) };
        mainStrip.Mesh = mainMesh;
        mainStrip.MaterialOverride = material;
        mainStrip.Position = new Vector3(localX + stripWidth * posMult, neon.Y, localZ);

        // Top horizontal arm
        var topArm = new MeshInstance3D();
        var topMesh = new BoxMesh { Size = new Vector3(bracketArmLength, RenderingConstants.CornerStripDepth * 2f, RenderingConstants.CornerStripDepth) };
        topArm.Mesh = topMesh;
        topArm.MaterialOverride = material;
        topArm.Position = new Vector3(localX + bracketArmLength * 0.5f, neon.Y + stripHeight / 2f - 0.3f, localZ + stripWidth * posMult);

        // Bottom horizontal arm
        var bottomArm = new MeshInstance3D();
        var bottomMesh = new BoxMesh { Size = new Vector3(bracketArmLength, RenderingConstants.CornerStripDepth * 2f, RenderingConstants.CornerStripDepth) };
        bottomArm.Mesh = bottomMesh;
        bottomArm.MaterialOverride = material;
        bottomArm.Position = new Vector3(localX + bracketArmLength * 0.5f, neon.Y - stripHeight / 2f + 0.3f, localZ + stripWidth * posMult);

        parent.AddChild(mainStrip);
        parent.AddChild(topArm);
        parent.AddChild(bottomArm);
    }
}
