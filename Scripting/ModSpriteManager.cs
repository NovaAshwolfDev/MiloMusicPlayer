using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace MiloMusicPlayer.Scripting;

public sealed class ModSpriteManager
{
    private readonly Canvas _canvas;
    private readonly Dictionary<InstanceValue, ModSprite> _sprites = new();

    public ModSpriteManager(Canvas canvas)
    {
        _canvas = canvas;
    }

    public void RegisterSprite(InstanceValue spriteInstance, string sourcePath, float x, float y)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var modSprite = new ModSprite(_canvas, spriteInstance, sourcePath, x, y);
            _sprites[spriteInstance] = modSprite;
            modSprite.AddToCanvas();
        });
    }

    public void DestroySprite(InstanceValue spriteInstance)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_sprites.Remove(spriteInstance, out var sprite))
                sprite.RemoveFromCanvas();
        });
    }

    public void UpdateSprites()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var sprite in _sprites.Values)
                sprite.SyncFromInstance();
        });
    }

    public void ClearAll()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var sprite in _sprites.Values)
                sprite.RemoveFromCanvas();
            _sprites.Clear();
        });
    }

    private sealed class ModSprite
    {
        private readonly Canvas _canvas;
        private readonly InstanceValue _instance;
        private readonly Image _image;
        private readonly Border _placeholder;
        private string _sourcePath;

        public ModSprite(Canvas canvas, InstanceValue instance, string sourcePath, float x, float y)
        {
            _canvas = canvas;
            _instance = instance;
            _sourcePath = sourcePath;
            _image = new Image
            {
                Stretch = Stretch.Fill,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
            };
            _placeholder = new Border
            {
                Background = null,
                Child = _image
            };

            SetField("x", new FloatValue(x));
            SetField("y", new FloatValue(y));
            SetField("opacity", new FloatValue(1f));
            SetField("scale", new FloatValue(1f));
            SetField("rotation", new FloatValue(0f));
            SetField("visible", BoolValue.True);
            SetField("source", new StringValue(sourcePath));
            SetField("width", new FloatValue(0f));
            SetField("height", new FloatValue(0f));

            LoadBitmap(sourcePath);
            SyncFromInstance();
        }

        public void AddToCanvas()
        {
            _canvas.Children.Add(_placeholder);
        }

        public void RemoveFromCanvas()
        {
            _canvas.Children.Remove(_placeholder);
            _image.Source = null;
        }

        public void SyncFromInstance()
        {
            var x = GetFloatField("x", 0f);
            var y = GetFloatField("y", 0f);
            var opacity = GetFloatField("opacity", 1f);
            var scale = GetFloatField("scale", 1f);
            var rotation = GetFloatField("rotation", 0f);
            var visible = GetBoolField("visible", true);
            var source = GetStringField("source", _sourcePath);
            var width = GetFloatField("width", 0f);
            var height = GetFloatField("height", 0f);

            if (!string.Equals(source, _sourcePath, StringComparison.OrdinalIgnoreCase))
                LoadBitmap(source);

            _placeholder.IsVisible = visible;
            _image.Opacity = Math.Clamp(opacity, 0f, 1f);
            _image.Width = width > 0 ? width : (_image.Source as Bitmap)?.PixelSize.Width ?? _image.Width;
            _image.Height = height > 0 ? height : (_image.Source as Bitmap)?.PixelSize.Height ?? _image.Height;
            _image.RenderTransform = new TransformGroup
            {
                Children = new Transforms
                {
                    new ScaleTransform(scale, scale),
                    new RotateTransform(rotation)
                }
            };

            Canvas.SetLeft(_placeholder, x);
            Canvas.SetTop(_placeholder, y);
        }

        private void LoadBitmap(string sourcePath)
        {
            _sourcePath = sourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                _image.Source = null;
                return;
            }

            try
            {
                _image.Source = new Bitmap(sourcePath);
            }
            catch
            {
                _image.Source = null;
            }
        }

        private void SetField(string name, MiloValue value)
        {
            _instance.Fields[name] = value;
        }

        private float GetFloatField(string name, float defaultValue)
        {
            if (_instance.Fields.TryGetValue(name, out var value))
            {
                return value switch
                {
                    FloatValue fv => fv.Value,
                    IntValue iv   => iv.Value,
                    _ => defaultValue
                };
            }
            return defaultValue;
        }

        private bool GetBoolField(string name, bool defaultValue)
        {
            if (_instance.Fields.TryGetValue(name, out var value) && value is BoolValue bv)
                return bv.Value;
            return defaultValue;
        }

        private string GetStringField(string name, string defaultValue)
        {
            if (_instance.Fields.TryGetValue(name, out var value) && value is StringValue sv)
                return sv.Value;
            return defaultValue;
        }
    }
}
