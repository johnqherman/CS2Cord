using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CSSCord.Cache;
using Microsoft.Extensions.Logging;

namespace CSSCord.Services;

public class SteamApiService : IDisposable
{
    private readonly HttpClient         _http;
    private readonly string             _apiKey;
    private readonly TimedCache<string> _avatarCache = new(512);
    private static readonly TimeSpan    AvatarTtl    = TimeSpan.FromMinutes(30);
    private readonly ILogger            _logger;

    public SteamApiService(string apiKey, string pluginVersion, ILogger logger)
    {
        _apiKey = apiKey;
        _logger = logger;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"CSSCord/{pluginVersion}");
    }

    public async Task<string?> GetAvatarUrlAsync(ulong steamId64)
    {
        var key = steamId64.ToString();
        if (_avatarCache.TryGet(key, out var cached))
            return cached;

        if (string.IsNullOrEmpty(_apiKey))
            return null;

        try
        {
            var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_apiKey}&steamids={steamId64}";
            var response = await _http.GetFromJsonAsync<SteamApiResponse>(url);
            var avatarUrl = response?.Response?.Players?.FirstOrDefault()?.AvatarFull;
            if (!string.IsNullOrEmpty(avatarUrl))
                _avatarCache.Set(key, avatarUrl, AvatarTtl);
            return avatarUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Steam avatar for {SteamId}", steamId64);
            return null;
        }
    }

    public void Dispose() => _http.Dispose();

    private record SteamApiResponse(
        [property: JsonPropertyName("response")] SteamResponse? Response);

    private record SteamResponse(
        [property: JsonPropertyName("players")] List<SteamPlayer>? Players);

    private record SteamPlayer(
        [property: JsonPropertyName("avatarfull")] string? AvatarFull);
}
