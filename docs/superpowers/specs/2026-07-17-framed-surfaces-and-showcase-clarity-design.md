# Framed Surfaces and Showcase Clarity Design

## Goal

Make collection and disclosure controls read as distinct surfaces by default,
and make every live Showcase specimen communicate its scenario without
accidental truncation or unexplained shorthand.

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

The `NavigationView`, `List`, and `Expander` pages demonstrate their constructor
defaults rather than repeating explicit frame configuration. At least one
example may deliberately override the default with rounded or borderless chrome,
but its heading and description must state that it is an override.

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
rule. The Showcase architecture contract requires live specimen labels to be
self-explanatory and fully visible unless a documented clipping example is
teaching overflow behavior.

## Proof

Focused constructor tests assert the three controls' public default values.
Mounted surface tests assert exact square frame cells, opaque body cells, and
content-box geometry at normal and tiny sizes. List and navigation tests verify
that idle rows reveal the owner surface while hover/current and selected rows
remain distinguishable.

Showcase tests render the affected pages and assert complete semantic labels at
their final cell positions. Existing inventory, narrow-layout, interaction,
theme, Unicode-continuation, and full repository gates remain green. Visual
inspection supplements the cell assertions for the full live-page audit.
