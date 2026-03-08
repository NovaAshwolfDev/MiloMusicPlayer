using NAudio.Wave;
using System;

namespace MiloMusicPlayer.Services;

public class AudioPlayer
{
    private WaveOutEvent? output;
    private AudioFileReader? reader;

    public void Play(string path)
    {
        Stop();

        reader = new AudioFileReader(path);

        output = new WaveOutEvent
        {
            DesiredLatency = 300
        };

        output.Init(reader);
        output.Play();
    }

    public void Stop()
    {
        output?.Stop();
        output?.Dispose();
        output = null;

        reader?.Dispose();
        reader = null;
    }

    public void Pause()
    {
        output?.Pause();
    }

    public void Resume()
    {
        output?.Play();
    }

    public TimeSpan Position
    {
        get => reader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (reader != null)
                reader.CurrentTime = value;
        }
    }

    public TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;
}