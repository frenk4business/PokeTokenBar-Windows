using System.Text.Json;

namespace PokeTokenBar.Providers.Codex;

internal static class CodexJsonLineParser
{
    public static bool TryReadSessionMeta(JsonElement root, out string? sessionId, out string? parentSessionId, out bool isSubagent)
    {
        sessionId = null;
        parentSessionId = null;
        isSubagent = false;

        if (!IsType(root, "session_meta") || !TryGetObject(root, "payload", out var payload))
        {
            return false;
        }

        sessionId = ReadString(payload, "id");
        parentSessionId =
            ReadString(payload, "parent_id")
            ?? ReadString(payload, "parentSessionId")
            ?? ReadString(payload, "parent_session_id");

        isSubagent =
            ReadBool(payload, "is_subagent")
            || ReadString(payload, "originator")?.Contains("subagent", StringComparison.OrdinalIgnoreCase) == true
            || ReadString(payload, "source")?.Contains("subagent", StringComparison.OrdinalIgnoreCase) == true;

        return sessionId is not null || parentSessionId is not null || isSubagent;
    }

    public static bool TryReadTokenEvent(JsonElement root, out DateTimeOffset timestampUtc, out CodexTokenUsage delta, out CodexTokenUsage? cumulative)
    {
        timestampUtc = default;
        delta = CodexTokenUsage.Zero;
        cumulative = null;

        JsonElement tokenPayload;
        if (IsType(root, "event_msg") && TryGetObject(root, "payload", out var payload) && IsType(payload, "token_count"))
        {
            tokenPayload = payload;
        }
        else if (IsType(root, "token_count"))
        {
            tokenPayload = TryGetObject(root, "payload", out var topPayload) ? topPayload : root;
        }
        else
        {
            return false;
        }

        if (!TryGetObject(tokenPayload, "info", out var info))
        {
            return false;
        }

        if (!TryGetObject(info, "last_token_usage", out var lastUsage))
        {
            return false;
        }

        var timestamp = ReadString(root, "timestamp");
        if (!DateTimeOffset.TryParse(timestamp, out timestampUtc))
        {
            return false;
        }

        timestampUtc = timestampUtc.ToUniversalTime();
        delta = ReadUsage(lastUsage);

        if (TryGetObject(info, "total_token_usage", out var totalUsage))
        {
            cumulative = ReadUsage(totalUsage);
        }

        return true;
    }

    public static CodexTokenUsage ReadUsage(JsonElement usage)
    {
        var input = ReadLong(usage, "input_tokens");
        var cached = ReadLong(usage, "cached_input_tokens");
        var cacheWrite = ReadLong(usage, "cache_write_input_tokens");
        var output = ReadLong(usage, "output_tokens");
        var reasoning = ReadLong(usage, "reasoning_output_tokens");
        var total = ReadLong(usage, "total_tokens");

        return new CodexTokenUsage(input, cached, cacheWrite, output, reasoning, total);
    }

    private static bool IsType(JsonElement element, string type)
        => ReadString(element, "type") == type;

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out value)
               && value.ValueKind == JsonValueKind.Object;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.True || (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var value) && value);
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
        {
            return Math.Max(0, value);
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out value))
        {
            return Math.Max(0, value);
        }

        return 0;
    }
}
