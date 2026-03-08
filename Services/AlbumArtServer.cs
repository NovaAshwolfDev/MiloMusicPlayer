// Services/AlbumArtServer.cs
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace MiloMusicPlayer.Services;

public class AlbumArtServer : IDisposable
{
    private readonly HttpClient _http = new();
    private string _currentUrl = "cover"; // fallback to portal key

    public string Url => _currentUrl;

    public async Task SetArt(byte[]? art)
    {
        if (art == null)
        {
            _currentUrl = "cover";
            return;
        }

        var copy = (byte[])art.Clone(); // ensure we own the buffer
        var uploaded = await UploadAsync(copy);
        _currentUrl = uploaded ?? "cover";
    }

    private async Task<string?> UploadAsync(byte[] imageBytes)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://catbox.moe/user/api.php");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes, 0, imageBytes.Length);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "fileToUpload", "cover.jpg");
            content.Add(new StringContent("fileupload"), "reqtype");

            request.Content = content;

            using var response = await _http.SendAsync(request);
            var url = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Catbox response: " + url);

            return url.StartsWith("https://") ? url.Trim() : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Catbox upload failed: " + ex.Message);
            Console.WriteLine("Inner: " + ex.InnerException?.Message);
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}