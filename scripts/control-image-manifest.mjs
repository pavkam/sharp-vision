/// Each entry maps one showcase page to one control document. `doc` is the
/// path under docs/controls without extension; the image slug is its basename.
/// A state without a name is the default capture. `actions` run before the
/// capture; `popup: true` widens the crop to rows the actions changed, and
/// `animated: true` skips the stable-frame wait for continuously moving pages.
export const controls = [
    { doc: "control", page: "Control" },
    {
        doc: "input/button",
        page: "Button",
        states: [
            {},
            { name: "pressed", actions: [{ press: "Click or press Enter" }] },
        ],
    },
    { doc: "input/hyperlink-button", page: "HyperlinkButton" },
    { doc: "input/calendar", page: "Calendar" },
    { doc: "input/date-input", page: "DateInput" },
    { doc: "input/date-time-input", page: "DateTimeInput" },
    {
        doc: "input/check-box",
        page: "CheckBox",
        states: [
            {},
            { name: "checked", actions: [{ click: "Toggle with Space" }] },
        ],
    },
    { doc: "input/color-picker", page: "ColorPicker" },
    {
        doc: "input/combo-box",
        page: "ComboBox",
        states: [
            {},
            { name: "open", popup: true, actions: [{ click: "Comfortable" }] },
        ],
    },
    { doc: "input/radio-button", page: "RadioButton" },
    { doc: "input/slider", page: "Slider" },
    {
        doc: "input/text-input",
        page: "TextInput",
        states: [
            {},
            { name: "focused", actions: [{ click: "Edit me" }] },
        ],
    },
    { doc: "input/time-input", page: "TimeInput" },
    { doc: "collections/json-view", page: "JsonView" },
    { doc: "collections/list-view", page: "ListView" },
    { doc: "collections/tab-control", page: "TabControl" },
    { doc: "collections/tree-view", page: "TreeView" },
    { doc: "layout/table", page: "Table" },
    {
        doc: "menus/menu",
        page: "Menu",
        states: [
            {},
            { name: "open", popup: true, actions: [{ click: "File" }] },
        ],
    },
    { doc: "menus/context-menu", page: "ContextMenu" },
    { doc: "navigation/navigation-view", page: "NavigationView" },
    { doc: "layout/dock", page: "Dock" },
    { doc: "layout/expander", page: "Expander" },
    { doc: "layout/grid", page: "Grid" },
    { doc: "layout/group-box", page: "GroupBox" },
    { doc: "layout/overlay", page: "Overlay" },
    { doc: "layout/stack", page: "Stack" },
    { doc: "scrolling/scroll-bar", page: "ScrollBar" },
    { doc: "display/chase-indicator", page: "ChaseIndicator", animated: true },
    { doc: "display/figlet-text", page: "FigletText" },
    { doc: "display/image", page: "Image" },
    { doc: "display/prism", page: "Prism", animated: true },
    { doc: "display/progress-bar", page: "ProgressBar", animated: true },
    { doc: "display/separator", page: "Separator" },
    { doc: "display/spinner", page: "Spinner", animated: true },
    { doc: "display/status-bar", page: "StatusBar", animated: true },
    { doc: "display/text", page: "Text" },
    { doc: "popups/popup", page: "Popup" },
    { doc: "popups/flyout", page: "Flyout" },
    { doc: "popups/tooltip", page: "Tooltip" },
    { doc: "windows/window", page: "Window" },
];
