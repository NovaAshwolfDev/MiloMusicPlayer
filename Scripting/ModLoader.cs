using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiloMusicPlayer.Scripting;

public sealed class ModManifest
{
    public string Name        { get; set; } = "";
    public string Version     { get; set; } = "1.0.0";
    public string Author      { get; set; } = "";
    public string Entrypoint  { get; set; } = "main.ms";
    public string Description { get; set; } = "";
}

public sealed class LoadedMod
{
    public ModManifest    Manifest    { get; }
    public string         FolderPath  { get; }
    public InstanceValue? ModInstance { get; set; }
    public FunctionValue? OnLoad         { get; set; }
    public FunctionValue? OnUnload       { get; set; }
    public FunctionValue? OnPlay         { get; set; }
    public FunctionValue? OnPause        { get; set; }
    public FunctionValue? OnTrackChange  { get; set; }
    public FunctionValue? OnVolumeChange { get; set; }
    public FunctionValue? Update         { get; set; }

    public LoadedMod(ModManifest manifest, string folderPath)
    {
        Manifest   = manifest;
        FolderPath = folderPath;
    }
}

public sealed class ModLoader
{
    private readonly string               _modsDirectory;
    private readonly List<LoadedMod>      _loadedMods  = new();
    private readonly System.Timers.Timer  _updateTimer = new(16);
    private DateTime                      _lastUpdate  = DateTime.UtcNow;

    public Action<string>?        Log            { get; set; }
    public Func<PlayerState>?     GetPlayerState { get; set; }
    public Func<float>?           GetVolume      { get; set; }
    public Action<float>?         SetVolume      { get; set; }
    public Action?                RequestPlay    { get; set; }
    public Action?                RequestPause   { get; set; }
    public Action?                RequestNext    { get; set; }
    public Action?                RequestPrev    { get; set; }
    public Func<List<TrackInfo>>? GetQueue       { get; set; }
    public Action<string>?        QueueTrack     { get; set; }
    public Action<string>?        PlayTrack      { get; set; }

    public IReadOnlyList<LoadedMod> LoadedMods => _loadedMods;

    public ModLoader(string modsDirectory)
    {
        _modsDirectory = modsDirectory;
    }

