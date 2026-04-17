using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSSCord.Cache;
using CSSCord.Models;
using Microsoft.Extensions.Logging;

namespace CSSCord.Services;

public class DiscordApiService : IDisposable
{
    private const string ApiBase = "https://discord.com/api/v10";

    private readonly HttpClient _http;
    private readonly string _channelId;
    private readonly string _guildId;
    private readonly ILogger _logger;

    public TimedCache<string> UserColorCache { get; } = new(512);
    public TimedCache<string> UserDisplayNameCache { get; } = new(512);
    public TimedCache<string> UserNickCache { get; } = new(512);
    public TimedCache<string> ChannelNameCache { get; } = new(512);
    public TimedCache<string> RoleNameCache { get; } = new(512);

    public Dictionary<string, string> GuildMemberCache { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> GuildRoleCache { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GuildEmoji> GuildEmojiCache { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan ColorTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan DisplayNameTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan NickTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ChannelNameTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan RoleNameTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan BulkRefreshInterval = TimeSpan.FromHours(1);

    private DateTime _lastMemberFetch = DateTime.MinValue;
    private DateTime _lastRoleFetch   = DateTime.MinValue;
    private DateTime _lastEmojiFetch  = DateTime.MinValue;

    private List<DiscordRole> _roleList = new();

    public DiscordApiService(string botToken, string channelId, string guildId, string pluginVersion, ILogger logger)
    {
        _channelId = channelId;
        _guildId   = guildId;
        _logger    = logger;

        _http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"CSSCord/{pluginVersion}");
    }

    public async Task<List<DiscordMessage>> FetchMessagesAsync(string? afterId, int limit = 5)
    {
        var url = afterId is not null
            ? $"{ApiBase}/channels/{_channelId}/messages?limit={limit}&after={afterId}"
            : $"{ApiBase}/channels/{_channelId}/messages?limit={limit}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var messages = await response.Content.ReadFromJsonAsync<List<DiscordMessage>>();
        return messages ?? new List<DiscordMessage>();
    }

    public async Task<string?> GetUserDisplayNameAsync(string userId)
    {
        if (UserDisplayNameCache.TryGet(userId, out var name))
            return name;

        var member = await FetchMemberAsync(userId);
        if (member is null) return null;

        var resolved = ResolveDisplayName(member);
        UserDisplayNameCache.Set(userId, resolved, DisplayNameTtl);

        if (member.Nick is not null)
            UserNickCache.Set(userId, member.Nick, NickTtl);

        return resolved;
    }

    public async Task<string?> GetUserNicknameAsync(string userId)
    {
        if (UserNickCache.TryGet(userId, out var nick))
            return nick;

        var member = await FetchMemberAsync(userId);
        if (member is null) return null;

        var resolved = ResolveDisplayName(member);
        UserNickCache.Set(userId, resolved, NickTtl);
        return resolved;
    }

    public async Task<string?> GetUserRoleColorAsync(string userId)
    {
        if (UserColorCache.TryGet(userId, out var color))
            return color;

        await RefreshGuildRolesAsync();

        var member = await FetchMemberAsync(userId);
        if (member is null) return null;

        var highestColor = ResolveHighestRoleColor(member.Roles);
        if (highestColor is not null)
            UserColorCache.Set(userId, highestColor, ColorTtl);

        return highestColor;
    }

    public async Task<string?> GetChannelNameAsync(string channelId)
    {
        if (ChannelNameCache.TryGet(channelId, out var name))
            return name;

        try
        {
            var channel = await _http.GetFromJsonAsync<DiscordChannel>($"{ApiBase}/channels/{channelId}");
            if (channel?.Name is not null)
            {
                ChannelNameCache.Set(channelId, channel.Name, ChannelNameTtl);
                return channel.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch channel {ChannelId}", channelId);
        }
        return null;
    }

    public async Task<string?> GetRoleNameAsync(string roleId)
    {
        if (RoleNameCache.TryGet(roleId, out var name))
            return name;

        await RefreshGuildRolesAsync();

        if (RoleNameCache.TryGet(roleId, out name))
            return name;

        return null;
    }

    public async Task RefreshGuildMembersAsync()
    {
        if (DateTime.UtcNow - _lastMemberFetch < BulkRefreshInterval)
            return;
        _lastMemberFetch = DateTime.UtcNow;

        try
        {
            string? afterId = null;
            GuildMemberCache.Clear();
            while (true)
            {
                var url = afterId is not null
                    ? $"{ApiBase}/guilds/{_guildId}/members?limit=1000&after={afterId}"
                    : $"{ApiBase}/guilds/{_guildId}/members?limit=1000";

                var members = await _http.GetFromJsonAsync<List<DiscordMember>>(url);
                if (members is null || members.Count == 0) break;

                foreach (var m in members)
                {
                    if (m.User?.Id is null) continue;
                    var displayName = ResolveDisplayName(m);
                    GuildMemberCache[displayName] = m.User.Id;
                    if (m.User.Username is not null)
                        GuildMemberCache[m.User.Username] = m.User.Id;
                }

                if (members.Count < 1000) break;
                afterId = members[^1].User?.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh guild members");
            _lastMemberFetch = DateTime.MinValue;
        }
    }

    public async Task RefreshGuildRolesAsync()
    {
        if (DateTime.UtcNow - _lastRoleFetch < BulkRefreshInterval)
            return;
        _lastRoleFetch = DateTime.UtcNow;

        try
        {
            var roles = await _http.GetFromJsonAsync<List<DiscordRole>>($"{ApiBase}/guilds/{_guildId}/roles");
            if (roles is null) return;

            _roleList = roles;
            GuildRoleCache.Clear();
            foreach (var role in roles)
            {
                if (role.Name is not null && role.Id is not null)
                {
                    GuildRoleCache[role.Name] = role.Id;
                    RoleNameCache.Set(role.Id, role.Name, RoleNameTtl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh guild roles");
            _lastRoleFetch = DateTime.MinValue;
        }
    }

    public async Task RefreshGuildEmojisAsync()
    {
        if (DateTime.UtcNow - _lastEmojiFetch < BulkRefreshInterval)
            return;
        _lastEmojiFetch = DateTime.UtcNow;

        try
        {
            var emojis = await _http.GetFromJsonAsync<List<DiscordEmoji>>($"{ApiBase}/guilds/{_guildId}/emojis");
            if (emojis is null) return;

            GuildEmojiCache.Clear();
            foreach (var emoji in emojis)
            {
                if (emoji.Name is not null && emoji.Id is not null)
                    GuildEmojiCache[emoji.Name] = new GuildEmoji(emoji.Id, emoji.Animated);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh guild emojis");
            _lastEmojiFetch = DateTime.MinValue;
        }
    }

    private async Task<DiscordMember?> FetchMemberAsync(string userId)
    {
        try
        {
            return await _http.GetFromJsonAsync<DiscordMember>($"{ApiBase}/guilds/{_guildId}/members/{userId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch member {UserId}", userId);
            return null;
        }
    }

    private static string ResolveDisplayName(DiscordMember member)
    {
        if (!string.IsNullOrEmpty(member.Nick))
            return member.Nick;
        if (!string.IsNullOrEmpty(member.User?.GlobalName))
            return member.User.GlobalName;
        return member.User?.Username ?? "Unknown";
    }

    private string? ResolveHighestRoleColor(List<string>? roleIds)
    {
        if (roleIds is null || _roleList.Count == 0) return null;

        DiscordRole? best = null;
        foreach (var roleId in roleIds)
        {
            var role = _roleList.FirstOrDefault(r => r.Id == roleId);
            if (role is null || role.Color == 0) continue;
            if (best is null || role.Position > best.Position)
                best = role;
        }

        if (best is null || best.Color is null or 0) return null;
        return best.Color.Value.ToString("x6");
    }

    public void Dispose() => _http.Dispose();
}
