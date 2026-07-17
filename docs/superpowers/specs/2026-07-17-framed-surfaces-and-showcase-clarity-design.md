# Framed Surfaces and Showcase Clarity Design

## Goal

Make semantically bounded controls read as distinct fields or surfaces by
default, and make every live Showcase specimen communicate its scenario
without accidental truncation or unexplained shorthand.

## Semantic frame policy

Border defaults belong to the concrete control whose public role promises a
visibly bounded field, content surface, group, popup, or window. Inheritance
from `Control` does not add a border. This keeps the default discoverable for
interactive fields while avoiding incidental frames around content and layout
implementation details.

`TextInput`, `ComboBox`, `Button`, `List`, `NavigationView`, `Expander`,
`GroupBox`, `Popup`, and `Window` therefore present bounded chrome through
their dedicated control contracts. Pure content controls such as `Text` and
`FigletText`; layout controls such as `Stack`, `Grid`, `Dock`, `Overlay`, and
`Canvas`; and semantic item rows, separators, tracks, and indicators remain
borderless unless their own control contract explicitly states otherwise.

The policy introduces no styling layer or new public API. Each bounded control
sets its constructor defaults through the existing intrinsic chrome properties,
and every caller can opt out with `BorderThickness = default` where that
property provides the frame. Controls with bespoke frame geometry retain their
dedicated chrome contract.

## Default framed surfaces

`NavigationView`, `List`, and `Expander` default to the same intrinsic chrome:

- `BorderThickness` is one cell on every edge;
- `BorderGlyphs` is `Glyphs.Light`, producing square corners independently of
  the active theme's general chrome family; and
- `Background` is `ColorRole.Surface`, making the complete arranged body an
  opaque raised or inset surface.

These are ordinary constructor defaults. Callers retain the existing public
properties and may remove the frame, choose another glyph family, or replace the
background without a new styling API.

The controls continue to use the base box model. The border consumes one cell
inside every arranged edge, content is measured and arranged within the
remaining content box, and tiny bounds saturate without negative geometry.

`TextInput` and the closed `ComboBox` field default to a one-cell border on
every edge with `Glyphs.Light`. Their existing appearance policies remain
unchanged: ComboBox keeps its opaque `ColorRole.Surface` field body and
TextInput keeps its current state colours. Explicit `BorderThickness` and
`BorderGlyphs` assignments remain authoritative, including a zero-thickness
borderless field.

## Transparent content and interactive states

Normal `NavigationViewItem` and realized `ListItem` rows do not paint a
background. Their text and glyphs render transparently over the owning control's
`Surface` body. Expander header glyphs, header text, and caller content follow
the same rule unless the caller gives a descendant an explicit background.

Interactive overlays remain local to the row or header. Selected rows continue
to paint `SelectionBackground` with `SelectionForeground`. Hover and current
states must remain visually distinguishable from the owning `Surface`; no hover
overlay may resolve to the same background as the new normal body. Disabled
foreground treatment remains unchanged.

## Showcase examples

The `NavigationView`, `List`, `Expander`, `TextInput`, and `ComboBox` pages
demonstrate their constructor defaults rather than repeating explicit frame
configuration. At least one example may deliberately override the default with
rounded or borderless chrome, but its heading and description must state that
it is an override.

Every live page under `src/SharpVision.Showcase/Panes/` is audited at the
supported wide Showcase layout. A specimen is corrected when fixed geometry,
padding, or chrome unintentionally clips a semantic label, when a visible
abbreviation is not defined by the surrounding explanation, or when generic
labels such as `Main` fail to identify the demonstrated application region.

The Dock application-shell specimen uses readable region names that fit their
committed rectangles. `Explorer`, `Application header`, `Inspector`,
`Status bar`, and `Editor workspace` remain visible in full. Intentional
clipping demonstrations remain clipped because truncation is their subject;
their headings and descriptions explicitly identify that behavior.

This audit covers the runnable documentation described by the
[Showcase contract](../../architecture/showcase.md#showcase-contract). It does
not rewrite unrelated normative Markdown examples whose identifiers are already
meaningful in code context.

## Documentation alignment

The `List`, `NavigationView`, and `Expander` control specifications state the
new frame, glyph, and background defaults plus the transparent normal-content
rule. The `TextInput` and `ComboBox` specifications state their light frame
defaults and explicit borderless opt-out. The Showcase architecture contract
requires live specimen labels to be self-explanatory and fully visible unless
a documented clipping example is teaching overflow behavior.

## Proof

Focused constructor tests assert every affected control's public default values.
Mounted surface tests assert exact light frame cells, opaque body cells where
specified, and content-box geometry at normal and tiny sizes. TextInput tests
prove text, selection, caret, and owned scrollbars stay inside the default
frame. ComboBox tests prove the selected label and disclosure glyph stay inside
the closed field while the owned popup retains its independent frame. List and
navigation tests verify that idle rows reveal the owner surface while
hover/current and selected rows remain distinguishable.

Showcase tests render the affected pages and assert complete semantic labels at
their final cell positions. Existing inventory, narrow-layout, interaction,
theme, Unicode-continuation, and full repository gates remain green. Visual
inspection supplements the cell assertions for the full live-page audit.
