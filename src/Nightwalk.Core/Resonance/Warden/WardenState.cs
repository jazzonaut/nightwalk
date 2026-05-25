namespace Nightwalk.Core.Resonance.Warden;

/// <summary>
/// States the Warden can be in during Resonance mode.
/// </summary>
public enum WardenState
{
    /// <summary>Warden is not present.</summary>
    Absent,

    /// <summary>Warden is spawning/arriving from above.</summary>
    Arriving,

    /// <summary>Warden is present and observing (not vulnerable).</summary>
    Present,

    /// <summary>Warden is vulnerable and can be repelled.</summary>
    Vulnerable,

    /// <summary>Warden is departing after being repelled or alert dropped.</summary>
    Departing
}
