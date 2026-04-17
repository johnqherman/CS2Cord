using System.Text;
using System.Text.RegularExpressions;
using CSSCord.Data;
using CSSCord.Models;

namespace CSSCord.Processing;

public static class EmojiProcessor
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> _unicodeToShortcode = new(() =>
    {
        var dict = new Dictionary<string, string>();
        foreach (var (key, value) in EmojiData.ShortcodeToUnicode)
            dict.TryAdd(value, key);
        return dict;
    });

    private static readonly Regex ShortcodePattern   = new(
        @":([a-zA-Z0-9_\-+]+):",
        RegexOptions.Compiled);

    private static readonly Regex CustomEmojiPattern = new(
        @"<(a?):([^:]+):(\d+)>",
        RegexOptions.Compiled);

    public static string ShortcodesToUnicode(string text) =>
        ShortcodePattern.Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            return EmojiData.ShortcodeToUnicode.TryGetValue(name, out var unicode) ? unicode : m.Value;
        });

    public static string ShortcodesToGuildEmoji(
        string text,
        IReadOnlyDictionary<string, GuildEmoji> guildEmojiCache) =>
        ShortcodePattern.Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            var lookupName = UnescapeEmojiName(name).ToLowerInvariant();
            if (guildEmojiCache.TryGetValue(lookupName, out var emoji))
                return emoji.Animated ? $"<a:{name}:{emoji.Id}>" : $"<:{name}:{emoji.Id}>";
            return m.Value;
        });

    public static string StripCustomEmoji(string text) =>
        CustomEmojiPattern.Replace(text, m => $":{UnescapeEmojiName(m.Groups[2].Value)}:");

    public static string UnicodeToShortcode(string text)
    {
        var reverse = _unicodeToShortcode.Value;
        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            bool matched = false;
            for (int len = Math.Min(8, text.Length - i); len >= 1; len--)
            {
                var candidate = text.Substring(i, len);
                if (reverse.TryGetValue(candidate, out var shortcode))
                {
                    sb.Append(':').Append(shortcode).Append(':');
                    i += len;
                    matched = true;
                    break;
                }
            }
            if (!matched)
                sb.Append(text[i++]);
        }
        return sb.ToString();
    }

    private static string UnescapeEmojiName(string name) => name.Replace("\\_", "_");
}
