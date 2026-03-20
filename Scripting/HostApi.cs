using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MiloMusicPlayer.Scripting;

public sealed class HostApi
{
    private readonly RuntimeScope    _scope;
    private readonly TypeEnvironment _types;

    public string ModFolderPath { get; set; } = "";

    public Action<string>?        LogHandler       { get; set; }
    public Func<PlayerState>?     GetPlayerState   { get; set; }
    public Func<float>?           GetVolume        { get; set; }
    public Action<float>?         SetVolume        { get; set; }
    public Action?                RequestPlay      { get; set; }
    public Action?                RequestPause     { get; set; }
    public Action?                RequestNext      { get; set; }
    public Action?                RequestPrev      { get; set; }
    public Func<List<TrackInfo>>? GetQueue         { get; set; }
    public Action<string>?        QueueTrackByPath { get; set; }
    public Action<string>?        PlaySound        { get; set; }
    public Action<string>?        PlayTrack        { get; set; }

    public HostApi(RuntimeScope scope, TypeEnvironment types)
    {
        _scope = scope;
        _types = types;
    }

    public void Register()
    {
        RegisterTypes();
        RegisterGlobalFunctions();
    }

    private void RegisterTypes()
    {
        var listType = new ClassType("List");
        listType.TypeParams.Add("T");
        _types.Register(listType);

        var trackClass = new ClassType("Track");
        trackClass.Fields["title"]    = PrimitiveType.String;
        trackClass.Fields["artist"]   = PrimitiveType.String;
        trackClass.Fields["album"]    = PrimitiveType.String;
        trackClass.Fields["genre"]    = PrimitiveType.String;
        trackClass.Fields["duration"] = PrimitiveType.Float;
        trackClass.Fields["path"]     = PrimitiveType.String;
        _types.Register(trackClass);

        var stateClass = new ClassType("PlayerState");
        stateClass.Fields["isPlaying"] = PrimitiveType.Bool;
        stateClass.Fields["volume"]    = PrimitiveType.Float;
        stateClass.Fields["position"]  = PrimitiveType.Float;
        _types.Register(stateClass);

        var pathsClass = new ClassType("Paths");
        pathsClass.Fields["music"]  = PrimitiveType.String;
        pathsClass.Fields["sounds"] = PrimitiveType.String;
        pathsClass.Fields["images"] = PrimitiveType.String;
        pathsClass.Methods["join"]  = new FunctionType(new() { PrimitiveType.String, PrimitiveType.String }, PrimitiveType.String);
        _types.Register(pathsClass);

        var imod = new InterfaceType("IMod");
        RegisterIModMethods(imod);
        _types.Register(imod);

        _scope.Define("Track", new InstanceFactory("Track", args =>
        {
            var inst = new InstanceValue(trackClass);
            inst.Fields["title"]    = args.Count > 0 ? args[0] : new StringValue("");
            inst.Fields["artist"]   = args.Count > 1 ? args[1] : new StringValue("");
            inst.Fields["album"]    = args.Count > 2 ? args[2] : new StringValue("");
            inst.Fields["genre"]    = args.Count > 3 ? args[3] : new StringValue("");
            inst.Fields["duration"] = args.Count > 4 ? args[4] : new FloatValue(0f);
            inst.Fields["path"]     = args.Count > 5 ? args[5] : new StringValue("");
            return inst;
        }));
    }

    private void RegisterIModMethods(InterfaceType imod)
    {
        var trackType = (ClassType)_types.Resolve("Track")!;
        imod.Methods["onLoad"]         = new FunctionType(new(), PrimitiveType.Void);
        imod.Methods["onUnload"]       = new FunctionType(new(), PrimitiveType.Void);
        imod.Methods["onPlay"]         = new FunctionType(new() { trackType }, PrimitiveType.Void);
        imod.Methods["onPause"]        = new FunctionType(new(), PrimitiveType.Void);
        imod.Methods["onTrackChange"]  = new FunctionType(new() { trackType }, PrimitiveType.Void);
        imod.Methods["onVolumeChange"] = new FunctionType(new() { PrimitiveType.Float }, PrimitiveType.Void);
        imod.Methods["update"]         = new FunctionType(new() { PrimitiveType.Float }, PrimitiveType.Void);
    }

