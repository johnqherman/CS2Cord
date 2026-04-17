using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;
using CS2Cord.Config;

namespace CS2Cord.Services;

public class ChatService
{
    private readonly PluginConfig _config;

    private static readonly (string Hex, char Color)[] ColorTable =
    [
        ("ffffff", ChatColors.White),
        ("8b0000", ChatColors.DarkRed),
        ("b981f0", ChatColors.LightPurple),
        ("3eff3f", ChatColors.Green),
        ("bcfe94", ChatColors.Olive),
        ("a3fe47", ChatColors.Lime),
        ("ff3f3f", ChatColors.Red),
        ("c4c4c4", ChatColors.Grey),
        ("ebe378", ChatColors.Gold),
        ("b0c2d8", ChatColors.Silver),
        ("5d97d7", ChatColors.Blue),
        ("4c6aff", ChatColors.DarkBlue),
        ("d42de6", ChatColors.Magenta),
        ("eb4b4b", ChatColors.LightRed),
        ("e1af37", ChatColors.Orange),
    ];

    public ChatService(PluginConfig config) => _config = config;

    public void PrintDiscordMessage(string displayName, string content, string? roleColorHex)
    {
        Server.NextFrame(() =>
        {
            var colorReset  = ChatColors.Default.ToString();
            var nameColor   = HexToNearestChatColor(roleColorHex ?? _config.DiscordColor).ToString();
            var prefixColor = ChatColors.DarkBlue.ToString();

            var line = _config.ShowDiscordPrefix
                ? $" {prefixColor}[Discord]{colorReset} {nameColor}{displayName}{colorReset}: {content}"
                : $" {nameColor}{displayName}{colorReset}: {content}";

            Server.PrintToChatAll(line);
        });
    }

    private static char HexToNearestChatColor(string hex)
    {
        hex = hex.TrimStart('#').ToUpperInvariant();
        if (hex.Length != 6) return ChatColors.Default;

        try
        {
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);

            char   best    = ChatColors.Default;
            double minDist = double.MaxValue;

            foreach (var (tableHex, color) in ColorTable)
            {
                int    tr   = Convert.ToInt32(tableHex[..2], 16);
                int    tg   = Convert.ToInt32(tableHex[2..4], 16);
                int    tb   = Convert.ToInt32(tableHex[4..6], 16);
                double dist = Math.Sqrt(Math.Pow(r - tr, 2) + Math.Pow(g - tg, 2) + Math.Pow(b - tb, 2));
                if (dist < minDist) { minDist = dist; best = color; }
            }

            return best;
        }
        catch
        {
            return ChatColors.Default;
        }
    }
}
