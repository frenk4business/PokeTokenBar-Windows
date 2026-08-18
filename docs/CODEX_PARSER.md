# Codex Parser

This document records the Phase 2 Windows Codex usage parser design.

## Upstream Reference

The implementation was based on the current upstream `chattymin/PokeTokenBar` Codex path in:

- `Sources/PokeTokenBar/Core/LocalUsageReader.swift`
- `Sources/PokeTokenBar/Core/LocalUsageProvider.swift`

Upstream reads `~/.codex/sessions/**/rollout-*.jsonl`. It does not currently expose a Codex-specific environment override in the inspected parser. The Windows implementation defaults to `%USERPROFILE%\.codex\sessions` and also supports `CODEX_HOME` as a practical Windows/test override.

## Token Event

A usable current Codex token event is:

```text
top-level type == "event_msg"
payload.type == "token_count"
payload.info.last_token_usage exists
timestamp parses as an instant
```

The parser also accepts a compatibility shape where the top-level record itself is `type == "token_count"`.

The headline usage delta comes from `payload.info.last_token_usage`, not from `total_token_usage`.

Tracked fields:

- `input_tokens`
- `cached_input_tokens`
- `cache_write_input_tokens`
- `output_tokens`
- `reasoning_output_tokens`
- `total_tokens`

`total_tokens` is the displayed headline metric.

## Event ID

For records with cumulative usage, local event identity is:

```text
codex|{relative rollout path}|{epoch}|{cumulative fingerprint}|{last fingerprint}
```

The canonical cross-rollout identity is:

```text
codex|{epoch}|{cumulative fingerprint}|{last fingerprint}
```

The relative rollout path is relative to the Codex session root and is used only as a sanitized key. Raw JSONL content and private metadata are not persisted.

## Duplicate Prevention

Within a file, duplicate cumulative+last usage states in the same epoch are ignored. This mirrors upstream's cumulative-state deduplication and prevents repeated `token_count` status events from being counted twice.

Across files, canonical duplicate states are collapsed and the earliest timestamp is kept, matching upstream's "keep earliest canonical state" philosophy for Codex.

## Cumulative Resets

`total_token_usage` is retained as cumulative state. If any cumulative counter decreases compared with the previous cumulative vector for the same file, the parser starts a new epoch and clears the file-local seen-state set.

This prevents a legitimate counter reset from being treated as a duplicate of an earlier state.

## Forks and Replayed History

The parser reads `session_meta` for:

- `id`
- `parent_id`
- `parentSessionId`
- `parent_session_id`
- subagent markers

For a child rollout with parent metadata, the resolver compares the child's usage-state prefix with resolved parent history. Matching prefix events are treated as replayed parent history and are not counted as child-owned events.

If a parent is unavailable or cumulative usage is unavailable, the fallback trims an initial tightly-clustered replay segment using upstream's one-second replay gap rule.

## Subagents

Upstream notes that observed Codex subagents include parent metadata but do not replay token_count events. The Windows parser follows that rule: subagent-marked rollouts do not use the one-second timing fallback. Their own token events remain countable. Canonical duplicate collapse still protects against identical states if Codex behavior changes.

## File Reading and Partial Lines

Files are opened read-only with:

```text
FileShare.ReadWrite | FileShare.Delete
```

The parser streams bytes and splits on newline. It saves only the byte offset after the last complete newline-terminated JSONL record. A trailing non-newline line is treated as incomplete and retried on the next refresh.

Malformed complete lines are skipped diagnostically. One bad line or unreadable file does not invalidate other files.

## Incremental Index

The index is stored as JSON in the local cache directory:

```text
%LOCALAPPDATA%\PokeTokenBar\Cache\codex-usage-index.json
```

It has a schema version and stores normalized parsed events, not raw source lines.

Per file it tracks:

- sanitized relative path key
- size
- last write time
- safe byte offset
- session ID and parent session ID
- subagent flag
- current epoch
- last cumulative usage vector
- seen state fingerprints
- normalized token events

Refresh behavior:

- unchanged complete file: skip raw parsing
- grown file: parse from previous safe offset
- incomplete tail: retry from the start of the incomplete line
- truncated/replaced file: rebuild that file from the beginning
- invalid/unsupported index: rebuild from source JSONL

The Codex source sessions are never modified.

## Time Aggregation

Token event timestamps are UTC instants. Period membership uses the current local timezone at refresh time.

Exposed periods:

- Today: local calendar day
- Last 5 hours: rolling five-hour window ending at refresh time
- Current week: local calendar week starting Monday
- Current month: local calendar month
- Observed lifetime: all resolved indexed events

The Monday week start matches the requested European-user convention. Upstream uses calendar start-of-week semantics through the current calendar; the Windows implementation makes the Monday choice explicit for deterministic tests.

## Known Differences

- `CODEX_HOME` is supported as a Windows/test override even though the inspected upstream parser uses the home-directory default directly.
- The upstream parser uses Swift value types and file-window scans; the Windows parser persists normalized parsed events in a versioned JSON index to support efficient unchanged refreshes.
- The parser intentionally does not implement official Codex limit display or `codex app-server`; that is outside Phase 2 local token analytics.