    public void LoadAll()
    {
        if (!Directory.Exists(_modsDirectory))
        {
            Log?.Invoke($"[ModLoader] Mods directory not found: {_modsDirectory}");
            return;
        }

        foreach (var modFolder in Directory.GetDirectories(_modsDirectory))
        {
            try
            {
                var mod = LoadMod(modFolder);
                if (mod is not null)
                    _loadedMods.Add(mod);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[ModLoader] Failed to load mod at '{modFolder}': {ex.Message}");
            }
        }

        _updateTimer.Elapsed += (_, _) =>
        {
            var now   = DateTime.UtcNow;
            var delta = (float)(now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;

            foreach (var mod in _loadedMods)
                FireHook(mod, mod.Update, new List<MiloValue> { new FloatValue(delta) }, mod.Manifest.Name);
        };
        _updateTimer.AutoReset = true;
        _updateTimer.Start();

        Log?.Invoke($"[ModLoader] Loaded {_loadedMods.Count} mod(s).");
    }

    private LoadedMod? LoadMod(string folderPath)
    {
        var manifestPath = Path.Combine(folderPath, "mod.json");
        if (!File.Exists(manifestPath))
        {
            Log?.Invoke($"[ModLoader] Skipping '{folderPath}' — no mod.json found.");
            return null;
        }

        var manifestJson = File.ReadAllText(manifestPath);
        var manifest     = JsonSerializer.Deserialize<ModManifest>(manifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("Failed to parse mod.json");

        Log?.Invoke($"[ModLoader] Loading mod '{manifest.Name}' v{manifest.Version} by {manifest.Author}");

        var entrypointPath = Path.Combine(folderPath, manifest.Entrypoint);
        if (!File.Exists(entrypointPath))
            throw new Exception($"Entrypoint '{manifest.Entrypoint}' not found in mod folder.");

        var allFiles = Directory.GetFiles(folderPath, "*.ms", SearchOption.AllDirectories)
            .OrderBy(f => f == entrypointPath ? 0 : 1)
            .ToList();

        var parsedFiles = new List<ScriptFile>();
        foreach (var filePath in allFiles)
        {
            var source = File.ReadAllText(filePath);

            List<Token> tokens;
            try   { tokens = new Lexer(source).Tokenize(); }
            catch (LexerException ex)
                  { throw new Exception($"Lexer error in '{Path.GetFileName(filePath)}': {ex.Message}"); }

            ScriptFile scriptFile;
            try   { scriptFile = new Parser(tokens).Parse(filePath); }
            catch (ParseException ex)
                  { throw new Exception($"Parse error in '{Path.GetFileName(filePath)}': {ex.Message}"); }

            parsedFiles.Add(scriptFile);
        }

        var typeEnv     = new TypeEnvironment();
        var globalScope = new RuntimeScope();

        var hostApi = new HostApi(globalScope, typeEnv);
        hostApi.ModFolderPath  = folderPath;
        hostApi.LogHandler     = msg => Log?.Invoke($"[Script] {msg}");
        hostApi.GetPlayerState = GetPlayerState;
        hostApi.GetVolume      = GetVolume;
        hostApi.SetVolume      = SetVolume;
        hostApi.RequestPlay    = RequestPlay;
        hostApi.RequestPause   = RequestPause;
        hostApi.RequestNext    = RequestNext;
        hostApi.RequestPrev    = RequestPrev;
        hostApi.GetQueue       = GetQueue;
        hostApi.QueueTrackByPath = QueueTrack;
        hostApi.PlayTrack      = path => PlayTrack?.Invoke(path);
        hostApi.PlaySound      = path =>
        {
            Log?.Invoke($"[Script] playSound path: '{path}'");
            try
            {
                if (!File.Exists(path))
                {
                    Log?.Invoke($"[Script] playSound: file not found at '{path}'");
                    return;
                }

                var ext = Path.GetExtension(path).ToLowerInvariant();
                NAudio.Wave.WaveStream reader = ext switch
                {
                    ".ogg" => new NAudio.Vorbis.VorbisWaveReader(path),
                    _      => new NAudio.Wave.AudioFileReader(path)
                };

                var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                var device     = enumerator.GetDefaultAudioEndpoint(
                    NAudio.CoreAudioApi.DataFlow.Render,
                    NAudio.CoreAudioApi.Role.Multimedia);

                using var output = new NAudio.Wave.WasapiOut(
                    device, NAudio.CoreAudioApi.AudioClientShareMode.Shared, true, 200);

                output.Init(reader);
                output.Play();

                while (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                    System.Threading.Thread.Sleep(50);

                reader.Dispose();
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[Script] playSound error: {ex.Message}");
            }
        };

        hostApi.Register();

        var typeScope = new Scope();
        RegisterHostGlobalsToScope(typeScope, typeEnv);

        var typeChecker = new TypeChecker(typeEnv);
        foreach (var scriptFile in parsedFiles)
            typeChecker.Check(scriptFile, typeScope);

        if (typeChecker.HasErrors)
        {
            foreach (var err in typeChecker.Errors)
                Log?.Invoke($"[ModLoader] Type error in '{manifest.Name}': {err.Message}");
            throw new Exception($"Mod '{manifest.Name}' has {typeChecker.Errors.Count} type error(s). Skipping.");
        }

        var interpreter = new Interpreter(globalScope);
        foreach (var scriptFile in parsedFiles)
        {
            try   { interpreter.Execute(scriptFile); }
            catch (RuntimeException ex)
                  { throw new Exception($"Runtime error in '{manifest.Name}': {ex.Message}"); }
        }

        if (!globalScope.IsDefined("mod"))
            throw new Exception($"Mod '{manifest.Name}' does not define a global 'mod' variable.");

        var modValue = globalScope.Get("mod");
        if (modValue is not InstanceValue modInstance)
            throw new Exception($"Mod '{manifest.Name}': 'mod' is not a class instance.");

        var loadedMod = new LoadedMod(manifest, folderPath) { ModInstance = modInstance };

        ResolveHook(modInstance, "onLoad",         fn => loadedMod.OnLoad         = fn);
        ResolveHook(modInstance, "onUnload",       fn => loadedMod.OnUnload       = fn);
        ResolveHook(modInstance, "onPlay",         fn => loadedMod.OnPlay         = fn);
        ResolveHook(modInstance, "onPause",        fn => loadedMod.OnPause        = fn);
        ResolveHook(modInstance, "onTrackChange",  fn => loadedMod.OnTrackChange  = fn);
        ResolveHook(modInstance, "onVolumeChange", fn => loadedMod.OnVolumeChange = fn);
        ResolveHook(modInstance, "update",         fn => loadedMod.Update         = fn);

        FireHook(loadedMod, loadedMod.OnLoad, new List<MiloValue>(), manifest.Name);

        return loadedMod;
    }

    private static void RegisterHostGlobalsToScope(Scope scope, TypeEnvironment typeEnv)
    {
        var voidFn    = new FunctionType(new(), PrimitiveType.Void);
        var listType  = (ClassType)typeEnv.Resolve("List")!;
        var pathsType = (ClassType)typeEnv.Resolve("Paths")!;

        scope.Define("log",            new FunctionType(new() { UnknownType.Instance }, PrimitiveType.Void));
        scope.Define("toString",       new FunctionType(new() { UnknownType.Instance }, PrimitiveType.String));
        scope.Define("toInt",          new FunctionType(new() { UnknownType.Instance }, PrimitiveType.Int));
        scope.Define("toFloat",        new FunctionType(new() { UnknownType.Instance }, PrimitiveType.Float));
        scope.Define("play",           voidFn);
        scope.Define("pause",          voidFn);
        scope.Define("next",           voidFn);
        scope.Define("prev",           voidFn);
        scope.Define("getVolume",      new FunctionType(new(), PrimitiveType.Float));
        scope.Define("setVolume",      new FunctionType(new() { PrimitiveType.Float }, PrimitiveType.Void));
        scope.Define("getPlayerState", new FunctionType(new(), (MiloType)typeEnv.Resolve("PlayerState")!));
        scope.Define("getQueue",       new FunctionType(new(), listType));
        scope.Define("queueTrack",     new FunctionType(new() { PrimitiveType.String }, PrimitiveType.Void));
        scope.Define("playTrack",      new FunctionType(new() { PrimitiveType.String }, PrimitiveType.Void));
        scope.Define("playSound",      new FunctionType(new() { PrimitiveType.String }, PrimitiveType.Void));
        scope.Define("listSize",       new FunctionType(new() { UnknownType.Instance }, PrimitiveType.Int));
        scope.Define("contains",       new FunctionType(new() { PrimitiveType.String, PrimitiveType.String }, PrimitiveType.Bool));
        scope.Define("now",            new FunctionType(new(), PrimitiveType.String));
        scope.Define("timestamp",      new FunctionType(new(), PrimitiveType.Float));
        scope.Define("year",           new FunctionType(new(), PrimitiveType.Int));
        scope.Define("month",          new FunctionType(new(), PrimitiveType.Int));
        scope.Define("day",            new FunctionType(new(), PrimitiveType.Int));
        scope.Define("hour",           new FunctionType(new(), PrimitiveType.Int));
        scope.Define("minute",         new FunctionType(new(), PrimitiveType.Int));
        scope.Define("second",         new FunctionType(new(), PrimitiveType.Int));
        scope.Define("Paths",          pathsType);
    }

    private static void ResolveHook(InstanceValue inst, string name, Action<FunctionValue> setter)
    {
        var key = $"__method_{name}";
        if (inst.Fields.TryGetValue(key, out var val) && val is FunctionValue fn)
            setter(fn);
    }

    public void UnloadAll()
    {
        foreach (var mod in _loadedMods)
        {
            try   { FireHook(mod, mod.OnUnload, new List<MiloValue>(), mod.Manifest.Name); }
            catch (Exception ex) { Log?.Invoke($"[ModLoader] Error unloading '{mod.Manifest.Name}': {ex.Message}"); }
        }
        _updateTimer.Stop();
        _loadedMods.Clear();
    }

    public void FireOnPlay(TrackInfo track)
    {
        var val = BuildTrackInstance(track);
        foreach (var mod in _loadedMods)
            FireHook(mod, mod.OnPlay, new List<MiloValue> { val }, mod.Manifest.Name);
    }

    public void FireOnPause()
    {
        foreach (var mod in _loadedMods)
            FireHook(mod, mod.OnPause, new List<MiloValue>(), mod.Manifest.Name);
    }

    public void FireOnTrackChange(TrackInfo track)
    {
        var val = BuildTrackInstance(track);
        foreach (var mod in _loadedMods)
            FireHook(mod, mod.OnTrackChange, new List<MiloValue> { val }, mod.Manifest.Name);
    }

    public void FireOnVolumeChange(float volume)
    {
        foreach (var mod in _loadedMods)
            FireHook(mod, mod.OnVolumeChange, new List<MiloValue> { new FloatValue(volume) }, mod.Manifest.Name);
    }

    private static InstanceValue BuildTrackInstance(TrackInfo track)
    {
        var ct   = new ClassType("Track");
        var inst = new InstanceValue(ct);
        inst.Fields["title"]    = new StringValue(track.Title);
        inst.Fields["artist"]   = new StringValue(track.Artist);
        inst.Fields["album"]    = new StringValue(track.Album);
        inst.Fields["genre"]    = new StringValue(track.Genre);
        inst.Fields["duration"] = new FloatValue(track.Duration);
        inst.Fields["path"]     = new StringValue(track.Path);
        return inst;
    }

    private void FireHook(LoadedMod mod, FunctionValue? hook, List<MiloValue> args, string modName)
    {
        if (hook is null) return;
        try
        {
            var callScope = new RuntimeScope(hook.Closure);

            if (mod.ModInstance is not null)
            {
                callScope.Define("this", mod.ModInstance);
                foreach (var (fieldName, fieldValue) in mod.ModInstance.Fields)
                    if (!fieldName.StartsWith("__method_"))
                        callScope.Define(fieldName, fieldValue);
            }

            for (int i = 0; i < hook.Params.Count && i < args.Count; i++)
                callScope.Define(hook.Params[i].Name, args[i]);

            var interpreter = new Interpreter(hook.Closure);
            try
            {
                foreach (var stmt in hook.Body.Statements)
                    interpreter.EvalHookStmt(stmt, callScope);
            }
            catch (ReturnSignal) { }
        }
        catch (RuntimeException ex)
        {
            Log?.Invoke($"[ModLoader] Runtime error in '{modName}' hook '{hook.Name}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[ModLoader] Unexpected error in '{modName}': {ex.Message}");
        }
    }
}