using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Nightwalk.Core.Audio;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Manages background music playback with crossfading support.
/// </summary>
public sealed partial class MusicManager : Node, IMusicService
{
    private MusicConfig _config = null!;
    private Dictionary<string, MusicTrackInfo> _tracks = new();
    private Dictionary<string, List<string>> _tracksByCategory = new();

    private MusicPlayer _playerA = null!;
    private MusicPlayer _playerB = null!;
    private MusicPlayer _activePlayer = null!;

    private Random _rng = new();

    public MusicState State { get; private set; } = MusicState.Stopped;
    public string? CurrentTrackId { get; private set; }

    public void Configure(MusicConfig config)
    {
        var wasEnabled = _config?.Enabled ?? false;
        _config = config;

        // Clear and re-index tracks (Configure may be called multiple times on hot-reload)
        _tracks.Clear();
        _tracksByCategory.Clear();

        foreach (var track in config.Tracks)
        {
            var info = new MusicTrackInfo(track.Id, track.Path, track.Category, track.BaseVolume);
            _tracks[track.Id] = info;

            if (!_tracksByCategory.TryGetValue(track.Category, out var list))
            {
                list = new List<string>();
                _tracksByCategory[track.Category] = list;
            }
            list.Add(track.Id);
        }

        // Handle enabled state changes
        if (!config.Enabled && State != MusicState.Stopped)
        {
            // Music was disabled - stop playback
            Stop(fadeOut: true);
        }
        else if (config.Enabled && !wasEnabled && State == MusicState.Stopped && HasTracks && _activePlayer != null)
        {
            // Music was just enabled - start playing (only if _Ready has been called)
            PlayCategory("ambient", crossfade: false);
        }
        else if (config.Enabled && CurrentTrackId != null && _tracks.TryGetValue(CurrentTrackId, out var currentTrack))
        {
            // Apply current volume to playing music
            var volumeDb = AudioMath.LinearToDb(currentTrack.BaseVolume * config.MasterVolume);
            _activePlayer?.SetVolumeImmediate(volumeDb);
        }

        Log.Info(LogCategory.Audio, $"Music manager configured with {_tracks.Count} track(s), enabled={config.Enabled}");
    }

    public override void _Ready()
    {
        // Create dual players for crossfading
        _playerA = new MusicPlayer { Name = "MusicPlayerA" };
        _playerB = new MusicPlayer { Name = "MusicPlayerB" };
        AddChild(_playerA);
        AddChild(_playerB);
        _activePlayer = _playerA;
    }

    public void Play(string trackId, bool crossfade = true)
    {
        if (!_config.Enabled) return;
        if (_activePlayer == null) return; // Not yet initialized (_Ready not called)
        if (!_tracks.TryGetValue(trackId, out var track))
        {
            Log.Warn(LogCategory.Audio, $"Unknown track: {trackId}");
            return;
        }

        var volumeDb = AudioMath.LinearToDb(track.BaseVolume * _config.MasterVolume);

        if (crossfade && State == MusicState.Playing)
        {
            // Crossfade: fade out active, fade in inactive
            var inactivePlayer = _activePlayer == _playerA ? _playerB : _playerA;

            State = MusicState.CrossFading;
            _activePlayer.FadeOut(_config.CrossfadeDuration, stopAfter: true);

            inactivePlayer.LoadTrack(track.Path);
            inactivePlayer.FadeIn(volumeDb, _config.CrossfadeDuration);

            _activePlayer = inactivePlayer;
        }
        else
        {
            // Direct play
            _activePlayer.Stop();
            _activePlayer.LoadTrack(track.Path);
            _activePlayer.Play(volumeDb, _config.LoopEnabled);
        }

        CurrentTrackId = trackId;
        State = MusicState.Playing;

        Log.Info(LogCategory.Audio, $"Playing track: {trackId}");
    }

    public void PlayCategory(string category, bool crossfade = true)
    {
        if (!_tracksByCategory.TryGetValue(category, out var tracks) || tracks.Count == 0)
        {
            Log.Warn(LogCategory.Audio, $"No tracks in category: {category}");
            return;
        }

        // Pick random track from category (avoiding current if possible)
        var candidates = tracks.Where(t => t != CurrentTrackId).ToList();
        if (candidates.Count == 0) candidates = tracks;

        var trackId = candidates[_rng.Next(candidates.Count)];
        Play(trackId, crossfade);
    }

    public void Stop(bool fadeOut = true)
    {
        if (State == MusicState.Stopped) return;
        if (_activePlayer == null) return;

        if (fadeOut)
        {
            _activePlayer.FadeOut(_config.CrossfadeDuration);
        }
        else
        {
            _activePlayer.Stop();
        }

        State = MusicState.Stopped;
        CurrentTrackId = null;

        Log.Info(LogCategory.Audio, "Music stopped");
    }

    public void Pause()
    {
        if (State != MusicState.Playing) return;
        if (_activePlayer == null) return;
        _activePlayer.SetPaused(true);
        State = MusicState.Paused;
    }

    public void Resume()
    {
        if (State != MusicState.Paused) return;
        if (_activePlayer == null) return;
        _activePlayer.SetPaused(false);
        State = MusicState.Playing;
    }


    /// <summary>
    /// Returns true if there are any tracks configured.
    /// </summary>
    public bool HasTracks => _tracks.Count > 0;

}
