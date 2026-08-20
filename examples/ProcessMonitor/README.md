# SharpVision Process Monitor

A live terminal process monitor built with SharpVision for Linux and macOS. It
polls system and process state in the background and renders live CPU and memory
charts, a process tree grouped by parent/child relationships, and a detail panel
with per-process history — all through retained, mutable controls that get their
properties updated in place rather than rebuilt.

![Process Monitor dashboard in a terminal](../../media/process-monitor.gif)

## Run it

Process Monitor is Linux- and macOS-only; it exits immediately on Windows, where
the `ps`, `/proc`, and `top`/`sysctl` sources it depends on do not exist.

```bash
dotnet run --project examples/ProcessMonitor/ProcessMonitor.csproj
```

## Controls

| Input  | Action                                                             |
| ------ | ------------------------------------------------------------------ |
| ↑ / ↓  | Move the process tree selection                                    |
| ← / →  | Collapse or expand the current item (built-in `TreeView` behavior) |
| Enter  | Toggle the current item's expansion, or confirm an armed kill      |
| K      | Arm termination of the selected process                            |
| Esc    | Cancel an armed termination                                        |
| R      | Force an immediate refresh instead of waiting for the next tick    |
| Ctrl+Q | Quit                                                               |

The host enables
[`TreatControlCAsInput`](../../docs/concepts/hosting.md#treatcontrolcasinput),
so Ctrl+Q is Process Monitor's own decoded exit path rather than the host's
default cooperative-shutdown signal.

## Sampling

A background loop refreshes every two seconds: it shells out to `ps` for the
full process table and reads platform-specific sources for system-wide figures —
`/proc/stat`, `/proc/meminfo`, `/proc/loadavg`, and `/proc/uptime` on Linux;
`top -l 1 -n 0 -stats cpu`, `sysctl -n hw.memsize`, and
`sysctl -n kern.boottime` on macOS. Sampling always runs off the dispatcher
thread; each completed sample is posted back and applied as one atomic screen
update.

The process tree rebuilds from scratch on every tick — parent/child edges from
the current `ps` snapshot rather than a persistent identity model — while still
preserving the caller's selection and each item's expanded/collapsed state
across the rebuild.

## Termination

Pressing `K` arms termination of the selected process only if it is owned by the
current OS user; a process owned by someone else is rejected outright with a
status message instead of being armed. An armed kill must be confirmed with
`Enter` within five seconds or it auto-cancels; `Esc` cancels it immediately.
Confirming sends `Process.Kill()` to the target PID and reports whether the
signal was sent, the process had already exited, or termination failed.

## Architecture

| File                    | Role                                                                            |
| ----------------------- | ------------------------------------------------------------------------------- |
| `Program.cs`            | Entry point; Windows platform gate; hosts the screen                            |
| `MonitorScreen.cs`      | Screen subclass — layout, refresh loop, tree, kill flow, input                  |
| `ProcessSample.cs`      | One process row parsed from `ps`                                                |
| `ProcessSampler.cs`     | Runs and parses `ps` into `ProcessSample` values                                |
| `ProcessNode.cs`        | One node of the parent/child process forest                                     |
| `ProcessTreeBuilder.cs` | Builds the forest from a flat `ProcessSample` list                              |
| `SystemSnapshot.cs`     | System-wide CPU, memory, load, and uptime figures                               |
| `SystemSampler.cs`      | Platform-specific system sampling (`/proc/*` on Linux, `top`/`sysctl` on macOS) |
| `MemoryCategory.cs`     | One named slice of the memory breakdown chart                                   |
| `CommandRunner.cs`      | Captures one external process's standard output asynchronously                  |
| `StatusSeverity.cs`     | Status-bar message severity                                                     |
| `SeverityPalette.cs`    | Resolves a `StatusSeverity` or CPU/memory percentage to a color                 |
| `GlobalUsings.cs`       | Shared framework imports                                                        |

Process Monitor uses no custom drawing. Every visual element is a standard
SharpVision control: `TreeView`, `LineChart`, `VerticalBarChart`, `Sparkline`,
`GroupBox`, `StatusBar`, `Text`, `Grid`, `Dock`, and `Stack`.
