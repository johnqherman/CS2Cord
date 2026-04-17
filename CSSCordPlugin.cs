using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Timers;
using CSSCord.Config;
using CSSCord.Processing;
using CSSCord.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CSSCord;

[MinimumApiVersion(80)]
public class CSSCordPlugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "CSSCord";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "johnqherman";
    public override string ModuleDescription => "Discord ↔ CS2 two-way chat integration";

    public PluginConfig Config { get; set; } = new();

    private CredentialsConfig      _credentials      = new();
    private DiscordApiService?     _discordApi;
    private DiscordPollingService? _polling;
    private WebhookService?        _webhook;
    private MentionProcessor?      _mentionProcessor;
    private SteamApiService?       _steamApi;
    private ChatService?           _chat;

    private readonly bool[]    _clientConnected  = new bool[65];
    private readonly string?[] _clientNames      = new string?[65];
    private readonly string?[] _clientSteamIds   = new string?[65];
    private          int       _humanPlayerCount;

    private CounterStrikeSharp.API.Modules.Timers.Timer? _pollTimer;

    public void OnConfigParsed(PluginConfig config)
    {
        config.PollingIntervalSeconds = Math.Clamp(config.PollingIntervalSeconds, 1.0f, 10.0f);
        if (!TextProcessor.IsValidHexColor(config.DiscordColor))
            config.DiscordColor = "5865F2";
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _credentials = LoadCredentials();

        _discordApi = new DiscordApiService(
            _credentials.BotToken, _credentials.ChannelId, _credentials.GuildId,
            ModuleVersion, Logger);

        _polling = new DiscordPollingService(Config.PollingIntervalSeconds, Logger);
        _webhook = new WebhookService(_credentials.WebhookUrl, ModuleVersion, _discordApi, Config, Logger);
        _steamApi = new SteamApiService(_credentials.SteamApiKey, ModuleVersion, Logger);
        _chat = new ChatService(Config);
        _mentionProcessor = new MentionProcessor(_discordApi, _chat, Config);

        RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Post);
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        if (_credentials.IsValid())
            StartPollingTimer();
        else
            Logger.LogWarning("[CSSCord] credentials.json is incomplete — Discord integration disabled.");
    }

    public override void Unload(bool hotReload)
    {
        _pollTimer?.Kill();
        _discordApi?.Dispose();
        _webhook?.Dispose();
        _steamApi?.Dispose();
    }

    private void StartPollingTimer()
    {
        _pollTimer?.Kill();
        _pollTimer = AddTimer(
            Config.PollingIntervalSeconds,
            () => _ = _polling!.PollAsync(_discordApi!, QueueIncomingMessage),
            TimerFlags.REPEAT);
    }

    private void QueueIncomingMessage(string userId, string authorName, string content) =>
        _ = _mentionProcessor!.ProcessAndPrintAsync(userId, authorName, content);

    private HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var name = player.PlayerName;
        var displayName = @event.Teamonly ? $"[TEAM] {name}" : name;

        if (Config.ShowSteamId > 0)
        {
            var steamId = FormatSteamId(player.AuthorizedSteamID);
            if (steamId is not null)
                displayName += $" ({steamId})";
        }

        var steamId64 = player.AuthorizedSteamID?.SteamId64 ?? 0;

        if (_webhook is null) return HookResult.Continue;

        _ = SendChatMessageWithAvatarAsync(displayName, @event.Text, steamId64);
        return HookResult.Continue;
    }

    private async Task SendChatMessageWithAvatarAsync(string displayName, string content, ulong steamId64)
    {
        string? avatarUrl = null;
        if (steamId64 != 0 && _steamApi is not null)
            avatarUrl = await _steamApi.GetAvatarUrlAsync(steamId64);

        await _webhook!.SendChatMessageAsync(displayName, content, avatarUrl);
    }

    private void OnClientPutInServer(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player is null || !player.IsValid || player.IsBot) return;
        if (_clientConnected[playerSlot]) return;

        _clientConnected[playerSlot] = true;
        _clientNames[playerSlot] = player.PlayerName;

        var steamId = FormatSteamId(player.AuthorizedSteamID);
        _clientSteamIds[playerSlot] = steamId;

        _humanPlayerCount++;

        if (Config.LogConnections > 0 && _webhook is not null)
        {
            var ip = Config.LogConnections >= 2 ? player.IpAddress : null;
            var msg = TextProcessor.FormatConnectionMessage(
                player.PlayerName, steamId, ip,
                isDisconnect: false, disconnectReason: null, Config.LogConnections);
            _ = _webhook.SendServerEventAsync(GetServerName(), msg);
        }
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var slot = player.Slot;
        if (!_clientConnected[slot]) return HookResult.Continue;

        var name = _clientNames[slot] ?? player.PlayerName;
        var steamId = _clientSteamIds[slot];

        _clientConnected[slot] = false;
        _clientNames[slot] = null;
        _clientSteamIds[slot] = null;

        if (_humanPlayerCount > 0) _humanPlayerCount--;

        if (Config.LogConnections > 0 && _webhook is not null)
        {
            var reason = @event.Reason > 0 ? FormatDisconnectReason(@event.Reason) : null;
            var msg = TextProcessor.FormatConnectionMessage(
                name, steamId, ipAddress: null,
                isDisconnect: true, disconnectReason: reason, Config.LogConnections);
            _ = _webhook.SendServerEventAsync(GetServerName(), msg);
        }

        return HookResult.Continue;
    }

    private void OnMapStart(string mapName)
    {
        if (!Config.LogMapChanges || _humanPlayerCount == 0 || _webhook is null) return;
        _ = _webhook.SendServerEventAsync(GetServerName(), TextProcessor.FormatMapChangeMessage(mapName));
    }

    private string? FormatSteamId(SteamID? steamId)
    {
        if (steamId is null) return null;
        return Config.ShowSteamId switch
        {
            1 => steamId.SteamId3,
            2 => steamId.SteamId2,
            _ => null
        };
    }

    private static string GetServerName() =>
        ConVar.Find("hostname")?.StringValue ?? "CS2 Server";

    private static string? FormatDisconnectReason(int reason) => reason switch
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

    private CredentialsConfig LoadCredentials()
    {
        var path = Path.Combine(ModuleDirectory, "credentials.json");
        if (!File.Exists(path))
        {
            var example = new CredentialsConfig();
            File.WriteAllText(path, JsonSerializer.Serialize(example,
                new JsonSerializerOptions { WriteIndented = true }));
            Logger.LogWarning("[CSSCord] Created credentials.json at {Path} — fill in your tokens.", path);
            return example;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CredentialsConfig>(json) ?? new CredentialsConfig();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[CSSCord] Failed to parse credentials.json");
            return new CredentialsConfig();
        }
    }
}
