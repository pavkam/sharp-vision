# HeaderedContentControl base API

## Overview

`HeaderedContentControl` is the abstract base for a control that owns a single
replaceable `Content` — inherited from
[`ContentControl`](content-control.md#overview) — plus an independent single
replaceable `Header`. It derives from `ContentControl`, so the two slots are
completely separate ownership edges: the same control instance can never be
assigned to both at once.

Use `HeaderedContentControl` when a component has a caller-replaceable title or
label alongside its main content, and that title should accept an ordinary
control rather than being locked to plain text.
[`GroupBox`](layout/group-box.md#overview),
[`Expander`](layout/expander.md#overview), and
[`TabItem`](collections/tab-control.md#tabitem) all derive from it. Each retains
full control over how its own header is arranged and rendered alongside its own
chrome; this role only owns the header's lifecycle and the plain-text
convenience over it.

## API

| Member       | Default | Purpose                                                                                    |
| ------------ | ------- | ------------------------------------------------------------------------------------------ |
| `Header`     | `null`  | Transfers ownership of zero or one detached `ControlBase`, independently of `Content`.     |
| `HeaderText` | Empty   | Reads or writes `Header` as plain text, materializing or mutating an owned `Text` caption. |

## Ownership and mutation

`Header` follows the exact ownership, replacement, and disposal contract
[`ContentControl.Content`](content-control.md#ownership-and-mutation) already
documents, through its own capacity-one, normal-layer slot: assignment,
equivalence, clearing, and every rejection path (disposed, attached,
already-owned, duplicate-slot, cross-parent, cyclic) behave identically, and a
control already owned as `Content` cannot also be assigned as `Header`, or the
reverse. A derived class observes committed header changes through
`OnHeaderChanged(previous, current)`, the header counterpart of
`OnContentChanged`, and `PropertyChanged(nameof(Header))` follows the same
publication order.

## HeaderText

`HeaderText` is a convenience, not a second storage location. Reading it returns
the current header's text when `Header` is a `Text` control, and an empty string
otherwise. Assigning it mutates an existing `Text` header in place; any other
header, including none, is replaced by a newly materialized `Text`. Because a
`Text` header never allocates until first assigned, a consumer that only ever
sets `HeaderText` pays no cost for the richer `Header` slot underneath it.
`HeaderText` notifies exactly once per committed change and is silent on
same-value assignment, matching
[`Pressable<TStyle>.Text`](pressable.md#overview)'s convention on the unrelated
caption role.

## Access keys

`AccessKeyText` projects `Header` through `IAccessKeyCaption` when the header
implements it — which a `Text` header does — and is `null` for any other header
or when none is assigned. An ampersand in `HeaderText` therefore declares an
[access key](../concepts/access-keys.md#focus-and-semantic-actions) exactly as
it did when these controls exposed a plain string `Header`.

## Layout and rendering

This role owns no layout or rendering override of its own: it does not measure,
arrange, or draw `Header`. Each derived control decides where its header sits
and how it participates in that control's own chrome — a titled border edge for
`GroupBox`, a disclosure-glyph-prefixed row for `Expander` — using the same
`MeasureChild`/`ArrangeChild` primitives available to any owned control.

## Expected behavior

`Header` behaves like `Content` for the full assignment matrix: null, first
assignment, equivalent assignment, replacement, clearing, and every rejection
path, all independent of the inherited `Content` slot. `HeaderText` materializes
once and mutates in place thereafter, notifies as documented, and falls back to
replacement for a non-`Text` header. `AccessKeyText` resolves only through a
`Text` header. Tests cover ownership, replacement, the `HeaderText` convenience
over a rich header, access-key projection, and a consumer-derived control
composing the role alongside its own state.
