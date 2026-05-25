namespace Nightwalk.Core.World.Neon;

/// <summary>
/// Constants for neon sign placement calculations.
/// </summary>
public static class NeonPlacementConstants
{
    #region Window Grid

    /// <summary>Margin from building edge for window placement calculation.</summary>
    public const float WindowEdgeMargin = 2f;

    #endregion

    #region Outline Frame

    /// <summary>Margin around outline frame (2x outline bar thickness).</summary>
    public const float OutlineFrameMargin = 0.3f;

    /// <summary>Gap between outline frame and the windows it surrounds.</summary>
    public const float OutlineWindowGap = 0.4f;

    /// <summary>Intensity multiplier for outline frames (reduces brightness).</summary>
    public const float OutlineIntensityMultiplier = 0.1f;

    #endregion

    #region Full Height Strip

    /// <summary>Offset from building edge for full-height strip placement.</summary>
    public const float FullHeightStripEdgeOffset = 0.5f;

    /// <summary>Y start offset for full-height strips above ground.</summary>
    public const float FullHeightStripYStart = 2.5f;

    /// <summary>Gap from building top for full-height strip end.</summary>
    public const float FullHeightStripTopGap = 5f;

    /// <summary>Maximum height ratio of building for full-height strips.</summary>
    public const float FullHeightStripMaxHeightRatio = 0.9f;

    /// <summary>Intensity multiplier for full-height strips (reduces brightness).</summary>
    public const float FullHeightStripIntensityMultiplier = 0.5f;

    #endregion

    #region Corner Strip

    /// <summary>Inset from building corner for corner strip placement.</summary>
    public const float CornerStripInset = 0.15f;

    /// <summary>Intensity multiplier for corner strips (reduces brightness).</summary>
    public const float CornerStripIntensityMultiplier = 0.5f;

    #endregion

    #region Large Sign

    /// <summary>Width margin for large sign sizing.</summary>
    public const float LargeSignWidthMargin = 0.4f;

    /// <summary>Height margin for large sign sizing.</summary>
    public const float LargeSignHeightMargin = 1f;

    /// <summary>Extra standoff beyond facade inset for large signs.</summary>
    public const float LargeSignExtraStandoff = 0.05f;

    /// <summary>Intensity multiplier for large signs (reduces brightness).</summary>
    public const float LargeSignIntensityMultiplier = 0.5f;

    #endregion

    #region Kanji Sign

    /// <summary>Extra standoff beyond facade inset for kanji signs.</summary>
    public const float KanjiSignExtraStandoff = 0.05f;

    /// <summary>Intensity multiplier for kanji signs (reduces brightness).</summary>
    public const float KanjiSignIntensityMultiplier = 0.5f;

    #endregion

    #region Billboard

    /// <summary>Extra standoff beyond facade inset for billboards (must exceed ledge depth of 0.3f).</summary>
    public const float BillboardExtraStandoff = 0.31f;

    /// <summary>Intensity multiplier for billboards (reduces brightness).</summary>
    public const float BillboardIntensityMultiplier = 0.5f;

    #endregion

    #region Horizontal Bar

    /// <summary>Minimum height for horizontal bar neons.</summary>
    public const float HorizontalBarMinHeight = 0.15f;

    /// <summary>Height variation range for horizontal bar neons.</summary>
    public const float HorizontalBarHeightRange = 0.15f;

    /// <summary>Width margin for horizontal bars spanning windows.</summary>
    public const float HorizontalBarWidthMargin = 0.2f;

    /// <summary>Extra standoff to place horizontal bars ON the ledge (must exceed ledge depth of 0.3f).</summary>
    public const float HorizontalBarExtraStandoff = 0.32f;

    /// <summary>Intensity multiplier for horizontal bars (reduces brightness).</summary>
    public const float HorizontalBarIntensityMultiplier = 0.5f;

    #endregion

    #region Vertical Strip

    /// <summary>Minimum width for vertical strip neons.</summary>
    public const float VerticalStripMinWidth = 0.12f;

    /// <summary>Width variation range for vertical strip neons.</summary>
    public const float VerticalStripWidthRange = 0.1f;

    /// <summary>Height margin for vertical strips spanning floors.</summary>
    public const float VerticalStripHeightMargin = 0.5f;

    /// <summary>Intensity multiplier for vertical strips (reduces brightness).</summary>
    public const float VerticalStripIntensityMultiplier = 0.5f;

    #endregion

    #region Sign

    /// <summary>Base width factor relative to window width for signs.</summary>
    public const float SignWidthBaseFactor = 0.8f;

    /// <summary>Width variation factor for signs.</summary>
    public const float SignWidthVariationFactor = 0.6f;

    /// <summary>Minimum height for sign neons.</summary>
    public const float SignMinHeight = 0.6f;

    /// <summary>Height variation range for sign neons.</summary>
    public const float SignHeightRange = 0.8f;

    /// <summary>Vertical position factor within floor for signs.</summary>
    public const float SignFloorPositionFactor = 0.75f;

    /// <summary>Intensity multiplier for small signs (reduces brightness).</summary>
    public const float SignIntensityMultiplier = 0.5f;

    #endregion
}
