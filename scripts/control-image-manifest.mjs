/// Each entry maps one showcase page to one control document. `doc` is the
/// path under docs/controls without extension; the image slug is its basename.
/// A state without a name is the default capture. `actions` run before the
/// capture; `press` followed by `drag` holds a real primary-button selection
/// gesture, `example` selects a DocExample by visible occurrence or marker,
/// `popup: true` widens the crop to rows the actions changed, and `animated:
/// true` skips the stable-frame wait for continuously moving pages.
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
    { doc: "input/number-input", page: "NumberInput" },
    { doc: "input/currency-input", page: "CurrencyInput" },
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
        doc: "input/command-bar",
        page: "CommandBar",
        states: [
            {},
            {
                name: "open",
                popup: true,
                actions: [{ click: " …" }],
            },
        ],
    },
    { doc: "input/command-bar-item", page: "CommandBarItem" },
    { doc: "input/command-bar-separator", page: "CommandBarSeparator" },
    {
        doc: "input/command-palette",
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
        doc: "input/suggestion-input",
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
        doc: "input/text-input",
        page: "TextInput",
        states: [{}, { name: "focused", actions: [{ click: "Edit me" }] }],
    },
    { doc: "input/time-input", page: "TimeInput" },
    {
        doc: "collections/document",
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
    { doc: "collections/json-view", page: "JsonView" },
    { doc: "display/code-view", page: "CodeView" },
    { doc: "collections/list-view", page: "ListView" },
    { doc: "collections/tab-control", page: "TabControl" },
    { doc: "collections/tree-view", page: "TreeView" },
    { doc: "layout/table", page: "Table" },
    { doc: "charts/horizontal-bar-chart", page: "HorizontalBarChart" },
    { doc: "charts/vertical-bar-chart", page: "VerticalBarChart" },
    { doc: "charts/line-chart", page: "LineChart" },
    { doc: "charts/area-chart", page: "AreaChart" },
    { doc: "charts/sparkline", page: "Sparkline" },
    {
        doc: "menus/menu",
        page: "Menu",
        states: [
            {},
            { name: "open", popup: true, actions: [{ click: "File" }] },
        ],
    },
    {
        doc: "menus/menu-item",
        page: "MenuItem",
        states: [{ actions: [{ click: "More options" }] }],
    },
    { doc: "menus/context-menu", page: "ContextMenu" },
    {
        doc: "navigation/breadcrumb",
        page: "Breadcrumb",
        states: [
            {},
            { name: "overflow", actions: [{ click: "Narrow path" }] },
        ],
    },
    { doc: "navigation/breadcrumb-item", page: "BreadcrumbItem" },
    { doc: "navigation/navigation-view", page: "NavigationView" },
    {
        doc: "navigation/pager",
        page: "Pager",
        states: [{}, { name: "narrow", example: 2 }],
    },
    { doc: "layout/dock", page: "Dock" },
    { doc: "layout/expander", page: "Expander" },
    { doc: "layout/grid", page: "Grid" },
    { doc: "layout/group-box", page: "GroupBox" },
    { doc: "layout/overlay", page: "Overlay" },
    { doc: "layout/split-pane", page: "SplitPane" },
    { doc: "layout/stack", page: "Stack" },
    { doc: "layout/wrap", page: "Wrap" },
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
    {
        doc: "notifications/info-bar",
        page: "InfoBar",
        states: [{ example: "Deployment requires attention" }],
    },
    {
        doc: "notifications/toast",
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
    { doc: "windows/window", page: "Window" },
];
