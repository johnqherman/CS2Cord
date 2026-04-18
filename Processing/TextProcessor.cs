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
        1  => null,                              // NETWORK_DISCONNECT_SHUTDOWN
        2  => "Disconnect by user",              // NETWORK_DISCONNECT_DISCONNECT_BY_USER
        3  => null,                              // NETWORK_DISCONNECT_DISCONNECT_BY_SERVER
        53 => null,                              // NETWORK_DISCONNECT_RECONNECTION
        54 => null,                              // NETWORK_DISCONNECT_LOOPSHUTDOWN
        55 => "Disconnect by user",              // NETWORK_DISCONNECT_LOOPDEACTIVATE
        56 => null,                              // NETWORK_DISCONNECT_HOST_ENDGAME
        57 => null,                              // NETWORK_DISCONNECT_LOOP_LEVELLOAD_ACTIVATE
        59 => null,                              // NETWORK_DISCONNECT_EXITING
        69 => null,                              // NETWORK_DISCONNECT_SERVER_SHUTDOWN

        4  => "Connection lost",                 // NETWORK_DISCONNECT_LOST
        29 => "Timed out",                       // NETWORK_DISCONNECT_TIMEDOUT

        6  => "Steam banned",                    // NETWORK_DISCONNECT_STEAM_BANNED
        9  => "Steam logon failure",             // NETWORK_DISCONNECT_STEAM_LOGON
        13 => "VAC banned",                      // NETWORK_DISCONNECT_STEAM_VACBANSTATE
        14 => "Logged in elsewhere",             // NETWORK_DISCONNECT_STEAM_LOGGED_IN_ELSEWHERE

        39  => "Kicked",                         // NETWORK_DISCONNECT_KICKED
        40  => "Banned",                         // NETWORK_DISCONNECT_BANADDED
        41  => "Kicked (banned)",                // NETWORK_DISCONNECT_KICKBANADDED
        149 => "Banned",                         // NETWORK_DISCONNECT_REJECT_BANNED
        150 => "Kicked (team killing)",          // NETWORK_DISCONNECT_KICKED_TEAMKILLING
        151 => "Kicked (team killing at start)", // NETWORK_DISCONNECT_KICKED_TK_START
        152 => "Kicked (untrusted account)",     // NETWORK_DISCONNECT_KICKED_UNTRUSTEDACCOUNT
        153 => "Kicked (convicted account)",     // NETWORK_DISCONNECT_KICKED_CONVICTEDACCOUNT
        154 => "Kicked (competitive cooldown)",  // NETWORK_DISCONNECT_KICKED_COMPETITIVECOOLDOWN
        155 => "Kicked (team hurting)",          // NETWORK_DISCONNECT_KICKED_TEAMHURTING
        156 => "Kicked (hostage killing)",       // NETWORK_DISCONNECT_KICKED_HOSTAGEKILLING
        157 => "Vote kicked",                    // NETWORK_DISCONNECT_KICKED_VOTEDOFF
        158 => "Kicked (idle)",                  // NETWORK_DISCONNECT_KICKED_IDLE
        159 => "Kicked (suicide)",               // NETWORK_DISCONNECT_KICKED_SUICIDE
        160 => "Kicked (no Steam login)",        // NETWORK_DISCONNECT_KICKED_NOSTEAMLOGIN
        161 => "Kicked (no Steam ticket)",       // NETWORK_DISCONNECT_KICKED_NOSTEAMTICKET

        _  => null
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
