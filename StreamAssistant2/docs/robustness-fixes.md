# Robustness fixes

Known defects and loose ends in the tree as it stands. Mostly independent of each other — each can
be done, tested and committed on its own, except where an item says otherwise.

Items are indexed in [TODO.md](TODO.md) — summaries, severities and the suggested order of work
live there, along with the conventions these codes follow. This file holds only the detail.

---

## ALR — Log line when a connection degrades or recovers

The status bar shows IRC ping age and EventSub keepalive age live, but nothing records them: once
the window has moved on, the dated log has no trace that a connection went stale. Add a coloured log
line when either crosses into a worse band (the status bar's thresholds) and when it recovers,
debounced so a value hovering on a threshold doesn't spam, and with the startup false positive
suppressed (both ages start counting before the first ping or keepalive arrives).

If built, don't copy `DiskSpace.Notify`'s mechanics: it shares one `previousPrintTime` across all
severities and gates on `now.Second > 0`, silently depending on `Clock` firing exactly on minute
boundaries. Read, not reproduced.

## BUF — Buffered log file writes

`ConsoleLogger.LogToFileAsync` opens, appends and closes the log file for every line, on the
per-chat-message hot path. Holding one `StreamWriter` and flushing when the queue drains would be
cheaper. Deferred because it trades away durability of the log tail on a hard kill (stopping the
debugger kills the process without running shutdown). Read, not measured.
