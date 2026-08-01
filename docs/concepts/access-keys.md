# Access keys

## Overview

An access key is a caption-local keyboard action declared by an ampersand. It is
distinct from a command shortcut such as `Ctrl+S`: the caption owns the access
key, SharpVision discovers it from the current retained tree, and the captioned
control decides whether the result is focus transfer, selection, toggle, popup
opening, or invocation.

This contract follows the Windows Forms access-key convention documented in
Microsoft's
[Add an access key shortcut to a control](https://learn.microsoft.com/dotnet/desktop/winforms/controls/how-to-create-access-keys)
(accessed 2026-07-18) and the VCL `Caption` accelerator convention documented in
Embarcadero's
[`TControl.Caption`](https://docwiki.embarcadero.com/Libraries/Sydney/en/Vcl.Controls.TControl.Caption)
(accessed 2026-07-18).

## Caption syntax

The first unescaped `&` followed immediately by a valid Unicode scalar declares
the access key. Matching is invariant and case-insensitive. The marker occupies
no cells; the complete grapheme beginning with the marked scalar is underlined
and uses the active theme's `Hotkey` status foreground while its caption owner
is enabled. For example, `"&Save"` renders `Save`, and `"&e\u0301dit"`
underlines the one-cell grapheme `é` rather than separating its combining mark.

`&&` renders one literal ampersand and declares no key. A trailing `&` also
renders literally because it has no following scalar. When more than one
unescaped marker is present, every marker is removed from visible text, but only
the first marked scalar is the access key and only its grapheme is underlined.

`Control.UseMnemonic` defaults to `true` for captioned controls. Setting it to
`false` removes that control from access-key discovery and renders its caption
ampersands literally. `Text` is a rich/body-text control and therefore defaults
`UseMnemonic` to `false`; set it explicitly to `true` when a standalone `Text`
acts as a label. A `Text` used as a `Pressable` caption inherits the owner's
effective setting, so visible syntax and dispatch cannot disagree.

## Dispatch precedence

Only a pressed `Code.Character` stroke containing `Alt` qualifies. `Shift`,
`CapsLock`, and `NumLock` may accompany Alt; Control, Super, Hyper, Meta, an
unknown modifier, a repeat/release transition, or a non-character key does not
enter access-key dispatch.

The application first completes the ordinary stable preview/bubble route and
each control default. A handled route keeps the key. Only an unhandled stroke
reaches access-key discovery, so an application or control can reserve Alt input
with the normal routed-event API.

Legacy terminals represent an Alt character as an adjacent stroke/text pair.
When the stroke activates an access key, `Application` consumes only the
immediately adjacent equal text Rune. The character therefore cannot leak into
the newly focused editor. A declined or routed-handled stroke leaves its text
record untouched.

## Discovery eligibility and duplicates

Discovery snapshots the current ownership tree for each qualifying stroke. There
is no mutable registration table to synchronize with caption, enabled,
visibility, ownership, popup, or modal changes. Traversal is deterministic
preorder over registered ownership slots. An active modal plane replaces the
application root with its insertion-ordered plane roots.

Detached, disposed, hidden, collapsed, disabled, `UseMnemonic = false`, and
out-of-plane controls are excluded. A private or public `Text` caption owned by
a semantic control is not a second candidate for the same marker.

Duplicate keys are valid. If focus is inside one matching candidate, that
candidate is the cycle anchor and the next match is tried, with wrapping.
Otherwise traversal starts at the first match. A candidate that declines its
action does not prevent a later match from accepting it.

## Focus and semantic actions

The common `Control.OnAccessKey(Rune)` default focuses an eligible captioned
control. A non-focusable captioned scope focuses its first eligible descendant
in hierarchical tab order. A label-like leaf advances through the same focus
traversal from its stable tree anchor.

Built-in action controls specialize that default without inventing a second
state path:

| Caption owner          | Access-key action                                                 |
| ---------------------- | ----------------------------------------------------------------- |
| `Button`               | Focuses and clicks with `ActivationCause.Keyboard`.               |
| `CheckBox`             | Focuses and toggles with `ActivationCause.Keyboard`.              |
| `RadioButton`          | Focuses and selects with `ActivationCause.Keyboard`.              |
| `Expander`             | Focuses and toggles expansion.                                    |
| `MenuItem`             | Selects/focuses its `Menu`, then opens its submenu or invokes.    |
| `TabItem` header       | Focuses its `TabControl` and selects the page.                    |
| `NavigationViewItem`   | Focuses the view, makes the item current, and invokes/selects it. |
| `NavigationViewGroup`  | Focuses the view, makes the group current, and toggles it.        |
| `GroupBox` or `Window` | Focuses the first eligible descendant.                            |

Unavailable controls never activate. Menu, popup, and window actions retain
their existing modality, focus restoration, selection, and event ordering.

## Rendering integration

Marker collapse, escaping, Unicode measurement, and segmented underline drawing
have one implementation. Direct `Header` and `Title` renderers use the same
visible width for measure and draw. `Text` converts enabled access syntax into
its existing semantic markup before grapheme layout, so clipping, wrapping,
wide-cell ownership, inherited background, attributes, and underline metadata
remain authoritative. Only the marked grapheme receives the resolved
`Theme.Hotkey` foreground. A disabled owner retains its disabled foreground
while marker collapse and underline remain visible.

## Expected behavior

Tests cover marker removal, `&&`, trailing markers, disabled parsing, invariant
Rune matching, combining and wide graphemes, exact cells, underline style,
access-key theme resolution, disabled-foreground preservation, dynamic
caption/tree mutation, duplicate cycling, routed interception, modifier
filtering, unavailable controls, modal confinement, scope focus transfer, every
built-in semantic action, and adjacent key/text suppression. Showcase inventory
tests require an intentional marker on every authored interactive specimen,
unique keys across each selected page and the complete active application tree,
and no ancestor collision along an open menu path. Generated list data, body
prose, repeated documentation chrome, and the arrow-navigated catalog sidebar do
not participate.
