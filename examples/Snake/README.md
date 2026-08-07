# SharpVision Snake

Snake is a playable terminal arcade example for SharpVision. It exists to show
what the library's retained, mutable control model can do when a screen is
genuinely busy: a self-playing attract mode, FIGlet headings colored by `Prism`,
a custom cell-canvas game board with layered transient effects, a draining
`ProgressBar` boost meter, routed keyboard input, and independent simulation and
presentation clocks — all without a single control being rebuilt at runtime.

## Run it

Install the .NET 10 SDK, then use a modern Unicode terminal. A true-color
terminal at least 100 columns by 30 rows gives the intended layout and palette;
256-color terminals (including headless tmux) degrade safely to the nearest
palette entries.

```bash
dotnet run --project examples/Snake/Snake.csproj
```

## Controls

| Input         | Action                                                                  |
| ------------- | ----------------------------------------------------------------------- |
| Arrows / WASD | Move while playing                                                      |
| P             | Pause or resume                                                         |
| 1 / 2 / 3     | Select Easy, Medium, or Hard on the title screen                        |
| Enter         | Start, continue after game over, or commit exactly three score initials |
| Backspace     | Remove the last score initial                                           |
| Q             | Quit from the title screen only                                         |
| Ctrl+Q        | Quit globally from every game phase                                     |

The host enables
[`TreatControlCAsInput`](../../docs/concepts/hosting.md#treatcontrolcasinput),
so Ctrl+C and other control keys reach SharpVision's decoder instead of becoming
the host's default cooperative-shutdown signal. Because that opt-in makes the
application responsible for its own decoded exit path, Snake reserves Ctrl+Q in
every phase. Plain Q remains a title-screen shortcut only.

A direction key also advances the simulation immediately instead of waiting for
the next timer tick, so steering always feels instant regardless of difficulty.

## Apples

The board pulses each apple between bright and dim variants while preserving a
small, readable gameplay palette. Apple glyphs retain the field background, so
their cells remain visually continuous with the surrounding soil.

| Glyph | Kind   | Effect                                                                       |
| ----- | ------ | ---------------------------------------------------------------------------- |
| `●`   | Normal | Adds 10 points and grows by one segment                                      |
| `◆`   | Golden | Adds 50 points and grows by three segments; twinkles through `◆◈◇◈`          |
| `✦`   | Poison | Removes 5 points, down to zero, and shrinks twice; costs a life if too short |
| `★`   | Speed  | Adds 20 points and resets the speed-boost tick budget                        |
| `♥`   | Life   | Adds 15 points and restores one life, up to five                             |

Every eaten apple spawns two transient effects at its cell: a floating score
label (`+10`, `+50`, `-5`, `+20`, `+15 ♥`) that rises one cell per two visual
pulses and fades from bold through dim, and an expanding sparkle ring (`✦` → `✧`
→ `·`). Both use the same signature color as the apple they came from, so
feedback stays legible even mid-chaos.

## Presentation choices

Each effect was chosen to demonstrate a specific library capability while
keeping gameplay readable:

- **Attract-mode demo game.** The title, record, and game-over screens all float
  above a dim, self-playing Snake game piloted by a small greedy AI
  (`AttractPilot`). It reuses the exact production `GameState` at one-eighth
  brightness, moves at half the visual rate, and restarts when it runs out of
  lives — a classic arcade attract loop. This works because every stretched
  container in the title panel declares a `Color.Transparent` background, so the
  retained overlay composes above the animated board without erasing it.
- **Gradient body with a traveling shimmer.** Body cells fade from bright green
  at the neck to dark green at the tail, and a two-cell highlight sweeps the
  body once per visual cycle. A speed boost swaps the whole ramp to cyan. The
  head is a directional glyph (`▶▲▼◀`) driven by the simulation's committed
  heading.
- **Border as a status ring.** The board border breathes cyan while a boost is
  active and strobes red during the death wave, so game state reads at a glance
  even when the player's eye is nowhere near the HUD.
- **Boost meter.** The HUD hosts a real `ProgressBar` with
  `UseSubCellResolution` enabled; it drains in eighth-cell steps as the boost
  budget expires and collapses to zero width otherwise. `GameState` exposes the
  remaining budget as a normalized `BoostFraction`.
- **Restrained rainbow.** The FIGlet title and new-record headings sit inside a
  diagonal, spatial `Prism`; gameplay itself never receives a rainbow wash, so
  the celebration stays special. A zero-score run never reaches the record
  screen at all.
- **Death wave.** Dying reveals a 12-pulse red/gold wave along the body, holds
  three more pulses, then resumes or ends the game.

## Timing

Simulation follows the selected difficulty's game tick (200 ms, 140 ms, or 90
ms). A speed apple resets a boost tick budget; while boosted, simulation uses
half the base interval with a 40 ms floor. Presentation advances on an
independent 80 ms clock that drives the rainbow phase, apple pulses, shimmer,
border animation, transient effect aging, and the attract-mode demo (which ticks
every second pulse). Only one visual pulse may be queued, so a busy dispatcher
skips stale pulses instead of replaying a catch-up burst. The caller advances
`Prism.Phase` through 60 frames, producing one 4.8-second rainbow cycle; `Prism`
itself owns no timer or animation loop.

