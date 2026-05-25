using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Audio;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Manages sound effect playback with support for one-shots and loops.
/// </summary>
public sealed partial class SfxManager : Node, ISfxService
{
    private SfxConfig _config = null!;
    private Dictionary<string, SoundInfo> _sounds = new();
    private Dictionary<string, SfxPlayer> _loopingPlayers = new();
    private List<SfxPlayer> _oneShotPool = new();
    private Stack<SfxPlayer> _loopPlayerPool = new();

    private const int OneShotPoolSize = 8;
    private const int MaxLoopPoolSize = 4;

    public void Configure(SfxConfig config)
    {
        _config = config;

        // Clear and re-index sounds (Configure may be called multiple times on hot-reload)
        _sounds.Clear();

        foreach (var sound in config.Sounds)
        {
            var info = new SoundInfo(sound.Id, sound.Path, sound.Category, sound.Volume, sound.Loop);
            _sounds[sound.Id] = info;
        }

        // Handle enabled state
        if (!config.Enabled)
        {
            // SFX disabled - stop all loops
            StopAll();
        }
        else
        {
            // Apply volume to all active loops
            foreach (var kvp in _loopingPlayers)
            {
                if (_sounds.TryGetValue(kvp.Key, out var sound))
                {
                    var volumeDb = AudioMath.LinearToDb(sound.Volume * config.MasterVolume);
                    kvp.Value.SetVolume(volumeDb);
                }
            }
        }

        Log.Info(LogCategory.Audio, $"SFX manager configured with {_sounds.Count} sound(s), enabled={config.Enabled}");
    }

    public override void _Ready()
    {
        // Create one-shot player pool
        for (int i = 0; i < OneShotPoolSize; i++)
        {
            var player = new SfxPlayer { Name = $"OneShotPlayer_{i}" };
            AddChild(player);
            _oneShotPool.Add(player);
        }
    }

    public void Play(string soundId)
    {
        if (!_config.Enabled) return;
        if (!_sounds.TryGetValue(soundId, out var sound))
        {
            // Log.Warn(LogCategory.Audio, $"Unknown SFX: {soundId}");
            return;
        }

        var volumeDb = AudioMath.LinearToDb(sound.Volume * _config.MasterVolume);
        PlayOneShot(sound.Path, volumeDb);
    }

    public void Play(string soundId, float volume)
    {
        if (!_config.Enabled) return;
        if (!_sounds.TryGetValue(soundId, out var sound))
        {
            // Log.Warn(LogCategory.Audio, $"Unknown SFX: {soundId}");
            return;
        }

        var volumeDb = AudioMath.LinearToDb(volume * _config.MasterVolume);
        PlayOneShot(sound.Path, volumeDb);
    }

    public void PlayWithPitch(string soundId, float pitch)
    {
        if (!_config.Enabled) return;
        if (!_sounds.TryGetValue(soundId, out var sound))
        {
            // Log.Warn(LogCategory.Audio, $"Unknown SFX: {soundId}");
            return;
        }

        var volumeDb = AudioMath.LinearToDb(sound.Volume * _config.MasterVolume);
        PlayOneShot(sound.Path, volumeDb, pitch);
    }

    public void PlayLoop(string soundId)
    {
        if (!_config.Enabled) return;
        if (_loopingPlayers.ContainsKey(soundId)) return; // Already looping

        if (!_sounds.TryGetValue(soundId, out var sound))
        {
            // Log.Warn(LogCategory.Audio, $"Unknown SFX: {soundId}");
            return;
        }

        // Get player from pool or create new
        SfxPlayer player;
        if (_loopPlayerPool.Count > 0)
        {
            player = _loopPlayerPool.Pop();
            player.Name = $"Loop_{soundId}";
        }
        else
        {
            player = new SfxPlayer { Name = $"Loop_{soundId}" };
            AddChild(player);
        }

        player.LoadSound(sound.Path);

        var volumeDb = AudioMath.LinearToDb(sound.Volume * _config.MasterVolume);
        player.PlayLoop(volumeDb);

        _loopingPlayers[soundId] = player;
    }

