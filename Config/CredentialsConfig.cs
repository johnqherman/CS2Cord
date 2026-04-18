using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2Cord.Config;

public class CredentialsConfig
{
    [JsonPropertyName("BotToken")]
    public string BotToken     { get; set; } = "";

    [JsonPropertyName("ChannelId")]
    public string ChannelId    { get; set; } = "";

    [JsonPropertyName("GuildId")]
    public string GuildId      { get; set; } = "";

    [JsonPropertyName("WebhookUrl")]
    public string WebhookUrl   { get; set; } = "";

    [JsonPropertyName("SteamApiKey")]
    public string SteamApiKey  { get; set; } = "";

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(BotToken) &&
        !string.IsNullOrWhiteSpace(ChannelId) &&
        !string.IsNullOrWhiteSpace(GuildId) &&
        !string.IsNullOrWhiteSpace(WebhookUrl);

    public static CredentialsConfig Load(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            var example = new CredentialsConfig();
            File.WriteAllText(path, JsonSerializer.Serialize(example,
                new JsonSerializerOptions { WriteIndented = true }));
            logger.LogWarning("[CS2Cord] Created credentials.json at {Path} — fill in your tokens.", path);
            return example;
        }

        try
        {
            return JsonSerializer.Deserialize<CredentialsConfig>(File.ReadAllText(path)) ?? new CredentialsConfig();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CS2Cord] Failed to parse credentials.json");
            return new CredentialsConfig();
        }
    }
}
