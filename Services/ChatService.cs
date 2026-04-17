using CounterStrikeSharp.API;
using CSSCord.Config;

namespace CSSCord.Services;

public class ChatService
{
    private readonly PluginConfig _config;

    public ChatService(PluginConfig config) => _config = config;

    public void PrintDiscordMessage(string displayName, string content, string? roleColorHex)
    {
        Server.NextFrame(() =>
        {
            const string colorReset = "\x01";
            var nameColor = $"\x07{(roleColorHex ?? _config.DiscordColor).ToLowerInvariant()}";
            const string prefixColor = "\x075865f2";

            var line = _config.ShowDiscordPrefix
                ? $" {prefixColor}[Discord]{colorReset} {nameColor}{displayName}{colorReset} :  {content}"
                : $" {nameColor}{displayName}{colorReset} :  {content}";

            Server.PrintToChatAll(line);
        });
    }
}
