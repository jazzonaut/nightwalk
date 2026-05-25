namespace Nightwalk.Godot.Ziplines;

/// <summary>
/// Entry tracking a cable with its associated anchors.
/// </summary>
public readonly struct ZiplineEntry
{
    public ZiplineCable Cable { get; init; }
    public ZiplineAnchor Anchor1 { get; init; }
    public ZiplineAnchor Anchor2 { get; init; }
}
