namespace Nightwalk.Godot.Ziplines;

/// <summary>
/// State machine states for the zipline tool.
/// </summary>
public enum ZiplineState
{
    /// <summary>
    /// Tool selected, no anchors placed.
    /// </summary>
    Idle,

    /// <summary>
    /// One anchor placed, awaiting second.
    /// </summary>
    FirstAnchor
}
