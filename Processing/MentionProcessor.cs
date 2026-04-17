using System.Text.RegularExpressions;
using CSSCord.Config;
using CSSCord.Services;

namespace CSSCord.Processing;

public class MentionProcessor
{
    private readonly DiscordApiService _api;
    private readonly ChatService       _chat;
    private readonly PluginConfig      _config;

    private static readonly Regex UserMentionRegex    = new(@"<@!?(\d+)>", RegexOptions.Compiled);
    private static readonly Regex ChannelMentionRegex = new(@"<#(\d+)>", RegexOptions.Compiled);
    private static readonly Regex RoleMentionRegex    = new(@"<@&(\d+)>", RegexOptions.Compiled);

    public MentionProcessor(DiscordApiService api, ChatService chat, PluginConfig config)
    {
        _api = api;
        _chat = chat;
        _config = config;
    }

    public async Task ProcessAndPrintAsync(string userId, string displayName, string rawContent)
    {
        var content = rawContent;

        content = await ResolveUserMentionsAsync(content);
        content = await ResolveChannelMentionsAsync(content);
        content = await ResolveRoleMentionsAsync(content);
        content = EmojiProcessor.StripCustomEmoji(content);
        content = EmojiProcessor.UnicodeToShortcode(content);

        string? resolvedName = null;
        if (_config.UseNicknames)
            resolvedName = await _api.GetUserNicknameAsync(userId);
        resolvedName ??= await _api.GetUserDisplayNameAsync(userId) ?? displayName;

        string? roleColor = null;
        if (_config.UseRoleColors)
            roleColor = await _api.GetUserRoleColorAsync(userId);

        _chat.PrintDiscordMessage(resolvedName, content, roleColor);
    }

    private async Task<string> ResolveUserMentionsAsync(string text)
    {
        var ids = UserMentionRegex.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        foreach (var id in ids)
        {
            var name = await _api.GetUserDisplayNameAsync(id);
            if (name is not null)
                text = text.Replace($"<@{id}>", $"@{name}")
                           .Replace($"<@!{id}>", $"@{name}");
        }
        return text;
    }

    private async Task<string> ResolveChannelMentionsAsync(string text)
    {
        var ids = ChannelMentionRegex.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        foreach (var id in ids)
        {
            var name = await _api.GetChannelNameAsync(id);
            if (name is not null)
                text = text.Replace($"<#{id}>", $"#{name}");
        }
        return text;
    }

    private async Task<string> ResolveRoleMentionsAsync(string text)
    {
        var ids = RoleMentionRegex.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        foreach (var id in ids)
        {
            var name = await _api.GetRoleNameAsync(id);
            if (name is not null)
                text = text.Replace($"<@&{id}>", $"@{name}");
        }
        return text;
    }
}
