// Milo/AnimationHelper.cs
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;


namespace Milo.Helpers;

public static class AnimationHelper
{
    private static double EaseInOut(double t)
    {
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    public static async Task Animate(Control control, double from, double to, int durationMs)
    {
        control.Opacity = from;
        var start = DateTime.UtcNow;

        while (true)
        {
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);
            control.Opacity = from + (to - from) * EaseInOut(t);
            if (t >= 1) break;
            await Task.Delay(8);
        }

        control.Opacity = to;
    }

    public static async Task AnimateX(Control control, double from, double to, int durationMs)
    {
        control.RenderTransform = new TranslateTransform(from, 0);
        var start = DateTime.UtcNow;

        while (true)
        {
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);
            var x = from + (to - from) * EaseInOut(t);
            control.RenderTransform = new TranslateTransform(x, 0);
            if (t >= 1) break;
            await Task.Delay(8);
        }

        control.RenderTransform = new TranslateTransform(to, 0);
    }
    public static async Task AnimateY(Control control, double from, double to, int durationMs)
    {
        var transform = control.RenderTransform as TranslateTransform;
        double currentX = transform?.X ?? 0;
        if (transform == null)
        {
            transform = new TranslateTransform(currentX, from);
            control.RenderTransform = transform;
        }
        else
        {
            transform.Y = from;
        }

        var start = DateTime.UtcNow;
        var tcs = new TaskCompletionSource();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };

        timer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);
            var e = EaseInOut(t);
            var y = from + (to - from) * e;

            control.RenderTransform = new TranslateTransform(currentX, y);

            if (t >= 1)
            {
                timer.Stop();
                tcs.SetResult();
            }
        };

        timer.Start();
        await tcs.Task;
    }
    public static Task AnimateMany(int durationMs, params (Control control, string property, double from, double to)[] targets)
    {
        var tcs = new TaskCompletionSource();
        var start = DateTime.UtcNow;

        foreach (var (control, property, from, _) in targets)
        {
            if (property == "Opacity") control.Opacity = from;
            else if (property == "X") control.Margin = new Thickness(from, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
            else if (property == "Y") control.RenderTransform = new TranslateTransform(0, from);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        timer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);
            var e = EaseInOut(t);

            foreach (var (control, property, from, to) in targets)
            {
                var val = from + (to - from) * e;
                if (property == "Opacity") control.Opacity = val;
                else if (property == "X") control.Margin = new Thickness(val, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
                else if (property == "Y")
                {
                    var current = (control.RenderTransform as TranslateTransform);
                    control.RenderTransform = new TranslateTransform(current?.X ?? 0, val);
                }
            }

            if (t >= 1)
            {
                timer.Stop();
                tcs.SetResult();
            }
        };

        timer.Start();
        return tcs.Task;
    }
}