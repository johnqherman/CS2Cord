using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;
using CS2Cord.Config;

namespace CS2Cord.Services;

public class ChatService
{
    private readonly PluginConfig _config;

    private static readonly (int R, int G, int B, char Color)[] ColorTable =
    [
        (0xFF, 0xFF, 0xFF, ChatColors.White),
        (0x8B, 0x00, 0x00, ChatColors.DarkRed),
        (0xB9, 0x81, 0xF0, ChatColors.LightPurple),
        (0x3E, 0xFF, 0x3F, ChatColors.Green),
        (0xBC, 0xFE, 0x94, ChatColors.Olive),
        (0xA3, 0xFE, 0x47, ChatColors.Lime),
        (0xFF, 0x3F, 0x3F, ChatColors.Red),
        (0xC4, 0xC4, 0xC4, ChatColors.Grey),
        (0xEB, 0xE3, 0x78, ChatColors.Gold),
        (0xB0, 0xC2, 0xD8, ChatColors.Silver),
        (0x5D, 0x97, 0xD7, ChatColors.Blue),
        (0x4C, 0x6A, 0xFF, ChatColors.DarkBlue),
        (0xD4, 0x2D, 0xE6, ChatColors.Magenta),
        (0xEB, 0x4B, 0x4B, ChatColors.LightRed),
        (0xE1, 0xAF, 0x37, ChatColors.Orange),
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

            foreach (var (tr, tg, tb, color) in ColorTable)
            {
                double dist = Math.Sqrt(Math.Pow(r - tr, 2) + Math.Pow(g - tg, 2) + Math.Pow(b - tb, 2));
                if (dist < minDist)
                {
                    minDist = dist;
                    best    = color;
                }
            }

            return best;
        }
        catch
        {
            return ChatColors.Default;
        }
    }
}
