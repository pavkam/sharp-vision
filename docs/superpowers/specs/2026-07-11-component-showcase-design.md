# Component showcase and external assets

## Goal

Replace the broad family-based showcase with a runnable terminal component
catalog. The sidebar lists only concrete visual controls. Selecting an item
opens a documentation-style page composed from public SharpVision controls, with
live examples, meaningful property guidance, interaction notes, and automatic
scrolling.

The same work moves vendored resource inputs into a tidy top-level `extern/`
tree with explicit provenance and licensing information.

## Component catalog

The first version registers one page for every concrete control currently
shipped by `SharpVision`: Border, Button, Canvas, CheckBox, ComboBox, Dock,
FigletText, Grid, List, Menu, Overlay, Popup, RadioButton, RichText, ScrollBar,
ScrollView, Shadow, Stack, Table, Text, TextInput, and Window.

Foundation abstractions and content primitives such as Control, Container,
Pressable, Children, Inline, Run, Hyperlink, and LineBreak do not receive
sidebar entries. They may be used inside examples and described where they are
part of a concrete control's public authoring model.

New concrete controls are added to the sidebar only after their implementation,
public contract, representative showcase page, and behavioral tests exist.

## Page structure

Each selected page is built by a catalog entry and a reusable page shell. The
shell uses ordinary `Stack`, `RichText`, `Border`, and layout controls; it does
not introduce reflection, a virtual tree, hooks, or private rendering paths.

Every page contains:

1. the control name and a concise statement of purpose;
2. live examples showing representative states or configurations;
3. a properties section covering the attributes that materially affect layout,
   appearance, content, or behavior;
4. keyboard and pointer interaction guidance where the control is interactive;
5. practical usage or degradation notes where terminal constraints matter.

Property descriptions name the public member, its value shape or default when
useful, and the observable effect. Inherited base properties are mentioned only
when a page demonstrates them; the showcase avoids repeating the complete base
API on every page.

## Navigation and layout

The root is a traditional `Dock`: a fixed-width framed sidebar on the left and a
`ScrollView` main pane consuming the remaining space. The sidebar owns the
product identity and one stateful navigation entry per concrete control; the
selected catalog entry owns the exact sidebar label and creates fresh page
content when selection changes. The follow-up
[dashboard specification](2026-07-12-showcase-dashboard-design.md#dashboard-composition)
defines its colored visual treatment and explicit cell-mouse startup policy. The
main pane enables automatic horizontal and vertical scrollbars.

Pages remain usable after terminal resize. Tiny layouts may clip or introduce
scrollbars, but must not throw, create negative geometry, or lose the selected
page. Keyboard and pointer selection continue through the public input-routing
and focus paths.

## External resource layout

All checked-in third-party source material and embedded resource payloads live
under the top-level `extern/` directory:

- `extern/figlet/` contains the deterministic font archive, audit manifest,
  provenance README, and licensing/redistribution notice;
- `extern/unicode/17.0.0/` contains the pinned Unicode Character Database and
  test inputs, with a parent README and the Unicode license or terms supplied
  with the snapshot.

The old `data/` tree is removed. Product project files embed resources from
`extern/` while preserving stable logical resource names, so public loading
behavior does not depend on repository paths. Audit and generation scripts use
the new locations. Documentation and package commands contain no stale paths.

The FIGlet notice continues to expose the unresolved upstream redistribution
classifications rather than representing them as approved licenses.

## Testing and visual proof

Tests are written before production changes and prove that:

- the sidebar inventory exactly matches the concrete shipped controls and
  renders a distinct selected state;
- every entry creates a page with typed `RichText`, live examples, and property
  documentation;
- selecting every entry updates the title and replaces page content;
- representative interactive examples respond through public keyboard and
  pointer paths after the executable showcase has emitted its SGR cell-mouse
  enable sequence;
- layout at tiny, typical, and large terminal sizes remains contained and uses
  scrolling where required;
- embedded FIGlet resources still load from the renamed source paths and all 400
  audited entries parse;
- repository checks reject stale `data/` resource references.

After focused and full automated gates pass, the actual showcase is launched in
a pseudoterminal or `tmux` pane at representative dimensions. Its pane is
captured and converted to an image for visual inspection. The image supplements
cell, event, focus, resize, and scrolling assertions; it never replaces them.

## Documentation

[Showcase architecture](../../architecture/showcase.md#showcase-contract) is
updated from family pages to one page per concrete control. The
[showcase testing contract](../../testing/showcase.md#showcase-testing) records
the live-pane capture alongside behavioral and virtual-screen tests. Resource
provenance remains adjacent to the files under `extern/`, while product docs
link to those notices inline where redistribution status matters.
