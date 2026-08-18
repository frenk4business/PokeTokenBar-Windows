using System.IO;
using System.Text;
using System.Text.Json;

namespace PokeTokenBar.Providers.Codex;

internal sealed class CodexUsageFileParser
{
    public async Task<CodexUsageParseResult> ParseAsync(
        string path,
        string pathKey,
        CodexIndexedFile? previous,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(path);
        var rebuild = previous is null || previous.SafeOffset < 0 || previous.SafeOffset > fileInfo.Length;
        var state = rebuild ? new CodexIndexedFile { PathKey = pathKey } : Clone(previous!);
        var startOffset = rebuild ? 0 : state.SafeOffset;
        var valid = 0;
        var duplicate = 0;
        var malformed = 0;
        var incomplete = 0;
        long bytesRead = 0;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            useAsync: true);

        if (startOffset > 0)
        {
            stream.Seek(startOffset, SeekOrigin.Begin);
        }

        var buffer = new byte[64 * 1024];
        var pending = new List<byte>(8 * 1024);
        var currentLineStart = startOffset;
        var safeOffset = startOffset;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
            for (var i = 0; i < read; i++)
            {
                var b = buffer[i];
                pending.Add(b);
                var absoluteOffset = stream.Position - read + i + 1;
                if (b != (byte)'\n')
                {
                    continue;
                }

                var lineBytes = TrimLineEnding(pending);
                ParseCompleteLine(lineBytes, pathKey, state, ref valid, ref duplicate, ref malformed);
                pending.Clear();
                safeOffset = absoluteOffset;
                currentLineStart = absoluteOffset;
            }
        }

        if (pending.Count > 0)
        {
            incomplete++;
            safeOffset = currentLineStart;
        }

        state.Size = fileInfo.Length;
        state.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        state.SafeOffset = safeOffset;

        return new CodexUsageParseResult
        {
            File = state,
            ValidTokenEvents = valid,
            DuplicateStateEventsIgnored = duplicate,
            MalformedLinesIgnored = malformed,
            IncompleteLinesIgnored = incomplete,
            BytesRead = bytesRead
        };
    }

    private static void ParseCompleteLine(
        byte[] lineBytes,
        string pathKey,
        CodexIndexedFile state,
        ref int valid,
        ref int duplicate,
        ref int malformed)
    {
        if (lineBytes.Length == 0)
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(lineBytes);
        }
        catch (JsonException)
        {
            malformed++;
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (CodexJsonLineParser.TryReadSessionMeta(root, out var sessionId, out var parentSessionId, out var isSubagent))
            {
                state.SessionId ??= sessionId;
                state.ParentSessionId ??= parentSessionId;
                state.IsSubagent = state.IsSubagent || isSubagent;
                return;
            }

            if (!CodexJsonLineParser.TryReadTokenEvent(root, out var timestampUtc, out var delta, out var cumulative))
            {
                return;
            }

            var epoch = state.Epoch;
            if (cumulative is not null)
            {
                if (state.LastCumulativeUsage is not null && cumulative.Value.IsLowerThan(state.LastCumulativeUsage.Value))
                {
                    state.Epoch++;
                    epoch = state.Epoch;
                    state.SeenStateFingerprints.Clear();
                }

                state.LastCumulativeUsage = cumulative.Value;
            }

            var stateFingerprint = cumulative is null ? string.Empty : cumulative.Value.Fingerprint + "|" + delta.Fingerprint;
            if (!string.IsNullOrEmpty(stateFingerprint) && !state.SeenStateFingerprints.Add(epoch + "|" + stateFingerprint))
            {
                duplicate++;
                return;
            }

            var localId = string.IsNullOrEmpty(stateFingerprint)
                ? $"codex|{pathKey}|{state.Events.Count}"
                : $"codex|{pathKey}|{epoch}|{stateFingerprint}";

            state.Events.Add(new CodexIndexedTokenEvent
            {
                LocalEventId = localId,
                TimestampUtc = timestampUtc,
                Delta = delta,
                Cumulative = cumulative,
                Last = delta,
                Epoch = epoch,
                SessionId = state.SessionId,
                ParentSessionId = state.ParentSessionId,
                IsSubagent = state.IsSubagent
            });
            valid++;
        }
    }

    private static CodexIndexedFile Clone(CodexIndexedFile source) => new()
    {
        PathKey = source.PathKey,
        Size = source.Size,
        LastWriteTimeUtc = source.LastWriteTimeUtc,
        SafeOffset = source.SafeOffset,
        SessionId = source.SessionId,
        ParentSessionId = source.ParentSessionId,
        IsSubagent = source.IsSubagent,
        Epoch = source.Epoch,
        LastCumulativeUsage = source.LastCumulativeUsage,
        SeenStateFingerprints = new HashSet<string>(source.SeenStateFingerprints, StringComparer.Ordinal),
        Events = source.Events.ToList()
    };

    private static byte[] TrimLineEnding(List<byte> bytes)
    {
        var length = bytes.Count;
        if (length > 0 && bytes[length - 1] == (byte)'\n')
        {
            length--;
        }

        if (length > 0 && bytes[length - 1] == (byte)'\r')
        {
            length--;
        }

        if (length == bytes.Count)
        {
            return bytes.ToArray();
        }

        var result = new byte[length];
        bytes.CopyTo(0, result, 0, length);
        return result;
    }
}
