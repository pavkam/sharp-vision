using System.Collections.ObjectModel;

namespace SharpVision.Showcase;

/// <summary>Owns the stable, exact inventory of concrete controls documented by the showcase.</summary>
internal static class Catalog
{
    /// <summary>Gets one immutable page per concrete shipped control.</summary>
    internal static IReadOnlyList<Page> Pages { get; } = new ReadOnlyCollection<Page>(
    [
        Border(),
        Button(),
        Canvas(),
        CheckBox(),
        Dock(),
        FigletText(),
        Grid(),
        List(),
        Overlay(),
        RadioButton(),
        RichText(),
        ScrollBar(),
        ScrollView(),
        Shadow(),
        Stack(),
        Text(),
        TextInput(),
    ]);

    #region Display controls

    private static Page Border() => new(
        "Border",
        "Frames one owned child with independently enabled physical edges and terminal-safe glyph sets.",
        "Border is display-only. Its child receives focus and input while border geometry participates in layout.",
        [
            P("Child", "Control?", "null", "Owns the single control measured, arranged, and rendered inside the border edges."),
            P("BorderThickness", "Thickness", "0", "Enables each physical edge with a validated thickness of zero or one terminal cell."),
            P("Glyphs", "Glyphs", "Light", "Selects light, heavy, paired, rounded, ASCII, solid, or shaded Unicode border runes."),
            P("BorderColor", "Color?", "inherited", "Overrides the foreground color used only for border cells while preserving child styling."),
            P("Background", "Color?", "inherited", "Fills the complete border box behind both its edges and owned child content."),
        ],
        Examples.Border);

    private static Page FigletText() => new(
        "FigletText",
        "Renders text through a bounded immutable FIGfont while preserving the ordinary control box model.",
        "FigletText is display-only. The showcase editor lets you type a preview and choose an audited catalog font; normal applications set Content, Font, or Options to remeasure safely.",
        [
            P("Content", "string", "empty", "Provides the non-null Unicode source text expanded through the selected FIGfont glyphs."),
            P("Font", "FigletFont", "required", "Selects the immutable parsed font and invalidates measurement whenever it changes."),
            P("Options", "FigletOptions", "font defaults", "Overrides horizontal or vertical layout and left-to-right or right-to-left rendering."),
            P("Foreground", "Color?", "inherited", "Overrides the foreground color of every generated FIGlet output cell."),
        ],
        Examples.FigletText);

    private static Page RichText() => new(
        "RichText",
        "Displays an owned document of styled runs, explicit line breaks, and semantic hyperlinks.",
        "RichText is display-only. Mutating an owned inline updates layout; an inline cannot belong to two documents.",
        [
            P("Inlines", "Inlines", "empty", "Owns the ordered Run, Hyperlink, and LineBreak values that form the displayed document."),
            P("Wrapping", "Wrapping", "Word", "Defaults to word-aware wrapping; applications may preserve logical lines or choose grapheme wrapping explicitly."),
            P("TextAlignment", "Alignment", "Start", "Places every formatted document line at the start, center, or end of its content box."),
            P("Padding", "Thickness", "0", "Adds internal terminal-cell space around the formatted inline document."),
        ],
        Examples.RichText);

    private static Page Shadow() => new(
        "Shadow",
        "Decorates one child with Turbo Vision-style composite darkening or explicit block-glyph overflow.",
        "Shadow is display-only and never captures input outside the child. Clipping degrades safely at viewport edges.",
        [
            P("Child", "Control?", "null", "Owns the single control whose committed cells provide the shadow silhouette."),
            P("Mode", "ShadowMode", "Composite", "Chooses style composition over existing cells or block-glyph drawing in exposed shadow cells."),
            P("Offset", "Point", "(2, 1)", "Moves the visual shadow by signed horizontal and vertical terminal-cell offsets."),
            P("Glyph", "Rune", "▓", "Selects the printable one-cell Rune used by block-glyph shadow mode."),
            P("Attributes", "Attributes?", "Dim", "Overrides rendition attributes applied to shadow cells without changing the child."),
        ],
        Examples.Shadow);

    private static Page Text() => new(
        "Text",
        "Formats Unicode text by grapheme cluster with wrapping, trimming, alignment, and cell-width policy.",
        "Text is display-only. Wide and combining graphemes are never split when wrapping, trimming, or clipping.",
        [
            P("Content", "string", "empty", "Provides the non-null UTF-16 content measured as extended grapheme clusters."),
            P("Wrapping", "Wrapping", "None", "Chooses no wrapping, grapheme wrapping, or word-aware wrapping within available cells."),
            P("Trimming", "Trimming", "None", "Clips or appends an ellipsis when a formatted line cannot fit the content box."),
            P("TextAlignment", "Alignment", "Start", "Places each formatted line at the start, center, or end of its available cells."),
            P("AmbiguousWidth", "Ambiguous", "Narrow", "Controls whether East Asian Ambiguous graphemes occupy one or two terminal cells."),
        ],
        Examples.Text);

    #endregion

    #region Interactive controls

