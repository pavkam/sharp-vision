# SharpVision Snake

Snake is a playable terminal example for SharpVision. It demonstrates retained,
mutable controls; FIGlet headings colored by `Prism`; a custom cell-canvas game
board; routed keyboard input; and independent simulation and presentation loops.

## Run it

Install the .NET 10 SDK, then use a modern Unicode terminal. A true-color
terminal at least 100 columns by 30 rows gives the intended layout and palette.

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

## Apples

The board pulses each apple between bright and dim variants while preserving a
small, readable gameplay palette.

| Glyph | Kind   | Effect                                                                       |
| ----- | ------ | ---------------------------------------------------------------------------- |
| `●`   | Normal | Adds 10 points and grows by one segment                                      |
| `◆`   | Golden | Adds 50 points and grows by three segments                                   |
| `✦`   | Poison | Removes 5 points, down to zero, and shrinks twice; costs a life if too short |
| `★`   | Speed  | Adds 20 points and resets the speed-boost tick budget                        |
| `♥`   | Life   | Adds 15 points and restores one life, up to five                             |

The ordinary snake is green, speed accents are cyan, pause is gold, and the
death wave alternates red and gold over a dim green body. Gameplay never
receives a full rainbow wash.

## Presentation and timing

The title uses FIGlet text inside a diagonal, spatial `Prism`; the same
restrained rainbow returns for a new-record celebration and nowhere else. The
title screen layers those headings and balanced action/high-score cards over a
sparse animated attract field. During play, apples and the snake head pulse,
boosted segments flash cyan, the pause card stays inside the board, and a
12-pulse red/gold death wave holds for three more pulses before play continues.

The two-row HUD keeps score, lives, difficulty, best score, status, contextual
movement guidance, and the full `CTRL+Q  QUIT` hint organized without covering
the board.

Simulation follows the selected difficulty's game tick (200 ms, 140 ms, or 90
ms). A speed apple resets a boost tick budget; while boosted, simulation uses
half the base interval with a 40 ms floor. Presentation advances on an
independent 80 ms clock. Only one visual pulse may be queued, so a busy
dispatcher skips stale pulses instead of replaying a catch-up burst. The caller
advances `Prism.Phase` through 60 frames, producing one 4.8-second rainbow
cycle; `Prism` itself owns no timer or animation loop.

## Architecture

- [`Program.cs`](Program.cs) hosts the screen and opts into decoded control
  keys.
- [`SnakeScreen.cs`](SnakeScreen.cs) owns retained composition, phases, routed
  input, and the independent game and visual loops.
- [`GameState.cs`](GameState.cs) contains simulation rules, scoring, collisions,
  obstacles, and apple effects without UI dependencies.
- [`SnakeTitlePanel.cs`](SnakeTitlePanel.cs) retains the title, record, and
  game-over views and applies FIGlet/Prism presentation.
- [`SnakeHud.cs`](SnakeHud.cs) owns the two-row metrics and shortcut bar.
- [`SnakeBoard.cs`](SnakeBoard.cs) draws the board and animations through the
  semantic `Canvas` API.
- [`SnakeAnimationState.cs`](SnakeAnimationState.cs) advances the bounded
  rainbow and death-wave counters.

The reusable effect is defined by the normative
[`Prism` control contract](../../docs/controls/display/prism.md#prism-contract).
The surrounding behavior follows SharpVision's
[routed input](../../docs/concepts/input-routing.md#input-routing-contract),
[runtime event loop](../../docs/architecture/runtime-event-loop.md#runtime-event-loop-contract),
and
[rendering pipeline](../../docs/architecture/rendering-pipeline.md#rendering-pipeline-contract)
contracts.
