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

    public ShaderBackground()
    {
        _startTime = DateTime.UtcNow;

        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://MiloMusicPlayer/Assets/RainbowNoise.sksl"));
            using var reader = new StreamReader(stream);
            string shaderSource = reader.ReadToEnd();

            string errors;
            _effect = SKRuntimeEffect.CreateShader(shaderSource, out errors);
            Console.WriteLine("Shader loaded successfully.");
            if (_effect == null)
            {
                _shaderError = errors;
                Console.WriteLine("Shader compilation error: " + errors);
            }
        }
        catch (Exception ex)
        {
            _shaderError = ex.Message;
            Console.WriteLine("Error loading shader: " + ex);
        }

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(4)
        };

        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (_effect == null)
        {   
            // fallback so the app still opens
            context.FillRectangle(
                new SolidColorBrush(Color.Parse("#070B14")),
                Bounds
            );
            return;
        }

        float time = (float)(DateTime.UtcNow - _startTime).TotalSeconds;
        context.Custom(new ShaderDrawOperation(Bounds, _effect, time));
    }

    private sealed class ShaderDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly SKRuntimeEffect _effect;
        private readonly float _time;

        public ShaderDrawOperation(Rect bounds, SKRuntimeEffect effect, float time)
        {
            _bounds = bounds;
            _effect = effect;
            _time = time;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => false;

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null)
            return;
        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;
        if (canvas == null)
            return;

        var builder = new SKRuntimeShaderBuilder(_effect);
        builder.Uniforms["iTime"] = _time;
        builder.Uniforms["iResolution"] = new float[]
        {
            (float)_bounds.Width,
            (float)_bounds.Height
        };

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