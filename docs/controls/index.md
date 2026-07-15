# Control API specifications

## Control catalog

All controls derive from the
[base control contract](control.md#control-contract) and use the shared
[layout](../concepts/layout.md#layout-contract),
[styling](../concepts/styling.md#styling-contract), and
[input](../concepts/input-routing.md#input-routing-contract) rules.

### Display

- [Text](display/text.md#text-contract)
- [FigletText](display/figlet-text.md#figlettext-contract)

Border and shadow are intrinsic `Control` chrome configured through
`BorderThickness`, `BorderGlyphs`, `HasShadow`, `ShadowMode`, and the related
style properties; neither is a standalone control. `BorderThickness` always
reserves layout through the base box model, while visible chrome requires a
render path that calls `RenderChrome` or a specialized equivalent. Use an
ordinary chrome-rendering container around a sealed bespoke renderer that calls
neither path when it needs a visible frame or shadow. See the
[shared chrome contract](../concepts/styling.md#shared-chrome).

### Input

- [Button](input/button.md#button-contract)
- [CheckBox](input/check-box.md#checkbox-contract)
- [ComboBox](input/combo-box.md#combobox-contract)
- [RadioButton](input/radio-button.md#radiobutton-contract)
- [TextInput](input/text-input.md#textinput-contract)

### Layout and scrolling

- [Stack](layout/stack.md#stack-contract)
- [Grid](layout/grid.md#grid-contract)
- [Dock](layout/dock.md#dock-contract)
- [Overlay](layout/overlay.md#overlay-contract)
- [Canvas](layout/canvas.md#canvas-contract)
- [Table](layout/table.md#table-contract)
- [ScrollBar](layout/scroll-bar.md#scrollbar-contract)

### Collections, menus, and windows

- [List](collections/list.md#list-contract)
- [Menu](menus/menu.md#menu-contract)
- [MenuItem](menus/menu-item.md#menuitem-contract)
- [Popup](windows/popup.md#popup-contract)
- [Window](windows/window.md#window-contract)
