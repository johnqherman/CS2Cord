using System.Text.Json.Serialization;

namespace CS2Cord.Models;

public record DiscordMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("author")] DiscordUser Author,
    [property: JsonPropertyName("mentions")] List<DiscordUser>? Mentions);

public record DiscordUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("global_name")] string? GlobalName,
    [property: JsonPropertyName("bot")] bool? Bot);

public record DiscordMember(
    [property: JsonPropertyName("user")] DiscordUser? User,
    [property: JsonPropertyName("nick")] string? Nick,
    [property: JsonPropertyName("roles")] List<string>? Roles);

public record DiscordRole(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("color")] int? Color,
    [property: JsonPropertyName("position")] int Position);

public record DiscordChannel(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name);

public record DiscordEmoji(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("animated")] bool Animated);

public readonly record struct GuildEmoji(string Id, bool Animated);
