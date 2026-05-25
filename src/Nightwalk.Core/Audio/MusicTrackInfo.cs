namespace Nightwalk.Core.Audio;

/// <summary>
/// Immutable track metadata (engine-agnostic).
/// </summary>
public sealed record MusicTrackInfo(
    string Id,
    string Path,
    string Category,
    float BaseVolume
);
