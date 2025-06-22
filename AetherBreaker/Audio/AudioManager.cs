using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AetherBreaker.Windows;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AetherBreaker.Audio;

public class AudioManager : IDisposable
{
    private readonly Configuration configuration;
    private WaveOutEvent? bgmOutputDevice;
    private Mp3FileReader? bgmFileReader;

    private readonly List<string> allMusicTracks = new();
    private readonly List<string> bgmPlaylist = new();
    private int currentTrackIndex = -1;
    private bool isBgmPlaying = false;

    private readonly WaveOutEvent sfxOutputDevice;
    private readonly MixingSampleProvider sfxMixer;

    public AudioManager(Configuration configuration)
    {
        this.configuration = configuration;

        var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        this.sfxMixer = new MixingSampleProvider(mixerFormat) { ReadFully = true };

        this.sfxOutputDevice = new WaveOutEvent();
        this.sfxOutputDevice.Init(this.sfxMixer);
        this.sfxOutputDevice.Play();

        DiscoverMusicTracks();
    }

    public void PlaySfx(string sfxName)
    {
        if (this.configuration.IsSfxMuted) return;

        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = $"AetherBreaker.Sfx.{sfxName}";

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null) return;

            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            WaveStream readerStream = sfxName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                ? new Mp3FileReader(memoryStream)
                : new WaveFileReader(memoryStream);

            ISampleProvider soundToPlay = readerStream.ToSampleProvider();

            if (soundToPlay.WaveFormat.SampleRate != this.sfxMixer.WaveFormat.SampleRate ||
                soundToPlay.WaveFormat.Channels != this.sfxMixer.WaveFormat.Channels)
            {
                var resampler = new WdlResamplingSampleProvider(soundToPlay, this.sfxMixer.WaveFormat.SampleRate);
                soundToPlay = resampler.WaveFormat.Channels != this.sfxMixer.WaveFormat.Channels
                    ? new MonoToStereoSampleProvider(resampler)
                    : resampler;
            }

            this.sfxMixer.AddMixerInput(soundToPlay);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to play SFX: {sfxName}");
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (this.bgmOutputDevice != null)
        {
            this.bgmOutputDevice.Volume = Math.Clamp(volume, 0f, 1f);
        }
    }

    private void DiscoverMusicTracks()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourcePrefix = "AetherBreaker.Music.";
        this.allMusicTracks.AddRange(
            assembly.GetManifestResourceNames()
                .Where(r => r.StartsWith(resourcePrefix) && r.EndsWith(".mp3"))
                .Select(r => r.Substring(resourcePrefix.Length))
                .OrderBy(r => r)
        );
    }

    public void StartBgmPlaylist()
    {
        this.bgmPlaylist.Clear();
        var defaultTracks = this.allMusicTracks.Where(t => !t.StartsWith("bonus_")).ToList();
        this.bgmPlaylist.AddRange(defaultTracks);

        // Load any tracks that were previously unlocked from the config file
        foreach (var trackNumber in this.configuration.UnlockedBonusTracks)
        {
            var trackName = $"bonus_{trackNumber}.mp3";
            if (this.allMusicTracks.Contains(trackName) && !this.bgmPlaylist.Contains(trackName))
            {
                this.bgmPlaylist.Add(trackName);
            }
        }

        isBgmPlaying = true;
        if (this.configuration.IsBgmMuted || !this.bgmPlaylist.Any()) return;
        currentTrackIndex = 0;
        PlayTrack(currentTrackIndex);
    }

    public void UnlockBonusTrack(int trackNumber)
    {
        var trackName = $"bonus_{trackNumber}.mp3";
        // Check if the track exists and isn't already in the playlist
        if (this.allMusicTracks.Contains(trackName) && !this.bgmPlaylist.Contains(trackName))
        {
            this.bgmPlaylist.Add(trackName);
            // Add the track number to the configuration and save it
            if (this.configuration.UnlockedBonusTracks.Add(trackNumber))
            {
                this.configuration.Save();
            }
        }
    }

    public void PlayNextTrack()
    {
        if (!this.bgmPlaylist.Any()) return;
        this.currentTrackIndex = (this.currentTrackIndex + 1) % this.bgmPlaylist.Count;
        PlayTrack(this.currentTrackIndex);
    }

    public void PlayPreviousTrack()
    {
        if (!this.bgmPlaylist.Any()) return;
        this.currentTrackIndex--;
        if (this.currentTrackIndex < 0)
        {
            this.currentTrackIndex = this.bgmPlaylist.Count - 1;
        }
        PlayTrack(this.currentTrackIndex);
    }

    private void PlayTrack(int trackIndex)
    {
        StopBgm();
        if (trackIndex < 0 || trackIndex >= this.bgmPlaylist.Count) return;

        this.currentTrackIndex = trackIndex;
        var bgmName = this.bgmPlaylist[trackIndex];
        var resourcePath = $"AetherBreaker.Music.{bgmName}";

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null) return;
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            this.bgmFileReader = new Mp3FileReader(memoryStream);
            this.bgmOutputDevice = new WaveOutEvent();
            this.bgmOutputDevice.PlaybackStopped += OnBgmPlaybackStopped;
            this.bgmOutputDevice.Init(this.bgmFileReader);
            this.bgmOutputDevice.Volume = this.configuration.MusicVolume;
            this.bgmOutputDevice.Play();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to play BGM: {bgmName}");
        }
    }

    private void OnBgmPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (this.bgmOutputDevice?.PlaybackState == PlaybackState.Stopped)
        {
            currentTrackIndex++;
            if (currentTrackIndex >= this.bgmPlaylist.Count)
                currentTrackIndex = 0;
            PlayTrack(currentTrackIndex);
        }
    }

    public void UpdateBgmState()
    {
        if (this.configuration.IsBgmMuted)
        {
            if (this.bgmOutputDevice?.PlaybackState == PlaybackState.Playing)
                this.bgmOutputDevice.Pause();
        }
        else
        {
            if (this.bgmOutputDevice != null) this.bgmOutputDevice.Volume = this.configuration.MusicVolume;

            if (this.bgmOutputDevice?.PlaybackState == PlaybackState.Paused)
                this.bgmOutputDevice.Play();
            else if (this.isBgmPlaying && this.bgmOutputDevice == null)
                StartBgmPlaylist();
        }
    }

    public void StopBgm()
    {
        if (this.bgmOutputDevice != null)
        {
            this.bgmOutputDevice.PlaybackStopped -= OnBgmPlaybackStopped;
            this.bgmOutputDevice.Stop();
            this.bgmOutputDevice.Dispose();
            this.bgmOutputDevice = null;
        }
        this.bgmFileReader?.Dispose();
        this.bgmFileReader = null;
    }

    public void EndPlaylist()
    {
        this.isBgmPlaying = false;
        StopBgm();
    }

    public void Dispose()
    {
        isBgmPlaying = false;
        StopBgm();
        this.sfxOutputDevice.Dispose();
    }
}
