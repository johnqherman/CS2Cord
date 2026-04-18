using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CS2Cord.Config;
using CS2Cord.Processing;
using Microsoft.Extensions.Logging;

namespace CS2Cord.Services;

public class WebhookService : IDisposable
{
    private static readonly Regex PingPattern = new(@"(?<!<)@([^\s@#<>]+)", RegexOptions.Compiled);

    private readonly HttpClient        _http;
    private readonly string            _webhookUrl;
    private readonly DiscordApiService _api;
    private readonly PluginConfig _config;
    private readonly ILogger _logger;

    public WebhookService(
        string webhookUrl,
        string pluginVersion,
        DiscordApiService api,
        PluginConfig config,
        ILogger logger)
    {
        _webhookUrl = webhookUrl;
        _api        = api;
        _config     = config;
        _logger     = logger;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"CS2Cord/{pluginVersion}");
    }

    public async Task SendChatMessageAsync(string username, string content, string? avatarUrl = null)
    {
        await _api.RefreshGuildEmojisAsync();
        if (_config.AllowUserPings) await _api.RefreshGuildMembersAsync();
        if (_config.AllowRolePings) await _api.RefreshGuildRolesAsync();

        var processed = TextProcessor.EscapeUserContent(content);
        processed = EmojiProcessor.ShortcodesToGuildEmoji(processed, _api.GuildEmojis);
        processed = EmojiProcessor.ShortcodesToUnicode(processed);

        if (_config.AllowUserPings)  processed = ConvertUserPings(processed);
        if (_config.AllowRolePings)  processed = ConvertRolePings(processed);

        await PostWebhookAsync(username, processed, avatarUrl);
    }

    public async Task SendServerEventAsync(string serverName, string content) =>
        await PostWebhookAsync(serverName, content, avatarUrl: null);

    private async Task PostWebhookAsync(string username, string content, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl)) return;

        try
        {
            var payload  = new WebhookPayload(username, content, avatarUrl);
            var json     = JsonSerializer.Serialize(payload);
            var response = await _http.PostAsync(_webhookUrl,
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 204)
                _logger.LogWarning("Webhook POST returned {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send webhook message");
        }
    }

    private string ConvertUserPings(string text) =>
        PingPattern.Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            return _api.GuildMembers.TryGetValue(name, out var userId)
                ? $"<@{userId}>"
                : m.Value;
        });

    private string ConvertRolePings(string text) =>
        PingPattern.Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            return _api.GuildRoles.TryGetValue(name, out var roleId)
                ? $"<@&{roleId}>"
                : m.Value;
        });

    public void Dispose() => _http.Dispose();

    private record WebhookPayload(
        [property: JsonPropertyName("username")]   string  Username,
        [property: JsonPropertyName("content")]    string  Content,
        [property: JsonPropertyName("avatar_url")] string? AvatarUrl);
}
