# Control API specifications

## Control catalog

All controls derive from the
[base control contract](control.md#control-contract) and use the shared
[layout](../concepts/layout.md#layout-contract),
[styling](../concepts/styling.md#styling-contract), and
[input](../concepts/input-routing.md#input-routing-contract) rules.

### Authoring roles

- [ContentControl](content-control.md#contentcontrol-contract) owns zero or one
  publicly replaceable content control.
- [CompositeControl](composite-control.md#compositecontrol-contract) owns one
  retained private implementation root initialized by the concrete constructor.
- [Pressable](pressable.md#pressable-contract) adds focus and completed
  activation to that single-content role.

### Display

- [Text](display/text.md#text-contract)
- [FigletText](display/figlet-text.md#figlettext-contract)
- [Prism](display/prism.md#prism-contract)
- [Separator](display/separator.md#separator-contract)
- [ProgressBar](display/progress-bar.md#progressbar-contract)
- [Spinner](display/spinner.md#spinner-contract)
- [ChaseIndicator](display/chase-indicator.md#chaseindicator-contract)

Border and shadow are intrinsic `Control` chrome configured through
`BorderThickness`, `BorderGlyphs`, `HasShadow`, `ShadowMode`, and the related
style properties; neither is a standalone control. `BorderThickness` always
reserves layout through the base box model, and the sealed `Control` renderer
paints configured chrome around every control's `OnRenderContent` callback.
Specialized controls use narrow chrome options when their frame geometry is
bespoke. See the [shared chrome contract](../concepts/styling.md#shared-chrome).

### Input

- [Button](input/button.md#button-contract)
- [CheckBox](input/check-box.md#checkbox-contract)
- [ColorPicker](input/color-picker.md#colorpicker-contract)
- [ComboBox](input/combo-box.md#combobox-contract)
- [RadioButton](input/radio-button.md#radiobutton-contract)
- [Slider](input/slider.md#slider-contract)
- [TextInput](input/text-input.md#textinput-contract)

### Layout and scrolling

- [Stack](layout/stack.md#stack-contract)
- [Grid](layout/grid.md#grid-contract)
- [Dock](layout/dock.md#dock-contract)
- [Overlay](layout/overlay.md#overlay-contract)
- [Canvas](layout/canvas.md#canvas-contract)
- [Table](layout/table.md#table-contract)
- [ScrollBar](layout/scroll-bar.md#scrollbar-contract)
- [GroupBox](layout/group-box.md#groupbox-contract)
- [Expander](layout/expander.md#expander-contract)

### Collections, menus, and windows

- [List](collections/list.md#list-contract)
- [TabControl and TabItem](collections/tab-control.md#tabcontrol-contract)
- [Menu](menus/menu.md#menu-contract)
- [MenuItem and MenuSeparator](menus/menu-item.md#menuitem-contract)
- [NavigationView](menus/navigation-view.md#navigationview-contract)
- [Popup](windows/popup.md#popup-contract)
- [Window](windows/window.md#window-contract)
