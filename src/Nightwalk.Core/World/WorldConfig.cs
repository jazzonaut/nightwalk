namespace Nightwalk.Core.World;

/// <summary>
/// Configuration for world creation.
/// </summary>
public sealed class WorldConfig
{
    public int Seed { get; set; }
    public float ChunkSize { get; set; }
    public int LoadRadius { get; set; }
    public int UnloadRadius { get; set; }
}
