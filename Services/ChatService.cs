using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;
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
            var colorReset = ChatColors.Default.ToString();
            var nameColor = $"\x07{roleColorHex ?? _config.DiscordColor}";
            var prefixColor = ChatColors.Purple.ToString();

            var line = _config.ShowDiscordPrefix
                ? $" {prefixColor}[Discord]{colorReset} {nameColor}{displayName}{colorReset} :  {content}"
                : $" {nameColor}{displayName}{colorReset} :  {content}";

            Server.PrintToChatAll(line);
        });
    }
}