    private void RegisterGlobalFunctions()
    {
        RegisterFn("log", args =>
        {
            var msg = args.Count > 0 ? args[0].Display() : "";
            LogHandler?.Invoke(msg);
            return NullValue.Instance;
        });

        RegisterFn("toString", args => new StringValue(args.Count > 0 ? args[0].Display() : "null"));

        RegisterFn("toInt", args =>
        {
            if (args.Count == 0) return new IntValue(0);
            return args[0] switch
            {
                IntValue    iv                                                    => iv,
                FloatValue  fv                                                    => new IntValue((int)fv.Value),
                StringValue sv when int.TryParse(sv.Value, out var n)             => new IntValue(n),
                _                                                                 => new IntValue(0)
            };
        });

        RegisterFn("toFloat", args =>
        {
            if (args.Count == 0) return new FloatValue(0f);
            return args[0] switch
            {
                FloatValue  fv                                                                         => fv,
                IntValue    iv                                                                         => new FloatValue(iv.Value),
                StringValue sv when float.TryParse(sv.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f) => new FloatValue(f),
                _                                                                                      => new FloatValue(0f)
            };
        });

        RegisterFn("play",  _ => { RequestPlay?.Invoke();  return NullValue.Instance; });
        RegisterFn("pause", _ => { RequestPause?.Invoke(); return NullValue.Instance; });
        RegisterFn("next",  _ => { RequestNext?.Invoke();  return NullValue.Instance; });
        RegisterFn("prev",  _ => { RequestPrev?.Invoke();  return NullValue.Instance; });

        RegisterFn("getVolume", _ => new FloatValue(GetVolume?.Invoke() ?? 1f));

        RegisterFn("setVolume", args =>
        {
            if (args.Count > 0 && args[0] is FloatValue fv)
                SetVolume?.Invoke(Math.Clamp(fv.Value, 0f, 1f));
            return NullValue.Instance;
        });

        RegisterFn("getPlayerState", _ =>
        {
            var state     = GetPlayerState?.Invoke() ?? new PlayerState();
            var stateType = (ClassType)_types.Resolve("PlayerState")!;
            var inst      = new InstanceValue(stateType);
            inst.Fields["isPlaying"] = state.IsPlaying ? BoolValue.True : BoolValue.False;
            inst.Fields["volume"]    = new FloatValue(state.Volume);
            inst.Fields["position"]  = new FloatValue(state.Position);
            return inst;
        });

        RegisterFn("getQueue", _ =>
        {
            var tracks    = GetQueue?.Invoke() ?? new List<TrackInfo>();
            var trackType = (ClassType)_types.Resolve("Track")!;
            var list      = new ListValue();
            foreach (var t in tracks)
            {
                var inst = new InstanceValue(trackType);
                inst.Fields["title"]    = new StringValue(t.Title);
                inst.Fields["artist"]   = new StringValue(t.Artist);
                inst.Fields["album"]    = new StringValue(t.Album);
                inst.Fields["genre"]    = new StringValue(t.Genre);
                inst.Fields["duration"] = new FloatValue(t.Duration);
                inst.Fields["path"]     = new StringValue(t.Path);
                list.Items.Add(inst);
            }
            return list;
        });

        RegisterFn("queueTrack", args =>
        {
            if (args.Count > 0 && args[0] is StringValue sv)
                QueueTrackByPath?.Invoke(sv.Value);
            return NullValue.Instance;
        });

        RegisterFn("playTrack", args =>
        {
            if (args.Count > 0 && args[0] is StringValue sv)
                PlayTrack?.Invoke(sv.Value);
            return NullValue.Instance;
        });

        RegisterFn("playSound", args =>
        {
            if (args.Count > 0 && args[0] is StringValue sv)
                PlaySound?.Invoke(sv.Value);
            return NullValue.Instance;
        });

        RegisterFn("listSize", args =>
        {
            if (args.Count > 0 && args[0] is ListValue lv)
                return new IntValue(lv.Items.Count);
            return new IntValue(0);
        });

        RegisterFn("contains", args =>
        {
            if (args.Count < 2) return BoolValue.False;
            var str = (args[0] as StringValue)?.Value ?? "";
            var sub = (args[1] as StringValue)?.Value ?? "";
            return str.Contains(sub) ? BoolValue.True : BoolValue.False;
        });

        RegisterFn("now",       _ => new StringValue(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        RegisterFn("timestamp", _ => new FloatValue((float)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000f));
        RegisterFn("year",      _ => new IntValue(DateTime.Now.Year));
        RegisterFn("month",     _ => new IntValue(DateTime.Now.Month));
        RegisterFn("day",       _ => new IntValue(DateTime.Now.Day));
        RegisterFn("hour",      _ => new IntValue(DateTime.Now.Hour));
        RegisterFn("minute",    _ => new IntValue(DateTime.Now.Minute));
        RegisterFn("second",    _ => new IntValue(DateTime.Now.Second));

        // Paths object — scoped to the mod's own folder
        var pathsType = (ClassType)_types.Resolve("Paths")!;
        var pathsInst = new InstanceValue(pathsType);
        pathsInst.Fields["music"]  = new StringValue(Path.Combine(ModFolderPath, "Music"));
        pathsInst.Fields["sounds"] = new StringValue(Path.Combine(ModFolderPath, "Sounds"));
        pathsInst.Fields["images"] = new StringValue(Path.Combine(ModFolderPath, "Images"));
        pathsInst.Fields["__method_join"] = new NativeFunctionValue("join", args =>
        {
            var parts = args.OfType<StringValue>().Select(s => s.Value).ToArray();
            return new StringValue(parts.Length > 0 ? Path.Combine(parts) : "");
        });
        _scope.Define("Paths", pathsInst);
    }

    private void RegisterFn(string name, Func<List<MiloValue>, MiloValue> fn)
    {
        _scope.Define(name, new NativeFunctionValue(name, fn));
    }

    public InstanceValue TrackToInstance(TrackInfo track)
    {
        var trackType = (ClassType)_types.Resolve("Track")!;
        var inst      = new InstanceValue(trackType);
        inst.Fields["title"]    = new StringValue(track.Title);
        inst.Fields["artist"]   = new StringValue(track.Artist);
        inst.Fields["album"]    = new StringValue(track.Album);
        inst.Fields["genre"]    = new StringValue(track.Genre);
        inst.Fields["duration"] = new FloatValue(track.Duration);
        inst.Fields["path"]     = new StringValue(track.Path);
        return inst;
    }
}

public sealed class TrackInfo
{
    public string Title    { get; init; } = "";
    public string Artist   { get; init; } = "";
    public string Album    { get; init; } = "";
    public string Genre    { get; init; } = "";
    public float  Duration { get; init; }
    public string Path     { get; init; } = "";
}

public sealed class PlayerState
{
    public bool  IsPlaying { get; init; }
    public float Volume    { get; init; }
    public float Position  { get; init; }
}