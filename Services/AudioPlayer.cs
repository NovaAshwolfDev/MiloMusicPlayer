using NAudio.Wave;
using System;

namespace MiloMusicPlayer.Services;

public class AudioPlayer
{
    private WaveOutEvent? output;
    private AudioFileReader? reader;
    private readonly object _lock = new object();

    public FftProvider? Fft { get; private set; }

    public void Play(string path)
    {
        Stop();
        reader = new AudioFileReader(path);
        Fft = new FftProvider(reader.ToSampleProvider());
        output = new WaveOutEvent
        {
            DesiredLatency = 300
        };
        output.Init(Fft);
        output.Play();
    }

    public void Stop()
    {
        output?.Stop();
        output?.Dispose();
        output = null;
        reader?.Dispose();
        reader = null;
        Fft = null;
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
    public float[] GetFftBands()
    {
        var bands = new float[8];
        lock (_lock)
        {
            // Focus on lower half of spectrum (more musically relevant)
            int usable = Fft.FftData.Length / 2;
            int perBand = usable / bands.Length;
            for (int b = 0; b < bands.Length; b++)
            {
                float sum = 0;
                for (int i = 0; i < perBand; i++)
                    sum += Fft.FftData[b * perBand + i];
                // Normalize and boost
                bands[b] = Math.Min(1f, sum / perBand * 40f);
            }
        }
        return bands;
    }
    public TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;
}