using CounterStrikeSharp.API.Modules.Entities;
using System.Text;
using System.Text.RegularExpressions;

namespace CS2Cord.Processing;

public static class TextProcessor
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>""]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmojiShortcodeRegex = new(
        @":\w+(?:\\_\w+)+:",
        RegexOptions.Compiled);

    private static readonly HashSet<char> MarkdownChars = ['*', '_', '`', '~', '|', '>', '\\'];

    private const string ZeroWidthSpace = "\u200B";

    public static string EscapeUserContent(string text)
    {
        text = UrlRegex.Replace(text, m => $"<{m.Value}>");
        text = EscapeMarkdown(text);
        text = text.Replace("@everyone", $"@{ZeroWidthSpace}everyone");
        text = text.Replace("@here",     $"@{ZeroWidthSpace}here");
        text = EmojiShortcodeRegex.Replace(text, m => m.Value.Replace("\\_", "_"));
        return text;
    }

    public static string FormatConnectionMessage(
        string playerName,
        string? steamId,
        string? ipAddress,
        bool isDisconnect,
        string? disconnectReason,
        int logConnections,
        int showSteamId = 0)
    {
        var steamPart = steamId is not null
            ? (showSteamId == 1 ? $" {steamId}" : $" ({steamId})")
            : "";

        if (isDisconnect)
        {
            var reason = !string.IsNullOrEmpty(disconnectReason) ? $": {disconnectReason}" : "";
            return $"**{EscapeMarkdown(playerName)}**{steamPart} disconnected{reason}";
        }

        var ipPart = logConnections >= 2 && !string.IsNullOrEmpty(ipAddress)
            ? $" [{ipAddress}]"
            : "";
        return $"**{EscapeMarkdown(playerName)}**{steamPart} connected{ipPart}";
    }

    public static string FormatMapChangeMessage(string mapName) =>
        $"Map changed to **{EscapeMarkdown(mapName)}**";

    public static string? FormatSteamId(SteamID? steamId, int showSteamId) =>
        steamId is null ? null : showSteamId switch
        {
            1 => steamId.SteamId3,
            2 => steamId.SteamId2,
            _ => null
        };

    public static string? FormatDisconnectReason(int reason) => reason switch
    {
        1  => "Client quit",
        2  => "Timed out",
        3  => "Kicked",
        4  => "Kicked by vote",
        5  => "Banned",
        6  => "Connection lost",
        7  => "No Steam logon",
        8  => "VAC banned",
        9  => "Steam logon failure",
        10 => "Connection failed",
        _  => $"Reason {reason}"
    };

    public static bool IsValidHexColor(string color) =>
        color.Length == 6 && color.All(c => "0123456789abcdefABCDEF".Contains(c));

    private static string EscapeMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (char c in text)
        {
            if (MarkdownChars.Contains(c))
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
