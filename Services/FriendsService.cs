using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MiloMusicPlayer.Models;

namespace MiloMusicPlayer.Services;

public class FriendsService
{
    private readonly HttpClient _http;

    public FriendsService(string authToken)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.miloashwolf.gay")
        };
        _http.DefaultRequestHeaders.Authorization = new("Bearer", authToken);
    }

    public async Task<List<Friend>> GetFriendsAsync() =>
        await _http.GetFromJsonAsync<List<Friend>>("/api/friends");

    public async Task<List<ActivityEntry>> GetActivityFeedAsync() =>
        await _http.GetFromJsonAsync<List<ActivityEntry>>("/api/activity");

    public async Task PostActivityAsync(string type, string trackTitle, string trackArtist)
    {
        await _http.PostAsJsonAsync("/api/activity", new { type, trackTitle, trackArtist });
    }

    public async Task<string> CreateSessionAsync()
    {
        var resp = await _http.PostAsync("/api/sessions", null);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<SessionCreateResponse>();
        return result.SessionId;
    }

    public async Task<SessionData> GetSessionInfoAsync(string sessionId) =>
        await _http.GetFromJsonAsync<SessionData>($"/api/sessions/{sessionId}/info");

    public async Task SendHeartbeatAsync(string? sessionId = null)
    {
        try { await _http.PostAsJsonAsync("/api/heartbeat", new { sessionId }); } catch { }
    }

    public async Task<List<UserSearchResult>> SearchUsersAsync(string query) =>
        await _http.GetFromJsonAsync<List<UserSearchResult>>($"/api/users/search?q={Uri.EscapeDataString(query)}");

    public async Task AddFriendAsync(string friendUserId) =>
        await _http.PostAsJsonAsync("/api/friends", new { friendUserId });

    private class SessionCreateResponse { public string SessionId { get; set; } }
}