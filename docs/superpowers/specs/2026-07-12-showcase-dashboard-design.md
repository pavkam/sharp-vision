# Showcase dashboard and mouse activation

## Goal

Turn `SharpVision.Showcase` into a usable terminal documentation dashboard. The
application must present a visually distinct sidebar, a colored documentation
surface, readable live-example and property cards, and working click navigation
in terminals that accept standard SGR cell mouse reporting.

## Capability policy

The library remains conservative: environment hints alone do not enable an
optional terminal mode. The showcase is an application with an explicit input
requirement, so its startup configuration supplies an explicit `CellMouse`
override to `Detector.Detect`. This produces supported override evidence and
allows `Runtime.Session` to enable VT200 press tracking and SGR cell reporting
through the existing typed mode lease.

Terminals that ignore those DEC private modes remain keyboard navigable. The
showcase does not claim pixel mouse support, and it does not override any
unrelated optional capability.

## Dashboard composition

The root uses a traditional `Dock` with two deliberate surfaces:

- a 28-cell left `Border` panel holding the product identity, a scrollable
  vertical navigation stack, and compact keyboard/pointer hints;
- a remaining `ScrollView` holding the selected page in a padded colored
  surface.

Navigation entries are small stateful `Pressable` controls rather than unstyled
`List` items. Every entry has a stable page index and invokes the same selection
path through keyboard or a primary pointer click. The selected entry uses an
accent background and foreground; normal, hovered, focused, pressed, and
disabled states use explicit visual-state overlays. Selecting a page replaces
only the main content tree and updates the current entry styling.

The dashboard palette is intentionally conservative: indexed colors provide a
coherent dark navy, cyan, violet, green, amber, and muted-text treatment in
basic capable terminals while retaining semantic contrast when color is reduced.
No content relies on color alone; labels, borders, and selection markers remain
visible without it.

Page content uses an accent header, a thin metadata line, section labels,
bordered example surfaces, and two-line property cards. The page shell remains
composed entirely from public `Stack`, `Border`, `RichText`, `ScrollView`, and
layout APIs.

## Interaction and resize

At startup the showcase emits the standard typed mouse lease before rendering.
Its selection path is proven from the mode-enable bytes, through decoded SGR
input, routed navigation activation, page replacement, and final screen styling.

On Linux and macOS, the executable host also owns a raw no-echo terminal input
lease while it runs and restores the exact saved state during cleanup. It keeps
terminal signals enabled so Ctrl+C continues to use the normal cancellation
path. Unsupported hosts retain keyboard fallback without changing the terminal
state.

Keyboard Tab/Enter and primary pointer activation are equivalent navigation
actions. The initially selected entry receives focus after the first frame; Up,
Down, Left, Right, Tab, Shift+Tab, Home, End, and Page keys navigate the sidebar
selection and preserve visibility. The sidebar and main pane independently
scroll when their content overflows. At tiny dimensions, borders and text clip
safely, selected state persists, and neither input nor resize throws.

On Unix the host opens `/dev/tty` as a one-byte asynchronous input stream after
entering raw mode. That is intentionally separate from normal standard input:
complete escape-prefixed reports must wake the decoder immediately instead of
being delayed until a later character arrives. Other platforms retain a safe
standard-input fallback.

## Tests and visual proof

Tests prove all of the following:

- the startup transport receives the exact SGR mouse enable sequence under the
  showcase's explicit capability policy;
- a raw primary SGR click selects a sidebar page through the running
  application, rather than by injecting directly into a control;
- the sidebar is a bordered visual region containing an identity label,
  navigation entries, a selected marker, and non-default cell colors;
- selected, hovered, focused, and pressed navigation appearances are observable
  in virtual-screen cells;
- the dashboard remains contained and navigable at tiny, typical, and large
  terminal sizes;
- the live tmux capture sends an ordinary Down key plus complete Canvas and
  Button SGR clicks, each without a synthetic trailing key, and observes every
  corresponding page change.

The Release application is captured from a 120 by 40 `tmux` pane after the
automated tests pass. The checked-in image demonstrates the real sidebar and
colored page chrome but supplements, rather than replaces, byte, routing,
resize, and virtual-screen assertions.