    public void StopLoop(string soundId)
    {
        if (!_loopingPlayers.TryGetValue(soundId, out var player)) return;

        player.Stop();
        _loopingPlayers.Remove(soundId);
        ReturnLoopPlayerToPool(player);
    }

    public void StopLoop(string soundId, float fadeOutDuration)
    {
        if (!_loopingPlayers.TryGetValue(soundId, out var player)) return;

        player.FadeOut(fadeOutDuration);
        _loopingPlayers.Remove(soundId);

        // Return to pool after fade completes
        GetTree().CreateTimer(fadeOutDuration + 0.1f).Timeout += () =>
        {
            if (IsInstanceValid(player))
                ReturnLoopPlayerToPool(player);
        };
    }

    private void ReturnLoopPlayerToPool(SfxPlayer player)
    {
        if (_loopPlayerPool.Count < MaxLoopPoolSize)
        {
            player.Stop();
            _loopPlayerPool.Push(player);
        }
        else
        {
            player.QueueFree();
        }
    }

    public void SetLoopPitch(string soundId, float pitch)
    {
        if (_loopingPlayers.TryGetValue(soundId, out var player))
        {
            player.SetPitch(pitch);
        }
    }

    public void SetLoopVolume(string soundId, float volume)
    {
        if (_loopingPlayers.TryGetValue(soundId, out var player))
        {
            var volumeDb = AudioMath.LinearToDb(volume * _config.MasterVolume);
            player.SetVolume(volumeDb);
        }
    }

    public bool IsLooping(string soundId)
    {
        return _loopingPlayers.ContainsKey(soundId) && _loopingPlayers[soundId].IsLooping;
    }

    public void StopAll()
    {
        // Stop all loops and return to pool
        foreach (var kvp in _loopingPlayers)
        {
            kvp.Value.Stop();
            ReturnLoopPlayerToPool(kvp.Value);
        }
        _loopingPlayers.Clear();

        // Stop all one-shots
        foreach (var player in _oneShotPool)
        {
            player.Stop();
        }
    }

    /// <summary>
    /// Returns true if sounds are configured.
    /// </summary>
    public bool HasSounds => _sounds.Count > 0;

    private void PlayOneShot(string path, float volumeDb, float pitch = 1f)
    {
        // Find an available player from the pool
        foreach (var player in _oneShotPool)
        {
            if (!player.IsPlaying)
            {
                player.LoadSound(path);
                player.PlayOneShot(volumeDb, pitch);
                return;
            }
        }

        // All players busy - steal the first one
        var stolen = _oneShotPool[0];
        stolen.Stop();
        stolen.LoadSound(path);
        stolen.PlayOneShot(volumeDb, pitch);
    }

    /// <summary>
    /// Plays a generated AudioStream (from SfxSynth) as a one-shot.
    /// </summary>
    public void PlayGenerated(AudioStream stream, float volume = 1f)
    {
        if (!_config.Enabled) return;

        var volumeDb = AudioMath.LinearToDb(volume * _config.MasterVolume);
        PlayOneShotStream(stream, volumeDb);
    }

    private void PlayOneShotStream(AudioStream stream, float volumeDb, float pitch = 1f)
    {
        // Find an available player from the pool
        foreach (var player in _oneShotPool)
        {
            if (!player.IsPlaying)
            {
                player.PlayOneShotStream(stream, volumeDb, pitch);
                return;
            }
        }

        // All players busy - steal the first one
        var stolen = _oneShotPool[0];
        stolen.Stop();
        stolen.PlayOneShotStream(stream, volumeDb, pitch);
    }

    public override void _ExitTree()
    {
        StopAll();
    }

    private sealed record SoundInfo(
        string Id,
        string Path,
        string Category,
        float Volume,
        bool Loop
    );
}
