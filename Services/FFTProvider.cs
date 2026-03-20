using NAudio.Dsp;
using NAudio.Wave;
using System;

namespace MiloMusicPlayer.Services;

public class FftProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _fftLength;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _window;
    private int _fftPos;
    private readonly object _lock = new();

    public float[] FftData { get; }
    public WaveFormat WaveFormat => _source.WaveFormat;

    public FftProvider(ISampleProvider source, int fftLength = 1024)
    {
        _source = source;
        _fftLength = fftLength;
        _fftBuffer = new Complex[fftLength];
        _window = new float[fftLength];
        FftData = new float[fftLength / 2];

        for (int i = 0; i < fftLength; i++)
            _window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (fftLength - 1))));
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);

        for (int i = 0; i < read; i++)
        {
            float sample = buffer[offset + i];

            _fftBuffer[_fftPos].X = sample * _window[_fftPos];
            _fftBuffer[_fftPos].Y = 0;
            _fftPos++;

            if (_fftPos >= _fftLength)
            {
                _fftPos = 0;
                var copy = (Complex[])_fftBuffer.Clone();
                FastFourierTransform.FFT(true, (int)Math.Log(_fftLength, 2), copy);

                lock (_lock)
                {
                    for (int j = 0; j < FftData.Length; j++)
                        FftData[j] = (float)Math.Sqrt(copy[j].X * copy[j].X + copy[j].Y * copy[j].Y);
                }
            }
        }

        return read;
    }

    public float[] GetBands(int bandCount = 8)
    {
        var bands = new float[bandCount];
        lock (_lock)
        {
            int usable = FftData.Length / 2;
            int perBand = usable / bandCount;
            for (int b = 0; b < bandCount; b++)
            {
                float sum = 0;
                for (int i = 0; i < perBand; i++)
                    sum += FftData[b * perBand + i];
                bands[b] = Math.Min(1f, sum / perBand * 40f);
            }
        }
        return bands;
    }
}