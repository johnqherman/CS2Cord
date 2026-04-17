using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace CS2Cord.Config;

public class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("PollingIntervalSeconds")]
    public float  PollingIntervalSeconds { get; set; } = 1.0f;

    [JsonPropertyName("LogConnections")]
    public int    LogConnections         { get; set; } = 1;

    [JsonPropertyName("LogMapChanges")]
    public bool   LogMapChanges          { get; set; } = false;

    [JsonPropertyName("UseRoleColors")]
    public bool   UseRoleColors          { get; set; } = true;

    [JsonPropertyName("UseNicknames")]
    public bool   UseNicknames           { get; set; } = true;

    [JsonPropertyName("ShowSteamId")]
    public int    ShowSteamId            { get; set; } = 1;

    [JsonPropertyName("ShowDiscordPrefix")]
    public bool   ShowDiscordPrefix      { get; set; } = true;

    [JsonPropertyName("DiscordColor")]
    public string DiscordColor           { get; set; } = "5865F2";

    [JsonPropertyName("AllowUserPings")]
    public bool   AllowUserPings         { get; set; } = false;

    [JsonPropertyName("AllowRolePings")]
    public bool   AllowRolePings         { get; set; } = false;
}
