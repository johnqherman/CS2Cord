using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CS2Cord.Config;
using CS2Cord.Processing;
using CS2Cord.Services;
using Microsoft.Extensions.Logging;

namespace CS2Cord;

[MinimumApiVersion(80)]
public class CS2CordPlugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "CS2Cord";
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
    private PlayerTracker          _players          = new();

    private Timer? _pollTimer;

    public void OnConfigParsed(PluginConfig config)
    {
        config.PollingIntervalSeconds = Math.Clamp(config.PollingIntervalSeconds, 1.0f, 10.0f);
        if (!TextProcessor.IsValidHexColor(config.DiscordColor))
            config.DiscordColor = "5865F2";
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _credentials = CredentialsConfig.Load(Path.Combine(ModuleDirectory, "credentials.json"), Logger);

        _discordApi = new DiscordApiService(
            _credentials.BotToken, _credentials.ChannelId, _credentials.GuildId,
            ModuleVersion, Logger);

        _polling  = new DiscordPollingService(Config.PollingIntervalSeconds, Logger);
        _webhook  = new WebhookService(_credentials.WebhookUrl, ModuleVersion, _discordApi, Config, Logger);
        _steamApi = new SteamApiService(_credentials.SteamApiKey, ModuleVersion, Logger);
        _chat     = new ChatService(Config);
        _mentionProcessor = new MentionProcessor(_discordApi, _chat, Config);
        _players  = new PlayerTracker();

        RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Post);
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        if (_credentials.IsValid())
            StartPollingTimer();
        else
            Logger.LogWarning("[CS2Cord] credentials.json is incomplete — Discord integration disabled.");
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
        var player = Utilities.GetPlayerFromUserid(@event.Userid);
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var name = player.PlayerName;
        var displayName = @event.Teamonly ? $"[TEAM] {name}" : name;

        if (Config.ShowSteamId > 0)
        {
            var steamId = TextProcessor.FormatSteamId(player.AuthorizedSteamID, Config.ShowSteamId);
            if (steamId is not null)
                displayName += Config.ShowSteamId == 1 ? $" {steamId}" : $" ({steamId})";
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
        if (_players.IsConnected(playerSlot)) return;

        var steamId = TextProcessor.FormatSteamId(player.AuthorizedSteamID, Config.ShowSteamId);
        _players.Add(playerSlot, player.PlayerName, steamId);

        if (Config.LogConnections > 0 && _webhook is not null)
        {
            var ip  = Config.LogConnections >= 2 ? player.IpAddress : null;
            var msg = TextProcessor.FormatConnectionMessage(
                player.PlayerName, steamId, ip,
                isDisconnect: false, disconnectReason: null, Config.LogConnections, Config.ShowSteamId);
            _ = _webhook.SendServerEventAsync(GetServerName(), msg);
        }
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var slot = player.Slot;
        if (!_players.IsConnected(slot)) return HookResult.Continue;

        var (name, steamId) = _players.Remove(slot, player.PlayerName);

        if (Config.LogConnections > 0 && _webhook is not null)
        {
            var reason = @event.Reason > 0 ? TextProcessor.FormatDisconnectReason(@event.Reason) : null;
            var msg = TextProcessor.FormatConnectionMessage(
                name, steamId, ipAddress: null,
                isDisconnect: true, disconnectReason: reason, Config.LogConnections, Config.ShowSteamId);
            _ = _webhook.SendServerEventAsync(GetServerName(), msg);
        }

        return HookResult.Continue;
    }

    private void OnMapStart(string mapName)
    {
        if (!Config.LogMapChanges || _players.HumanPlayerCount == 0 || _webhook is null) return;
        _ = _webhook.SendServerEventAsync(GetServerName(), TextProcessor.FormatMapChangeMessage(mapName));
    }

    private static string GetServerName() =>
        ConVar.Find("hostname")?.StringValue ?? "CS2 Server";
}
