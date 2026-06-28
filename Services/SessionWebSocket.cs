using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Concentus;
using Concentus.Structs;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using MiloMusicPlayer.Models;

namespace MiloMusicPlayer.Services;

public class SessionWebSocket : IDisposable
{
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private readonly string _url;

    public event Action<SessionData> OnSessionUpdated;
    public event Action<ChatMessage> OnChatReceived;
    public event Action<string, string> OnUserJoined;
    public event Action<string> OnUserLeft;
    public event Action<string> OnHostResolved;

    private WasapiLoopbackCapture? _capture;
    private OpusEncoder? _encoder;
    private bool _isStreaming = false;

    private BufferedWaveProvider? _playbackBuffer;
    private WasapiOut? _waveOut;
    private OpusDecoder? _decoder;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public SessionWebSocket(string wsUrl)
    {
        _url = wsUrl;
    }

    public async Task ConnectAsync()
    {
        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        await _ws.ConnectAsync(new Uri(_url), _cts.Token);
        _ = ReceiveLoop();
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[65536];
        try
        {
            while (_ws.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(buffer, _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (_decoder == null) InitPlayback();

                    var encoded = new byte[result.Count];
                    Array.Copy(buffer, encoded, result.Count);

                    var pcm = new short[5760 * 2];
                    int samples = _decoder!.Decode(encoded, 0, encoded.Length, pcm, 0, 5760);
                    if (samples > 0)
                    {
                        var bytes = new byte[samples * 2 * 2];
                        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
                        _playbackBuffer?.AddSamples(bytes, 0, bytes.Length);
                    }
                    continue;
                }
                if (result.MessageType != WebSocketMessageType.Text) continue;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp)) continue;
                var type = typeProp.GetString();

                if (!root.TryGetProperty("data", out var data)) continue;

                switch (type)
                {
                    case "sessionState":
                        var session = JsonSerializer.Deserialize<SessionData>(data.GetRawText(), _opts);
                        if (session != null)
                        {
                            OnSessionUpdated?.Invoke(session);
                            if (!string.IsNullOrWhiteSpace(session.HostUserId))
                                OnHostResolved?.Invoke(session.HostUserId);
                        }
                        break;
                    case "playbackUpdate":
                        var update = JsonSerializer.Deserialize<SessionData>(data.GetRawText(), _opts);
                        if (update != null) OnSessionUpdated?.Invoke(update);
                        break;
                    case "chat":
                        var chat = JsonSerializer.Deserialize<ChatMessage>(data.GetRawText(), _opts);
                        if (chat != null) OnChatReceived?.Invoke(chat);
                        break;
                    case "userJoined":
                        var joined = JsonSerializer.Deserialize<JoinLeave>(data.GetRawText(), _opts);
                        if (joined != null) OnUserJoined?.Invoke(joined.UserId, joined.DisplayName);
                        break;
                    case "userLeft":
                        var left = JsonSerializer.Deserialize<JoinLeave>(data.GetRawText(), _opts);
                        if (left != null) OnUserLeft?.Invoke(left.UserId);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
        }
    }

    private void InitPlayback()
    {
        _decoder = new OpusDecoder(48000, 2);
        _playbackBuffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 2))
        {
            BufferDuration = TimeSpan.FromSeconds(5),
            DiscardOnBufferOverflow = true,
        };
        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _waveOut = new WasapiOut(device, AudioClientShareMode.Shared, true, 200);
        _waveOut.Init(_playbackBuffer);
        
        // Don't start playing immediately — wait for buffer to fill a bit
        Task.Run(async () =>
        {
            while (_playbackBuffer.BufferedDuration < TimeSpan.FromMilliseconds(400))
                await Task.Delay(10);
            _waveOut.Play();
        });
    }

    public void StartAudioStream()
    {
        if (_isStreaming) return;
        _isStreaming = true;

        _capture = new WasapiLoopbackCapture();
        _encoder = new OpusEncoder(48000, 2, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
        _encoder.Bitrate = 96000;

        _capture.DataAvailable += async (s, e) =>
        {
            if (!IsConnected || e.BytesRecorded == 0) return;

            var floats = new float[e.BytesRecorded / 4];
            Buffer.BlockCopy(e.Buffer, 0, floats, 0, e.BytesRecorded);

            var pcm = new short[floats.Length];
            for (int i = 0; i < floats.Length; i++)
                pcm[i] = (short)Math.Clamp(floats[i] * 32767f, short.MinValue, short.MaxValue);

            int frameSize = 960;
            int channels = 2;
            for (int i = 0; i + frameSize * channels <= pcm.Length; i += frameSize * channels)
            {
                var frame = new short[frameSize * channels];
                Array.Copy(pcm, i, frame, 0, frameSize * channels);

                var encoded = new byte[4000];
                int len = _encoder.Encode(frame, 0, frameSize, encoded, 0, encoded.Length);
                if (len <= 0) continue;

                var chunk = new byte[len];
                Array.Copy(encoded, chunk, len);

                try
                {
                    await _ws.SendAsync(chunk, WebSocketMessageType.Binary, true, _cts.Token);
                }
                catch { }
            }
        };

        _capture.StartRecording();
    }

    public void StopAudioStream()
    {
        _isStreaming = false;
        _capture?.StopRecording();
        _capture?.Dispose();
        _capture = null;
        _encoder = null;
    }

    public async Task SendAsync(string type, object data)
    {
        if (!IsConnected) return;
        var json = JsonSerializer.Serialize(new { type, data });
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
    }

    public async Task SendPlaybackUpdate(double position, bool isPlaying, string trackPath) =>
        await SendAsync("playbackUpdate", new { position, isPlaying, trackPath });

    public async Task SendChat(string message) =>
        await SendAsync("chat", new { message });

    public async Task CloseAsync()
    {
        StopAudioStream();
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    public void Dispose()
    {
        StopAudioStream();
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _cts?.Cancel();
        _ws?.Dispose();
    }

    private class JoinLeave
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}