/// Each entry maps one showcase page to one concrete control or dialog document. `doc` is the
/// full path under docs without extension; the image slug is its basename.
/// A state without a name is the default capture. `actions` run before the
/// capture; `press` followed by `drag` holds a real primary-button selection
/// gesture, `example` selects a DocExample by visible occurrence or marker,
/// `popup: true` widens the crop to rows the actions changed, and `animated:
/// true` skips the stable-frame wait for continuously moving pages.
export const controls = [
    {
        doc: "concepts/styling",
        page: "Styling",
        imageDirectory: "concepts",
        states: [
            { name: "palette", example: "Complete semantic palette" },
            {
                name: "states",
                example: "Live element states",
                actions: [{ click: "Selected row" }],
            },
            {
                name: "states-focused",
                example: "Live element states",
                actions: [{ click: "Focus target" }],
            },
            {
                name: "states-pressed",
                example: "Live element states",
                actions: [{ press: "Press target" }],
            },
        ],
    },
    { doc: "controls/control", page: "Control" },
    {
        doc: "controls/input/button",
        page: "Button",
        states: [
            {},
            { name: "pressed", actions: [{ press: "Click or press Enter" }] },
        ],
    },
    { doc: "controls/input/hyperlink-button", page: "HyperlinkButton" },
    { doc: "controls/input/calendar", page: "Calendar" },
    { doc: "controls/input/date-input", page: "DateInput" },
    { doc: "controls/input/date-time-input", page: "DateTimeInput" },
    { doc: "controls/input/number-input", page: "NumberInput" },
    { doc: "controls/input/currency-input", page: "CurrencyInput" },
    {
        doc: "controls/input/check-box",
        page: "CheckBox",
        states: [
            {},
            { name: "checked", actions: [{ click: "Toggle with Space" }] },
        ],
    },
    { doc: "controls/input/color-picker", page: "ColorPicker" },
    {
        doc: "controls/input/command-bar",
        page: "CommandBar",
        states: [
            { example: "Narrow bar" },
            {
                name: "open",
                example: "Narrow bar",
                popup: true,
                actions: [{ click: " …" }],
            },
        ],
    },
    {
        doc: "controls/input/command-bar-item",
        page: "CommandBar",
        states: [{ example: "Invoke Deploy" }],
    },
    {
        doc: "controls/input/command-bar-separator",
        page: "CommandBar",
        states: [{ example: "Cycle glyph" }],
    },
    {
        doc: "controls/input/command-palette",
        page: "CommandPalette",
        states: [
            {},
            {
                name: "centered",
                example: "Open centered",
                popup: true,
                actions: [
                    { click: "Open centered" },
                    { type: "s" },
                    { key: "Home" },
                ],
            },
            {
                name: "top-centered",
                example: "Open centered",
                popup: true,
                actions: [{ click: "Open at top" }],
            },
        ],
    },
    {
        doc: "controls/input/combo-box",
        page: "ComboBox",
        states: [
            {},
            { name: "open", popup: true, actions: [{ click: "Comfortable" }] },
        ],
    },
    { doc: "controls/input/radio-button", page: "RadioButton" },
    { doc: "controls/input/slider", page: "Slider" },
    {
        doc: "controls/input/suggestion-input",
        page: "SuggestionInput",
        states: [
            {},
            {
                name: "open",
                // The pane's bounded stage contains the complete suggestion popup. Keeping the
                // ordinary example crop avoids unrelated focus or page-scroll changes expanding
                // the image to the whole Gallery.
                actions: [
                    { click: "Search destinations…" },
                    { type: "li" },
                    { wait: 800 },
                ],
            },
        ],
    },
    {
        doc: "controls/input/text-input",
        page: "TextInput",
        states: [{}, { name: "focused", actions: [{ click: "Edit me" }] }],
    },
    { doc: "controls/input/time-input", page: "TimeInput" },
    {
        doc: "controls/collections/document",
        page: "Document",
        states: [
            {
                actions: [
                    { press: "Drag from this" },
                    { drag: "var selected = document.SelectedText;" },
                ],
            },
        ],
    },
    { doc: "controls/collections/json-view", page: "JsonView" },
    { doc: "controls/display/code-view", page: "CodeView" },
    { doc: "controls/collections/list-view", page: "ListView" },
    { doc: "controls/collections/tab-control", page: "TabControl" },
    { doc: "controls/collections/tree-view", page: "TreeView" },
    { doc: "controls/layout/table", page: "Table" },
    { doc: "controls/charts/horizontal-bar-chart", page: "HorizontalBarChart" },
    { doc: "controls/charts/vertical-bar-chart", page: "VerticalBarChart" },
    { doc: "controls/charts/line-chart", page: "LineChart" },
    { doc: "controls/charts/area-chart", page: "AreaChart" },
    { doc: "controls/charts/sparkline", page: "Sparkline" },
    {
        doc: "controls/menus/menu",
        page: "Menu",
        states: [
            {},
            {
                name: "open",
                popup: true,
                cropPadding: { right: 1 },
                actions: [{ click: "File" }],
            },
        ],
    },
    {
        doc: "controls/menus/menu-item",
        page: "Menu",
        states: [
            {
                example: "More options",
                popup: true,
                actions: [{ click: "More options" }],
            },
        ],
    },
    {
        doc: "controls/menus/context-menu",
        page: "ContextMenu",
        states: [
            {
                actions: [{ secondaryClick: "Right-click me" }],
            },
        ],
    },
    {
        doc: "controls/navigation/breadcrumb",
        page: "Breadcrumb",
        states: [
            { example: "Clear current" },
            {
                name: "overflow",
                example: "Clear current",
                actions: [{ click: "Narrow path" }],
            },
            {
                name: "overflow-open",
                example: "Widen path",
                popup: true,
                actions: [{ click: " …" }],
            },
            { name: "spacing", example: "Spacing: before 2" },
        ],
    },
    {
        doc: "controls/navigation/breadcrumb-item",
        page: "Breadcrumb",
        states: [{ example: "Invoke Design" }],
    },
    { doc: "controls/navigation/navigation-view", page: "NavigationView" },
    {
        doc: "controls/navigation/pager",
        page: "Pager",
        states: [{}, { name: "narrow", example: 2 }],
    },
    { doc: "controls/layout/dock", page: "Dock" },
    { doc: "controls/layout/expander", page: "Expander" },
    { doc: "controls/layout/grid", page: "Grid" },
    { doc: "controls/layout/group-box", page: "GroupBox" },
    { doc: "controls/layout/overlay", page: "Overlay" },
    { doc: "controls/layout/split-pane", page: "SplitPane" },
    { doc: "controls/layout/stack", page: "Stack" },
    {
        doc: "controls/layout/wrap",
        page: "Wrap",
        states: [
            {},
            {
                name: "horizontal-reflow",
                example: "Narrow rows",
                actions: [{ click: "Narrow rows" }],
            },
            {
                name: "vertical-reflow",
                example: "Shorten columns",
                actions: [{ click: "Shorten columns" }],
            },
        ],
    },
    { doc: "controls/scrolling/scroll-bar", page: "ScrollBar" },
    { doc: "controls/display/chase-indicator", page: "ChaseIndicator", animated: true },
    { doc: "controls/display/figlet-text", page: "FigletText" },
    { doc: "controls/display/image", page: "Image" },
    { doc: "controls/display/prism", page: "Prism", animated: true },
    { doc: "controls/display/progress-bar", page: "ProgressBar", animated: true },
    { doc: "controls/display/separator", page: "Separator" },
    { doc: "controls/display/spinner", page: "Spinner", animated: true },
    { doc: "controls/display/status-bar", page: "StatusBar", animated: true },
    { doc: "controls/display/text", page: "Text" },
    {
        doc: "controls/popups/popup",
        page: "Popup",
        states: [{ actions: [{ click: "Actions" }] }],
    },
    { doc: "controls/popups/flyout", page: "Flyout" },
    { doc: "controls/popups/tooltip", page: "Tooltip" },
    {
        doc: "controls/notifications/info-bar",
        page: "InfoBar",
        states: [{ example: "Allow close" }],
    },
    {
        doc: "controls/notifications/toast",
        page: "Toast",
        states: [
            { popup: true, actions: [{ click: "Show toast" }] },
            {
                name: "error",
                example: "Show error",
                popup: true,
                actions: [{ click: "Show error" }],
            },
        ],
    },
    {
        doc: "controls/windows/window",
        page: "Window",
        states: [
            {},
            {
                name: "default-action",
                example: "Focus command target",
                actions: [{ click: "Focus command target" }, { key: "Enter" }],
            },
        ],
    },
    {
        doc: "dialogs/message-box",
        page: "MessageBox",
        states: [
            {
                example: "Yes / No",
                popup: true,
                actions: [{ click: "Yes / No" }],
            },
        ],
    },
    {
        doc: "dialogs/file-picker-dialog",
        page: "OpenFilePicker",
        states: [
            {
                example: "Open one file",
                popup: true,
                actions: [{ click: "Open one file" }],
            },
        ],
    },
    {
        doc: "dialogs/save-file-dialog",
        page: "SaveFilePicker",
        states: [
            {
                example: "Overwrite report",
                popup: true,
                actions: [{ click: "Overwrite report" }],
            },
        ],
    },
];
