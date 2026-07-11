# SharpVision First-Milestone Roadmap

> **For agentic workers:** Each phase requires its own detailed implementation
> plan before code changes. Execute plans inline unless the user explicitly
> requests subagent delegation.

**Goal:** Deliver the approved SharpVision first milestone through six
independently verifiable phases.

**Architecture:** Establish repository-wide guardrails first, then build upward
from terminal protocols to rendering, UI infrastructure, controls, and the
showcase. Every phase ends with format, lint, build, and test gates so later
work never rests on an unverified layer.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Moq, Markdown, Prettier,
Markdownlint, GitHub Actions

---

## Phase 1: Repository foundation

Create the solution and project graph, strict build and formatting policy,
normative documentation tree, root `AGENTS.md`, domain skills, test conventions,
and compiling project/test shells.

**Exit:** All six projects restore, format, lint, build, and test from root
commands; documentation and skill link checks pass.

## Phase 2: Terminal protocol engine

Implement bounded sequence encoding/decoding, protocol limits, typed CSI/OSC
commands, OSC 52 and Kitty OSC 5522 clipboard support, capability profiles, and
safe degradation diagnostics.

**Exit:** Exact-byte tests, all-fragment-boundary parser tests, hostile-payload
limits, and protocol documentation coverage pass.

## Phase 3: Text, input, rendering, and transport

Implement extended-grapheme segmentation, width policy, cell buffers, damage
tracking, frame diffing, typed keyboard/mouse/paste/focus/resize input,
transports, terminal lifecycle restoration, and runtime events.

**Exit:** Unicode geometry, parser recovery, frame-equivalence, resize, pixel
mouse, pseudoterminal, allocation, and throughput checks pass.

## Phase 4: UI infrastructure

Implement the single-thread dispatcher, application/window lifecycle,
traditional control tree, invalidation, routed events, focus, pointer capture,
styling, and measure/arrange layout with fixed, percentage, automatic, and
proportional sizing.

**Exit:** Cross-thread, parenting, routing, state precedence, resize, and layout
invariant tests pass through the terminal canvas boundary.

## Phase 5: Controls and scrolling

Implement the initial display, input, selection, container, menu, popup, window,
scrollbar, scroll view, and `RichText` controls with full public docs.

**Exit:** Every control has keyboard, pointer, focus, disabled, style, render,
and end-to-end interaction proof; automatic and nested scrolling pass.

## Phase 6: Showcase and hardening

Build the navigable showcase, interactive variants, live event log, responsive
sidebar behavior, integrated rich documentation, representative screen tests, CI
matrix, packaging metadata, and final performance hardening.

**Exit:** Every shipped control is registered, documented, demonstrated, and
tested in the showcase; all repository quality gates pass cleanly.