    private static Page Button() => new(
        "Button",
        "Activates one semantic action through keyboard, pointer, programmatic, or command paths.",
        "Press Enter for immediate activation or Space for press-and-release behavior. A primary pointer click focuses and activates once.",
        [
            P("Content", "Control?", "null", "Owns the single visual child used as the button label or richer content."),
            P("Command", "ICommand?", "null", "Queries executable state and runs after the Click event for a completed activation."),
            P("CommandParameter", "object?", "null", "Supplies the borrowed value passed to command availability and execution methods."),
            P("IsDefault", "bool", "false", "Marks the button for an owning Window to use as its Enter fallback action."),
            P("IsCancel", "bool", "false", "Marks the button for an owning Window to use as its Escape fallback action."),
            P("IsEnabled", "bool", "true", "Disables focus, pointer capture, keyboard activation, Click, and command execution when false."),
        ],
        Examples.Button);

    private static Page CheckBox() => new(
        "CheckBox",
        "Toggles an optional label through two-state or three-state selection with explicit events.",
        "Press Space or click the primary pointer button to advance state. Disabled boxes retain state and ignore activation.",
        [
            P("IsChecked", "bool?", "false", "Stores unchecked, checked, or indeterminate state when three-state mode permits null."),
            P("IsThreeState", "bool", "false", "Adds indeterminate to the activation cycle and normalizes null when later disabled."),
            P("Content", "Control?", "null", "Owns the optional label arranged two cells after the one-cell state mark."),
            P("Marks", "Marks", "Unicode defaults", "Selects validated printable one-cell Runes for every check state."),
            P("IsEnabled", "bool", "true", "Prevents focus and state transitions while preserving the current mark when false."),
        ],
        Examples.CheckBox);

    private static Page List() => new(
        "List",
        "Realizes selectable items with keyboard, pointer, activation, and automatic vertical scrolling behavior.",
        "Use arrows, Home, End, Page Up, and Page Down to move; Enter invokes the active item and pointer clicks select rows.",
        [
            P("Items", "IReadOnlyList<object?>", "empty", "Supplies borrowed item values realized through the current item template."),
            P("ItemTemplate", "ItemTemplate", "text", "Creates one fresh control for each realized item and selection state."),
            P("SelectionMode", "SelectionMode", "Single", "Chooses no selection, one selected item, or multiple selected items."),
            P("SelectedIndex", "int", "-1", "Gets or selects the active zero-based item while keeping it visible."),
            P("VerticalOffset", "int", "0", "Reports the first vertically visible item after navigation or pointer scrolling."),
        ],
        Examples.List);

    private static Page RadioButton() => new(
        "RadioButton",
        "Selects one option from an ordinally named group scoped to the attached control root.",
        "Press Space or click to select. Arrow keys move through eligible members of the same group without selecting disabled entries.",
        [
            P("IsChecked", "bool", "false", "Selects this member and atomically clears the previously selected peer."),
            P("GroupName", "string?", "null", "Scopes mutual exclusion by ordinal name within the attached root."),
            P("Content", "Control?", "null", "Owns the optional label arranged after the single-cell radio indicator."),
            P("IsEnabled", "bool", "true", "Excludes the member from focus, pointer activation, and group keyboard navigation when false."),
        ],
        Examples.RadioButton);

    private static Page ScrollBar() => new(
        "ScrollBar",
        "Edits an integer viewport range through buttons, track paging, keyboard commands, and thumb dragging.",
        "Use arrows for SmallChange, Page keys for LargeChange, Home/End for endpoints, or drag the thumb with cell or pixel coordinates.",
        [
            P("Minimum / Maximum", "int", "0 / 100", "Define validated non-negative inclusive range endpoints containing the current value."),
            P("Value", "int", "0", "Stores the current clamped position and raises ValueChanged only after a real change."),
            P("ViewportSize", "int", "0", "Sizes the thumb relative to the visible extent represented by the control."),
            P("Orientation", "Orientation", "Vertical", "Chooses a top-to-bottom or left-to-right range and glyph direction."),
            P("SmallChange / LargeChange", "int", "1 / 10", "Control line-button and page-track movement amounts for keyboard and pointer input."),
        ],
        Examples.ScrollBar);

    private static Page TextInput() => new(
        "TextInput",
        "Edits grapheme-safe single-line or multiline text with selection, undo, masking, and scrolling.",
        "Type text; use arrows with Shift for selection, common copy/cut/undo shortcuts, and Enter to submit unless multiline mode accepts it.",
        [
            P("Text", "string", "empty", "Stores non-null content and keeps the caret and selection on grapheme boundaries."),
            P("IsReadOnly", "bool", "false", "Allows navigation and copying while suppressing edits, cutting, and pasted mutations."),
            P("AcceptsReturn / AcceptsTab", "bool", "false", "Controls whether Enter and Tab insert content instead of submitting or moving focus."),
            P("PasswordCharacter", "Rune?", "null", "Masks each grapheme with one printable cell and suppresses source disclosure through copy."),
            P("MaxLength", "int", "0 (unlimited)", "Limits content by grapheme count while rejecting a value below existing text length."),
            P("SelectionStart / SelectionLength", "int", "0 / 0", "Expose a normalized UTF-16 range whose endpoints must align to grapheme boundaries."),
        ],
        Examples.TextInput);

