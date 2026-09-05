using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Wallpapier.WinClient.DTOs;

namespace Wallpapier.WinClient.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly DatabaseService _db;

    public ApiClient(DatabaseService db)
    {
        _db = db;
        _httpClient = new HttpClient
        {
            // Augmentation du Timeout à 5 minutes pour éviter l'échec sur les réseaux lents
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    private string GetBaseUrl()
    {
        var rawIp = _db.GetSetting("ServerIP") ?? "100.64.0.1:8000";
        if (!rawIp.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !rawIp.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            rawIp = "http://" + rawIp;
        }
        return rawIp.TrimEnd('/');
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        var pin = _db.GetSetting("Pin") ?? "";
        request.Headers.Clear();
        request.Headers.Add("X-PIN-Code", pin);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ManifestResponseDto?> GetManifestAsync(string? since)
    {
        var url = $"{GetBaseUrl()}/api/manifest";
        if (!string.IsNullOrEmpty(since))
        {
            url += $"?since={Uri.EscapeDataString(since)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ManifestResponseDto>(json);
    }

    public async Task<bool> DownloadPhotoAsync(string photoId, string destinationPath)
    {
        var url = $"{GetBaseUrl()}/api/photos/{photoId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(request);

        // HttpCompletionOption.ResponseHeadersRead permet de ne pas charger tout le fichier en RAM
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var tempPath = destinationPath + ".tmp";
        await using (var stream = await response.Content.ReadAsStreamAsync())
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(fileStream);
        }

        if (File.Exists(destinationPath)) File.Delete(destinationPath);
        File.Move(tempPath, destinationPath);

        return true;
    }

    public async Task<bool> SendManifestAckAsync()
    {
        var url = $"{GetBaseUrl()}/api/manifest/ack";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyHeaders(request);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdatePhotoFavoriteAsync(string photoId, bool isFavorite)
    {
        var url = $"{GetBaseUrl()}/api/photos/{photoId}/favorite";
        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        ApplyHeaders(request);

        var payload = JsonSerializer.Serialize(new FavoriteUpdateDto { IsFavorite = isFavorite });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateServerResetTimeAsync(string resetTime)
    {
        var url = $"{GetBaseUrl()}/api/system/reset-time";
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        ApplyHeaders(request);

        var payload = JsonSerializer.Serialize(new ResetTimeUpdateDto { ResetTime = resetTime });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}