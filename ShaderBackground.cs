using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace MiloMusicPlayer;

public class ShaderBackground : Control
{
    private readonly DispatcherTimer _timer;
    private readonly DateTime _startTime;
    private SKRuntimeEffect? _effect;
    private string? _shaderError;

    public float[] FftBands { get; set; } = new float[8];

    public ShaderBackground()
    {
        _startTime = DateTime.UtcNow;
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://MiloMusicPlayer/Assets/RainbowNoise.sksl"));
            using var reader = new StreamReader(stream);
            string shaderSource = reader.ReadToEnd();
            LoadShaderSource(shaderSource);
        }
        catch (Exception ex)
        {
            _shaderError = ex.Message;
        }

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
    }

    public string? ShaderError => _shaderError;

    public bool LoadShaderSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            _shaderError = "Shader source is empty.";
            _effect = null;
            Dispatcher.UIThread.InvokeAsync(() => InvalidateVisual());
            return false;
        }

        try
        {
            string errors;
            var effect = SKRuntimeEffect.CreateShader(source, out errors);
            if (effect == null)
            {
                _shaderError = errors;
                _effect = null;
                Dispatcher.UIThread.InvokeAsync(() => InvalidateVisual());
                return false;
            }

            _effect = effect;
            _shaderError = null;
            Dispatcher.UIThread.InvokeAsync(() => InvalidateVisual());
            return true;
        }
        catch (Exception ex)
        {
            _shaderError = ex.Message;
            _effect = null;
            Dispatcher.UIThread.InvokeAsync(() => InvalidateVisual());
            return false;
        }
    }

    public bool LoadShaderFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _shaderError = "Shader file path is empty.";
            _effect = null;
            Dispatcher.UIThread.InvokeAsync(() => InvalidateVisual());
            return false;
        }

        if (!File.Exists(filePath))
        {
            _shaderError = $"Shader file not found: {filePath}";
            _effect = null;
            Dispatcher.UIThread.InvokeAsync(() => InvalidateVisual());
            return false;
        }

        try
        {
            var source = File.ReadAllText(filePath);
            return LoadShaderSource(source);
        }
        catch (Exception ex)
        {
            _shaderError = ex.Message;
            _effect = null;
            Dispatcher.UIThread.InvokeAsync(() => InvalidateVisual());
            return false;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (_effect == null)
        {
            context.FillRectangle(
                new SolidColorBrush(Color.Parse("#070B14")),
                Bounds
            );
            return;
        }

        float time = (float)(DateTime.UtcNow - _startTime).TotalSeconds;
        var bands = (float[])FftBands.Clone();
        context.Custom(new ShaderDrawOperation(Bounds, _effect, time, bands));
    }

    private sealed class ShaderDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly SKRuntimeEffect _effect;
        private readonly float _time;
        private readonly float[] _bands;

        public ShaderDrawOperation(Rect bounds, SKRuntimeEffect effect, float time, float[] bands)
        {
            _bounds = bounds;
            _effect = effect;
            _time = time;
            _bands = bands;
        }

        public Rect Bounds => _bounds;
        public bool HitTest(Point p) => false;
        public void Dispose() { }
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            if (canvas == null) return;

            var builder = new SKRuntimeShaderBuilder(_effect);
            builder.Uniforms["iTime"] = _time;
            builder.Uniforms["iResolution"] = new float[]
            {
                (float)_bounds.Width,
                (float)_bounds.Height
            };
            for (int i = 0; i < 8; i++)
            {
                float value = i < _bands.Length ? _bands[i] : 0f;
                builder.Uniforms[$"iBand{i}"] = value;
            }
            
            using var shader = builder.Build();
            using var paint = new SKPaint
            {
                Shader = shader,
                IsAntialias = true
            };

            int saveCount = canvas.Save();
            canvas.Translate((float)_bounds.X, (float)_bounds.Y);
            canvas.DrawRect(
                new SKRect(0, 0, (float)_bounds.Width, (float)_bounds.Height),
                paint
            );
            canvas.RestoreToCount(saveCount);
        }
    }
}
