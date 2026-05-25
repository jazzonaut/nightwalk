using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scoring.Events;

namespace Nightwalk.Godot.Rendering;

/// <summary>
/// Handles temporary visual effects when score changes.
/// Gain: neon (saturation + bloom). Loss: danger (orange tint + desaturate).
/// </summary>
public sealed partial class ScoreEffectSystem : Node
{
    private ShaderMaterial? _material;
    private VisualPresetManager? _presetManager;
    private IEventBus? _eventBus;
    private WorldEnvironment? _worldEnvironment;
    private GameDataService? _data;
    private Tween? _effectTween;

    private ScoreEffectsConfig Config => _data!.Gameplay.ScoreEffects;

    // Access current Environment (may change on hot reload)
    private global::Godot.Environment? Environment => _worldEnvironment?.Environment;

    /// <summary>
    /// Configures the effect system with required dependencies.
    /// </summary>
    public void Configure(ShaderMaterial material, VisualPresetManager? presetManager, IEventBus eventBus, WorldEnvironment? worldEnvironment, GameDataService data)
    {
        // Clean up any existing tween from previous configuration
        _effectTween?.Kill();
        _effectTween = null;

        // Unsubscribe from previous event bus if reconfiguring
        _eventBus?.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);

        _material = material;
        _presetManager = presetManager;
        _eventBus = eventBus;
        _worldEnvironment = worldEnvironment;
        _data = data;

        _eventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
    }

    public override void _ExitTree()
    {
        _eventBus?.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
        _effectTween?.Kill();
        _effectTween = null;
    }

    private void OnScoreChanged(ScoreChangedEvent e)
    {
        if (e.Points == 0 || _material == null || _presetManager == null || Environment == null) return;
        // Do not react to zipline or prism placement
        if (e.Category == Core.Scoring.ScoreCategory.ZiplinePlacement || e.Category == Core.Scoring.ScoreCategory.PrismPlacement) return;

        if (e.Points > 0)
            TriggerGainEffect();
        else
            TriggerLossEffect();
    }

    private VisualPreset GetCurrentPreset()
    {
        // _presetManager is checked in OnScoreChanged before this is called
        return VisualPreset.All[_presetManager!.CurrentPresetIndex];
    }

    /// <summary>
    /// Restores all effect parameters to current preset baseline.
    /// Call before starting a new effect to prevent stuck values.
    /// </summary>
    private void RestoreBaseline()
    {
        var preset = GetCurrentPreset();
        _material!.SetShaderParameter("saturation", preset.Saturation);
        _material.SetShaderParameter("contrast", preset.Contrast);
        _material.SetShaderParameter("highlight_tint", preset.HighlightTint);
        _material.SetShaderParameter("chromatic_aberration_strength", preset.ChromaticStrength);
        _material.SetShaderParameter("shadow_tint", preset.ShadowTint);
        _material.SetShaderParameter("color_grading_strength", preset.ColorGradingStrength);
        Environment!.GlowIntensity = preset.BloomIntensity;
    }

    private void TriggerGainEffect()
    {
        _effectTween?.Kill();
        RestoreBaseline();

        var config = Config;
        var preset = GetCurrentPreset();
        var baseSaturation = preset.Saturation;
        var baseBloom = preset.BloomIntensity;
        var baseContrast = preset.Contrast;
        var baseHighlightTint = preset.HighlightTint;

        var gainHighlightTint = Color.FromHtml(config.GainHighlightTint);

        // Immediately set peak values
        _material!.SetShaderParameter("saturation", config.GainSaturationPeak);
        _material.SetShaderParameter("contrast", config.GainContrastPeak);
        _material.SetShaderParameter("highlight_tint", gainHighlightTint);
        Environment!.GlowIntensity = config.GainBloomPeak;

        // Decay tween - animate back to baseline
        _effectTween = CreateTween();
        _effectTween.SetParallel(true);

        _effectTween.TweenMethod(
            Callable.From<float>(v => _material.SetShaderParameter("saturation", v)),
            config.GainSaturationPeak, baseSaturation, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        _effectTween.TweenMethod(
            Callable.From<float>(v => _material.SetShaderParameter("contrast", v)),
            config.GainContrastPeak, baseContrast, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        _effectTween.TweenMethod(
            Callable.From<Color>(c => _material.SetShaderParameter("highlight_tint", c)),
            gainHighlightTint, baseHighlightTint, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        _effectTween.TweenMethod(
            Callable.From<float>(v => Environment!.GlowIntensity = v),
            config.GainBloomPeak, baseBloom, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);
    }

    private void TriggerLossEffect()
    {
        _effectTween?.Kill();
        RestoreBaseline();

        var config = Config;
        var preset = GetCurrentPreset();
        var baseSaturation = preset.Saturation;
        var baseChromatic = preset.ChromaticStrength;
        var baseContrast = preset.Contrast;
        var baseShadowTint = preset.ShadowTint;
        var baseColorGradingStrength = preset.ColorGradingStrength;

        var lossShadowTint = Color.FromHtml(config.LossShadowTint);

        // Immediately set danger values
        _material!.SetShaderParameter("saturation", config.LossSaturationPeak);
        _material.SetShaderParameter("chromatic_aberration_strength", config.LossChromaticPeak);
        _material.SetShaderParameter("contrast", config.LossContrastPeak);
        _material.SetShaderParameter("shadow_tint", lossShadowTint);
        _material.SetShaderParameter("color_grading_strength", config.LossColorGradingStrength);

        // Decay tween - animate back to baseline
        _effectTween = CreateTween();
        _effectTween.SetParallel(true);

        _effectTween.TweenMethod(
            Callable.From<float>(v => _material.SetShaderParameter("saturation", v)),
            config.LossSaturationPeak, baseSaturation, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        _effectTween.TweenMethod(
            Callable.From<float>(v => _material.SetShaderParameter("chromatic_aberration_strength", v)),
            config.LossChromaticPeak, baseChromatic, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        _effectTween.TweenMethod(
            Callable.From<float>(v => _material.SetShaderParameter("contrast", v)),
            config.LossContrastPeak, baseContrast, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        _effectTween.TweenMethod(
            Callable.From<Color>(c => _material.SetShaderParameter("shadow_tint", c)),
            lossShadowTint, baseShadowTint, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        _effectTween.TweenMethod(
            Callable.From<float>(v => _material.SetShaderParameter("color_grading_strength", v)),
            config.LossColorGradingStrength, baseColorGradingStrength, config.EffectDuration
        ).SetEase(Tween.EaseType.Out).SetDelay(0.1f);
    }
}
