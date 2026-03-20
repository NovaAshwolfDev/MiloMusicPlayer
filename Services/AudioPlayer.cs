using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Vorbis;
using System;
using System.IO;


namespace MiloMusicPlayer.Services;

public class AudioPlayer
{
    private IWavePlayer? output;
    private WaveStream? reader;
    private readonly object _lock = new object();
    public FftProvider? Fft { get; private set; }
    private float _volume = 1f;
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            if (output is not null)
                output.Volume = value;
        }
    }
    public void Play(string path)
    {
        try
        {
            Stop();

            var ext = Path.GetExtension(path).ToLowerInvariant();
            reader = ext switch
            {
                ".ogg" => new VorbisWaveReader(path),
                _      => new AudioFileReader(path)
            };

            Fft = new FftProvider(reader.ToSampleProvider());
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            output = new WasapiOut(device, AudioClientShareMode.Shared, true, 300);
            output.Init(Fft);
            output.Play();
        }
        catch (Exception ex)
        {
            Stop();
            throw new Exception($"Failed to play audio '{path}': {ex.Message}", ex);
        }
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

        if (Fft == null || Fft.FftData == null)
            return bands;

        lock (_lock)
        {
            int usable = Fft.FftData.Length / 2;
            int perBand = usable / bands.Length;

            if (perBand <= 0)
                return bands;

            for (int b = 0; b < bands.Length; b++)
            {
                float sum = 0;
                for (int i = 0; i < perBand; i++)
                    sum += Fft.FftData[b * perBand + i];

                bands[b] = Math.Min(1f, sum / perBand * 40f);
            }
        }

        return bands;
    }

    public TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;
}