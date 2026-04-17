# CSSCord

A [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) port of [SourceCord](https://github.com/johnqherman/sourcecord). Two-way Discord ↔ CS2 chat integration for Source 2.

- CS2 chat messages appear in Discord via webhook
- Discord messages appear in CS2 chat with role colors and nicknames
- Player connect/disconnect and map change events posted to Discord
- Steam avatars as webhook profile pictures
- Emoji shortcode conversion in both directions (`:+1:` ↔ `👍`, guild custom emoji)

## Requirements

- CounterStrikeSharp
- A Discord server where you have Manage Server permissions
- A Discord bot in your server with **Read Message History** and **View Channel** permissions for the target channel

## Installation

1. Drop `CSSCord.dll` into `game/csgo/addons/counterstrikesharp/plugins/CSSCord/`
2. Start the server once. `credentials.json` will be created automatically
3. Fill in `credentials.json` (see below) and restart

## Configuration

### `credentials.json`

Located in the plugin directory alongside `CSSCord.dll`.

| Field         | Description                                           |
| ------------- | ----------------------------------------------------- |
| `BotToken`    | Discord bot token                                     |
| `ChannelId`   | ID of the channel to read messages from               |
| `GuildId`     | ID of your Discord server                             |
| `WebhookUrl`  | Webhook URL for the same channel                      |
| `SteamApiKey` | Steam Web API key (optional — enables player avatars) |

### `CSSCord.json`

Auto-created by CounterStrikeSharp in `configs/plugins/CSSCord/`.

| Field                    | Default  | Description                                                     |
| ------------------------ | -------- | --------------------------------------------------------------- |
| `PollingIntervalSeconds` | `1.0`    | How often to poll Discord for new messages (1–10s)              |
| `LogConnections`         | `1`      | `0` = off, `1` = name + SteamID, `2` = include IP               |
| `LogMapChanges`          | `false`  | Post map changes to Discord                                     |
| `UseRoleColors`          | `true`   | Color player names by their top Discord role color              |
| `UseNicknames`           | `true`   | Show Discord server nickname instead of username                |
| `ShowSteamId`            | `1`      | `0` = off, `1` = Steam3 `[U:1:...]`, `2` = Steam2 `STEAM_0:...` |
| `ShowDiscordPrefix`      | `true`   | Show `[Discord]` prefix on incoming messages                    |
| `DiscordColor`           | `5865F2` | Hex color for Discord name when no role color is set            |
| `AllowUserPings`         | `false`  | Allow `@username` in CS2 chat to ping Discord users             |
| `AllowRolePings`         | `false`  | Allow `@rolename` in CS2 chat to ping Discord roles             |

## Discord Setup

### Bot token and IDs

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications) and create an application
2. Under **Bot**, create a bot and copy the token → `BotToken`
3. Enable **Server Members Intent** under Privileged Gateway Intents (required for nicknames and role colors)
4. Invite the bot to your server with `bot` scope and **Read Message History** + **View Channel** permissions
5. In Discord, enable **Developer Mode** (User Settings → Advanced)
6. Right-click your server icon → **Copy Server ID** → `GuildId`
7. Right-click the target channel → **Copy Channel ID** → `ChannelId`

### Webhook

1. In your target channel, go to **Edit Channel → Integrations → Webhooks → New Webhook**
2. Copy the webhook URL → `WebhookUrl`

### Steam API key (optional)

Get a key at [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey) → `SteamApiKey`
