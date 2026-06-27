using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MiloMusicPlayer.Services;

public class AuthService
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.miloashwolf.gay") };

    public string UserId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";

    public async Task<string> RegisterAsync(string email, string password, string displayName)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/register", new { email, password, displayName });
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        UserId = result.UserId ?? email;
        DisplayName = result.DisplayName ?? displayName;
        return result.Token;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        UserId = result.UserId ?? email;
        DisplayName = result.DisplayName ?? email;
        return result.Token;
    }
    
    public async Task<(string UserId, string DisplayName)> GetMeAsync(string token)
    {
        _http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var resp = await _http.GetFromJsonAsync<MeResponse>("/api/auth/me");
        return (resp.Id, resp.DisplayName);
    }

    private class MeResponse
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
    private class AuthResponse
    {
        public string Token { get; set; } = "";
        public string? UserId { get; set; }
        public string? DisplayName { get; set; }
    }
}