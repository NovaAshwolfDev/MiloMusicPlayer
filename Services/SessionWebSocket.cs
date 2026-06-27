using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
                    case "playbackUpdate":
                        var session = JsonSerializer.Deserialize<SessionData>(data.GetRawText(), _opts);
                        if (session != null) OnSessionUpdated?.Invoke(session);
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
            Console.WriteLine($"[SessionWebSocket] ReceiveLoop error: {ex.Message}");
        }
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
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _ws?.Dispose();
    }

    private class JoinLeave
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}