    #endregion

    #region Layout controls

    private static Page Canvas() => new(
        "Canvas",
        "Positions children with fixed or percentage offsets from physical edges and optional clipping.",
        "Canvas does not handle input itself; hit testing follows positioned child geometry and the ClipToBounds policy.",
        [
            P("Children", "Children", "empty", "Owns positioned controls in stable insertion order for layout, rendering, and hit testing."),
            P("ClipToBounds", "bool", "true", "Clips descendant rendering and hit testing to the committed Canvas content box."),
            P("Left / Top", "Length?", "null", "Attach fixed-cell or percentage offsets from the leading physical edges."),
            P("Right / Bottom", "Length?", "null", "Attach fixed-cell or percentage offsets from trailing edges and resolve deferred sizes."),
            P("Width / Height", "Length", "Auto", "Accept fixed, percentage, automatic, or proportional border-box size requests."),
        ],
        Examples.Canvas);

    private static Page Dock() => new(
        "Dock",
        "Consumes remaining physical edges in child order and optionally gives the final child all remaining space.",
        "Dock is a layout container. Keyboard focus traverses children in the same stable order used for edge consumption.",
        [
            P("Children", "Children", "empty", "Owns controls whose attached Side values consume the remaining rectangle in order."),
            P("LastChildFills", "bool", "true", "Lets the final child fill the remaining content box regardless of its attached side."),
            P("Spacing", "int", "0", "Adds non-negative terminal cells after each consuming edge without overflowing tiny layouts."),
            P("Side", "Side", "Left", "Attaches Left, Top, Right, or Bottom placement to each child."),
        ],
        Examples.Dock);

    private static Page Grid() => new(
        "Grid",
        "Allocates fixed, automatic, percentage, and proportional tracks with exact integer rounding and spans.",
        "Grid is a layout container. Focus follows stable child order rather than visual row or column position.",
        [
            P("Rows / Columns", "TrackCollection", "one implicit Auto", "Define validated track lengths, minimums, and maximums for each axis."),
            P("RowSpacing", "int", "0", "Adds non-negative cells between resolved row tracks while preserving containment."),
            P("ColumnSpacing", "int", "0", "Adds non-negative cells between resolved column tracks while preserving containment."),
            P("Row / Column", "int", "0", "Attach a zero-based starting track to each child."),
            P("RowSpan / ColumnSpan", "int", "1", "Attach positive contiguous spans that contribute intrinsic size across tracks."),
        ],
        Examples.Grid);

    private static Page Overlay() => new(
        "Overlay",
        "Arranges children into one shared content box with stable attached z-order for rendering and hit testing.",
        "Pointer hit testing visits the highest z-index first; equal z-index values retain insertion order.",
        [
            P("Children", "Children", "empty", "Owns layered controls and preserves stable insertion order inside equal z-index groups."),
            P("ClipToBounds", "bool", "true", "Clips layered descendants and pointer hit testing to the Overlay content box."),
            P("ZIndex", "int", "0", "Attaches a signed render and hit-test order to each child without changing ownership order."),
            P("Padding", "Thickness", "0", "Deflates the shared content rectangle before every child is arranged."),
        ],
        Examples.Overlay);

    private static Page ScrollView() => new(
        "ScrollView",
        "Hosts one child in a cell viewport with automatic bars, nested wheel propagation, and bring-into-view.",
        "Use arrows, Page keys, Home/End, wheel input, or its bars. Nested views consume only movement they can apply.",
        [
            P("Content", "Control?", "null", "Owns the single scrollable child measured against the enabled unbounded axes."),
            P("HorizontalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the horizontal bar."),
            P("VerticalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the vertical bar."),
            P("HorizontalOffset / VerticalOffset", "int", "0", "Store validated cell offsets clamped whenever extent or viewport changes."),
            P("LineSize / PageOverlap", "int", "1 / 1", "Control keyboard line movement and retained overlap between page movements."),
        ],
        Examples.ScrollView);

    private static Page Stack() => new(
        "Stack",
        "Arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable spacing.",
        "Stack is a layout container. Reverse changes geometry, rendering, and default focus traversal together.",
        [
            P("Children", "Children", "empty", "Owns the sequential controls whose box requests participate in track allocation."),
            P("Orientation", "Orientation", "Vertical", "Chooses top-to-bottom or left-to-right sequential layout."),
            P("Spacing", "int", "0", "Adds non-negative terminal cells between participating children."),
            P("Reverse", "bool", "false", "Reverses geometry, rendering, hit testing, and default focus traversal consistently."),
            P("Width / Height", "Length", "Auto", "Supports fixed, percentage, automatic, and proportional requests on child border boxes."),
        ],
        Examples.Stack);

    #endregion

    private static PropertyDescription P(
        string name,
        string type,
        string defaultValue,
        string description) => new(name, type, defaultValue, description);
}
