namespace Nightwalk.Core.Audio;

/// <summary>
/// Sound effect ID constants for type-safe sound references.
/// </summary>
public static class SfxId
{
    // Player sounds
    public const string FootstepWalk = "footstep_walk";
    public const string FootstepRun = "footstep_run";
    public const string Jump = "jump";
    public const string Land = "land";

    // Movement sounds
    public const string JetpackActivate = "jetpack_activate";
    public const string JetpackLoop = "jetpack_loop";
    public const string JetpackDeactivate = "jetpack_deactivate";
    public const string FlightActivate = "flight_activate";
    public const string FlightLoop = "flight_loop";
    public const string FlightDeactivate = "flight_deactivate";

    // Energy sounds
    public const string EnergyRegen = "energy_regen";
    public const string EnergyLow = "energy_low";
    public const string EnergyDepleted = "energy_depleted";
    public const string EnergyFull = "energy_full";

    // Environment sounds
    public const string RainLight = "rain_light";
    public const string RainMedium = "rain_medium";
    public const string RainHeavy = "rain_heavy";
    public const string CityAmbient = "city_ambient";

    // UI sounds
    public const string PresetSwitch = "preset_switch";
    public const string UiConfirm = "ui_confirm";
}
