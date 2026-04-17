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
}