## Architecture

- [`Program.cs`](Program.cs) hosts the screen and opts into decoded control
  keys.
- [`SnakeScreen.cs`](SnakeScreen.cs) owns retained composition, phases, routed
  input, the independent game and visual loops, the attract-mode demo lifecycle,
  and eat-effect spawning.
- [`GameState.cs`](GameState.cs) contains simulation rules, scoring, collisions,
  obstacles, and apple effects without UI dependencies, and reports each tick's
  eaten apple through `LastEaten`.
- [`AttractPilot.cs`](AttractPilot.cs) is the pure, deterministic policy that
  steers the demo snake toward apples while avoiding one-step traps.
- [`SnakeTitlePanel.cs`](SnakeTitlePanel.cs) retains the title, record, and
  game-over views, applies FIGlet/Prism presentation, and keeps every stretched
  container transparent so the attract field shows through.
- [`SnakeHud.cs`](SnakeHud.cs) owns the two-row metrics bar and the sub-cell
  `ProgressBar` boost meter.
- [`SnakeBoard.cs`](SnakeBoard.cs) draws the board, the dim demo game, the
  gradient snake, and the transient popup/burst effects through the semantic
  `Canvas` API.
- [`ScorePopup.cs`](ScorePopup.cs) and [`SparkleBurst.cs`](SparkleBurst.cs) are
  immutable effect snapshots the board ages once per visual pulse.
- [`SnakeAnimationState.cs`](SnakeAnimationState.cs) advances the bounded
  rainbow and death-wave counters.

The reusable effect is defined by the normative
[`Prism` control contract](../../docs/controls/display/prism.md#overview). The
surrounding behavior follows SharpVision's
[routed input](../../docs/concepts/input-routing.md#overview),
[runtime event loop](../../docs/architecture/runtime-event-loop.md#overview),
and [rendering pipeline](../../docs/architecture/rendering-pipeline.md#overview)
contracts.

## Verifying a live game in a terminal

The animated UI can be exercised end to end in a scripted tmux session, which is
awkward for an always-moving game; two properties make it tractable:

- **Frame captures as files.** `tmux capture-pane -p` (and `-p -e` for colors)
  snapshots the live screen into numbered files without disturbing the game.
  Diffing consecutive captures proves animation (prism phase, shimmer, demo
  motion); pausing with `P` freezes the simulation while presentation keeps
  running, which gives stable frames for layout inspection.
- **Closed-loop steering.** Because a direction key triggers an immediate
  simulation tick, a small script can capture the pane, locate the head and the
  nearest apple in the text, send one arrow key, and repeat — reliably driving
  the snake into apples to exercise eat popups, sparkle bursts, the boost meter,
  the death wave, and the record flow.
