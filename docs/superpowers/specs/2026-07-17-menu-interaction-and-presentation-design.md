# Menu Interaction and Presentation Design

## Goal

Make horizontal menu bars, vertical flyout menus, and nested submenus compact,
visually coherent, pointer-responsive, and fully keyboard operable without
weakening the generic `Popup` contract.

## Responsibility boundaries

- `Menu` owns item selection, compact sequential layout, Tab/Shift+Tab and
  directional navigation, activation of the selected item, and switching an
  already-open sibling submenu when pointer or keyboard selection moves.
- `MenuItem` owns its retained submenu popup, row-width participation,
  Unicode-aware shortcut measurement/rendering, submenu placement and visual
  presentation, and focus restoration when its submenu closes.
- `MenuSeparator` owns a one-row rule that stretches across the containing
  menu's cross axis.
- `ControlAppearanceDefaults` gives `MenuItem` an explicit interactive hover
  surface even though menu items are private faces and the `Menu` retains the
  single focus stop.
- `Popup` remains generic. Its existing open transition closes unrelated popups,
  preserves ancestor popup chains, promotes rendering, performs light dismissal,
  and constrains geometry. Menu policy must not leak into it.

## Layout and rendering

`Menu.Spacing` defaults to zero. Callers may still opt into spacing, such as a
horizontal menu bar. Vertical menu rows and separators stretch to the menu's
shared width, making shortcut hints share one trailing edge and separators span
the flyout interior. Shortcut widths and trailing positions use terminal cell
geometry rather than UTF-16 length.

Submenu popups use the theme surface background and square light frame so they
read as part of the menu system. A submenu opens below an item in a horizontal
menu and to the right of an item in a vertical menu; generic popup edge flipping
still keeps the surface inside the terminal.

## Interaction

Pointer motion over an available item selects and highlights it. Hover alone
does not open a dormant menu. Once any sibling submenu is open, moving to a new
available item closes the previous sibling popup and opens the new item's
submenu. Moving to an item without a submenu closes the previous sibling popup.

Directional keys follow menu orientation. Tab selects the next available item
and Shift+Tab selects the previous item, both wrapping and remaining inside the
menu. Enter and Space activate the selected item because the menu—not its
private item faces—owns keyboard focus. When selection moves while a sibling
submenu is open, keyboard navigation switches the open submenu as pointer
navigation does.

Opening transfers focus into the submenu. Closing restores focus to the owning
menu before the submenu becomes unavailable. Disabled, hidden, and separator
entries neither select, highlight interactively, open, nor invoke.

## Proof

Focused unit tests cover zero-spacing geometry, uniform row widths, full-width
separators, Unicode shortcut alignment, Tab/Shift+Tab, activation, disabled
skipping, submenu placement, sibling switching, and focus restoration. Mounted
surface tests drive raw terminal pointer and keyboard input through the real
application and assert hover state, selected state, final colors/cells, popup
switching, and exact compact output. The showcase menu page retains horizontal
spacing explicitly and demonstrates vertical compact menus and nested submenu
placement.
