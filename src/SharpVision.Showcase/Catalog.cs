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
        ComboBox(),
        Dock(),
        FigletText(),
        Grid(),
        List(),
        Menu(),
        Overlay(),
        Popup(),
        RadioButton(),
        RichText(),
        ScrollBar(),
        ScrollView(),
        Shadow(),
        Stack(),
        Table(),
        Text(),
        TextInput(),
        Window(),
    ]);

    #region Display controls

    private static Page Border() => new(
        "Border",
        "Frames one owned child with independently enabled physical edges and terminal-safe glyph sets.",
        [
            I("Child ownership", "Assign one detached child", "The child receives the border's measured, arranged, rendered, and input space."),
            I("Rendering", "Change Glyphs or BorderThickness", "The frame redraws with the selected physical edges and terminal-safe runes."),
            I("Resize", "Change the available parent cells", "The child is remeasured inside the committed border edges."),
        ],
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
        [
            I("Content edit", "Change the source text", "FIGfont output is regenerated and measured from the new grapheme content."),
            I("Font selection", "Choose an audited FIGfont", "The preview rebuilds with the selected glyph catalog and its documented metrics."),
            I("Resize", "Change the available cells", "The generated glyphs clip or reflow through the ordinary control box."),
        ],
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
        [
            I("Inline mutation", "Add or edit a Run, Hyperlink, or LineBreak", "The document invalidates and remeasures its formatted content."),
            I("Pointer", "Activate a Hyperlink", "The hyperlink event receives the clicked semantic target."),
            I("Resize", "Change the available width", "Wrapping and line alignment recompute without splitting grapheme clusters."),
        ],
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
        [
            I("Child input", "Interact with the owned child", "The child receives focus and pointer input; the shadow remains passive."),
            I("Mode", "Switch Composite or BlockGlyph", "Exposed shadow cells either darken existing cells or draw the configured glyph."),
            I("Viewport edge", "Move or resize the shadow beyond the canvas", "Only the visible shadow footprint is clipped into the terminal."),
        ],
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
        [
            I("Content edit", "Change Unicode text", "The control measures extended grapheme clusters and preserves combining and wide-cell ownership."),
            I("Resize", "Change the available width", "Wrapping or trimming recomputes while keeping complete grapheme clusters intact."),
            I("Alignment", "Choose Start, Center, or End", "Each formatted line moves within its committed content box."),
        ],
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
        [
            I("Enter", "Press Enter while the button is available", "Click fires once and the command executes when CanExecute permits it."),
            I("Space", "Press and release Space", "The button enters pressed state, then activates on release."),
            I("Pointer", "Press and release the primary pointer inside", "Focus and capture are applied; release inside fires one Click."),
            I("Programmatic", "Call PerformClick", "The same availability and command rules apply without synthesizing terminal input."),
        ],
        [
            P("Content", "Control?", "null", "Owns the single visual child used as the button label or richer content."),
            P("Command", "ICommand?", "null", "Queries executable state and runs after the Click event for a completed activation."),
            P("CommandParameter", "object?", "null", "Supplies the borrowed value passed to command availability and execution methods."),
            P("IsDefault", "bool", "false", "Marks the button for an owning Window to use as its Enter fallback action."),
            P("IsCancel", "bool", "false", "Marks the button for an owning Window to use as its Escape fallback action."),
            P("Glyphs", "Glyphs", "Rounded", "Selects the one-cell border family rendered around the button."),
            P("HasShadow / ShadowOffset", "bool / Point", "true / (1, 1)", "Controls the compact shadow footprint outside the button's interactive surface."),
            P("ShadowMode / ShadowGlyph", "ShadowMode / Rune", "Composite / ▓", "Selects a quiet style-only lift or an explicit Turbo Vision block-glyph shadow."),
            P("IsEnabled", "bool", "true", "Disables focus, pointer capture, keyboard activation, Click, and command execution when false."),
        ],
        Examples.Button);

    private static Page CheckBox() => new(
        "CheckBox",
        "Toggles an optional label through two-state or three-state selection with explicit events.",
        [
            I("Space", "Press and release Space", "The control advances unchecked, checked, and optional indeterminate states."),
            I("Pointer", "Click the primary pointer inside", "Focus moves to the box and one state transition is committed."),
            I("Disabled", "Set IsEnabled to false", "The current state remains visible while keyboard and pointer activation are ignored."),
        ],
        [
            P("IsChecked", "bool?", "false", "Stores unchecked, checked, or indeterminate state when three-state mode permits null."),
            P("IsThreeState", "bool", "false", "Adds indeterminate to the activation cycle and normalizes null when later disabled."),
            P("Content", "Control?", "null", "Owns the optional label after the fixed-width active mark family."),
            P("MarkStyle", "CheckBoxStyle", "Square", "Chooses square, [x] bracket, or Unicode tick marks without label movement."),
            P("Marks", "Marks", "Unicode defaults", "Selects validated printable one-cell Runes for the square mark family."),
            P("IsEnabled", "bool", "true", "Prevents focus and state transitions while preserving the current mark when false."),
        ],
        Examples.CheckBox);

    private static Page List() => new(
        "List",
        "Realizes selectable items with keyboard, pointer, activation, and automatic vertical scrolling behavior.",
        [
            I("Arrows", "Press Up or Down", "Selection moves by one eligible item and keeps the active row visible."),
            I("Paging", "Press Home, End, Page Up, or Page Down", "Selection jumps to the corresponding endpoint or viewport page."),
            I("Enter", "Press Enter on the active item", "ItemInvoked reports the selected value and activation cause."),
            I("Pointer", "Click a row", "The clicked row becomes selected and remains visible."),
        ],
        [
            P("Items", "IReadOnlyList<object?>", "empty", "Supplies borrowed item values realized through the current item template."),
            P("ItemTemplate", "ItemTemplate", "text", "Creates one fresh control for each realized item and selection state."),
            P("SelectionMode", "SelectionMode", "Single", "Chooses no selection, one selected item, or multiple selected items."),
            P("SelectedIndex", "int", "-1", "Gets or selects the active zero-based item while keeping it visible."),
            P("VerticalOffset", "int", "0", "Reports the first vertically visible item after navigation or pointer scrolling."),
            P("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Expose the same enabled-axis and visibility policy used by the canonical ScrollView."),
            P("ScrollBarChrome / ScrollBarFill", "ScrollBarStyle / ScrollBarFill", "Full / Block", "Configure the actual composed rails as thin/full and line/block without a List-only style dialect."),
        ],
        Examples.List);

    private static Page ComboBox() => new(
        "ComboBox",
        "Displays one selected value and opens an owned popup-style List for keyboard or pointer choice.",
        [
            I("Enter or Space", "Open the drop-down", "Focus moves into the owned list and the popup becomes interactive."),
            I("Arrows", "Navigate while open", "The list selection changes without closing the popup."),
            I("Enter", "Commit the active item", "SelectedIndex and SelectionChanged update, then the popup closes."),
            I("Escape", "Dismiss while open", "The popup closes and the previous selection remains unchanged."),
        ],
        [
            P("Items", "IReadOnlyList<object?>", "empty", "Copies borrowed choices into the owned List used by the popup field."),
            P("SelectedIndex", "int", "-1", "Gets or sets the exclusive selected choice while keeping List active navigation synchronized."),
            P("DropDownHeight", "int", "8", "Caps the visible popup list height in non-zero terminal cells."),
            P("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Use the common overflow policy for the popup List; vertical thin rails are ideal for long option sets."),
            P("ScrollBarChrome / ScrollBarFill", "ScrollBarStyle / ScrollBarFill", "Full / Block", "Choose the same thin/full and line/block rail treatment used by Lists and ScrollViews."),
            P("IsOpen", "bool", "false", "Controls list arrangement, rendering, hit testing, and focus transfer into the drop-down."),
            P("SelectionChanged", "event", "null", "Reports List selection commits from direct assignment, pointer, or keyboard activation."),
        ],
        Examples.ComboBox);

    private static Page RadioButton() => new(
        "RadioButton",
        "Selects one option from an ordinally named group scoped to the attached control root.",
        [
            I("Space", "Press and release Space", "This member becomes checked and its checked peer is cleared."),
            I("Pointer", "Click the primary pointer inside", "The member receives focus and selects within its group."),
            I("Arrows", "Navigate among group members", "Focus moves through eligible members without selecting disabled entries."),
        ],
        [
            P("IsChecked", "bool", "false", "Selects this member and atomically clears the previously selected peer."),
            P("GroupName", "string?", "null", "Scopes mutual exclusion by ordinal name within the attached root."),
            P("Content", "Control?", "null", "Owns the optional label arranged after the single-cell radio indicator."),
            P("IsEnabled", "bool", "true", "Excludes the member from focus, pointer activation, and group keyboard navigation when false."),
        ],
        Examples.RadioButton);

    private static Page Menu() => new(
        "Menu",
        "Arranges typed command, check, radio, and separator items with semantic selected state and keyboard navigation.",
        [
            I("Directional arrows", "Move according to Orientation", "The next eligible item becomes selected while separators and disabled items are skipped."),
            I("Enter or Space", "Activate the selected item", "Check or radio state commits before ItemInvoked is raised."),
            I("Pointer", "Click an available item", "The item selects and invokes through the same semantic path as keyboard activation."),
        ],
        [
            P("Items", "MenuItems", "empty", "Owns detached MenuItem controls through a typed collection and tracks each item invocation."),
            P("Orientation", "Orientation", "Horizontal", "Chooses left-to-right or top-to-bottom item geometry and matching directional keyboard navigation."),
            P("Spacing", "int", "1", "Adds non-negative terminal cells between participating menu items."),
            P("SelectedIndex", "int", "-1", "Selects the active non-separator item, applies checked visual state, and optionally moves keyboard focus."),
            P("ItemInvoked", "event", "null", "Reports the activated item and the keyboard, pointer, or programmatic activation cause after state commit."),
        ],
        Examples.Menu);

    private static Page Popup() => new(
        "Popup",
        "Displays one owned child on an opaque bordered surface relative to an optional anchor.",
        [
            I("Enter or pointer", "Activate the owning trigger", "IsOpen becomes true, the child is arranged, and focus enters the popup when possible."),
            I("Arrows and Enter", "Navigate or activate the popup child", "The child handles its own routed interaction without leaking through the overlay."),
            I("Escape", "Press Escape while open", "CloseOnEscape closes the popup and restores focus according to the owner policy."),
            I("Resize", "Move the anchor or change the viewport", "Preferred placement flips and clamps inside the available terminal bounds."),
        ],
        [
            P("Child", "Control?", "null", "Owns the one child inside the popup frame and collapses it while the popup is closed."),
            P("Anchor", "Control?", "null", "Uses an optional sibling control as the anchor rectangle for the preferred placement."),
            P("Placement", "PopupPlacement", "Below", "Chooses below, above, left, or right and flips to the natural opposite side before edge clamping."),
            P("Glyphs / BorderColor", "Glyphs / Color?", "Rounded / inherited", "Controls the physical frame family and optional direct frame color."),
            P("Background", "Color?", "inherited", "Fills the complete popup surface so content behind a drop-down never bleeds through."),
            P("SurfaceBounds", "Rect", "empty", "Reports the committed framed popup rectangle while open."),
            P("IsOpen", "bool", "false", "Controls the framed surface, child arrangement, rendering, hit testing, and focus transfer to a focusable child."),
            P("CloseOnEscape", "bool", "true", "Closes an open popup when Escape bubbles through the owned child."),
            P("Closing / Closed", "event", "null", "Observe dismissal immediately before and after the owned child becomes unavailable."),
        ],
        Examples.Popup);

    private static Page ScrollBar() => new(
        "ScrollBar",
        "Edits an integer viewport range through buttons, track paging, keyboard commands, and thumb dragging.",
        [
            I("Arrows", "Press an arrow button or key", "Value changes by SmallChange and remains clamped to the range."),
            I("Page keys", "Press Page Up or Page Down", "Value changes by LargeChange while preserving the viewport relationship."),
            I("Home or End", "Jump to a range endpoint", "Value becomes Minimum or Maximum through the normal ValueChanged path."),
            I("Pointer drag", "Drag the thumb using cell or pixel coordinates", "The thumb tracks the pointer and capture releases cleanly on completion or cancellation."),
        ],
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
        [
            I("Text entry", "Type printable characters", "Text changes at grapheme boundaries and the caret advances by user-visible clusters."),
            I("Selection", "Use arrows with Shift", "SelectionStart and SelectionLength expand or contract without splitting a grapheme."),
            I("Clipboard and undo", "Use copy, cut, paste, or undo shortcuts", "The edit transaction updates text, selection, and the undo history together."),
            I("Tab and Enter", "Press Tab or Enter", "Focus moves or submission occurs unless AcceptsTab or AcceptsReturn consumes the key."),
            I("Mouse wheel", "Wheel over a multiline editor", "The editor scrolls its own cells first; at an endpoint the next wheel event reaches an enclosing viewport."),
        ],
        [
            P("Text", "string", "empty", "Stores non-null content and keeps the caret and selection on grapheme boundaries."),
            P("IsReadOnly", "bool", "false", "Allows navigation and copying while suppressing edits, cutting, and pasted mutations."),
            P("AcceptsReturn / AcceptsTab", "bool", "false", "Controls whether Enter and Tab insert content instead of submitting or moving focus."),
            P("PasswordCharacter", "Rune?", "null", "Masks each grapheme with one printable cell and suppresses source disclosure through copy."),
            P("MaxLength", "int", "0 (unlimited)", "Limits content by grapheme count while rejecting a value below existing text length."),
            P("SelectionStart / SelectionLength", "int", "0 / 0", "Expose a normalized UTF-16 range whose endpoints must align to grapheme boundaries."),
            P("HorizontalOffset / VerticalOffset", "int", "0 / 0", "Expose the committed cell and logical-line scroll positions used by caret and wheel navigation."),
            P("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Reserve canonical rails for enabled overflowing axes while retaining wheel and caret scrolling when chrome is hidden."),
            P("ScrollBarChrome / ScrollBarFill", "ScrollBarStyle / ScrollBarFill", "Full / Block", "Configure the editor's owned rails with the same thin/full and line/block treatments as every other scrolling host."),
        ],
        Examples.TextInput);

    private static Page Window() => new(
        "Window",
        "Frames one owned child as a titled terminal application surface with optional Turbo Vision-style shadowing.",
        [
            I("Title", "Choose a title placement and Glyphs family", "The title stays inside the frame corners while the selected border set redraws the chrome."),
            I("Enter", "Press Enter when no child handles it", "The first available IsDefault Button activates."),
            I("Escape", "Press Escape when no child handles it", "The first available IsCancel Button activates."),
            I("Resize", "Change the viewport or window bounds", "The child remains inside the frame and the shadow clips safely at the terminal edge."),
        ],
        [
            P("Child", "Control?", "null", "Owns the one control arranged in the framed interior after the one-cell frame edges."),
            P("Title", "string", "empty", "Writes a non-null title into the top edge and safely clips it before either frame corner."),
            P("TitlePlacement", "WindowTitlePlacement", "Left", "Places the safe title at the left, center, or right of the usable top edge."),
            P("Glyphs", "Glyphs", "Rounded", "Selects the Unicode or ASCII-safe physical glyph family used by the complete frame."),
            P("HasShadow", "bool", "true", "Enables the translated visual shadow without changing the content layout rectangle."),
            P("ShadowMode", "ShadowMode", "Composite", "Chooses composite darkening or visible block glyphs for shadow cells outside the window body."),
            P("ShadowOffset", "Point", "(2, 1)", "Moves the optional shadow in signed terminal cells and clips it safely to the terminal viewport."),
        ],
        Examples.Window);

    #endregion

    #region Layout controls

    private static Page Canvas() => new(
        "Canvas",
        "Positions children with fixed or percentage offsets from physical edges and optional clipping.",
        [
            I("Position", "Set fixed or percentage edge attachments", "Children resolve against the committed Canvas content rectangle."),
            I("Pointer", "Target a positioned child", "Hit testing follows the child's final geometry and z-order."),
            I("Resize", "Change the Canvas bounds", "Percentage positions and deferred sizes recompute from the new edges."),
            I("Clipping", "Set ClipToBounds", "Rendering and hit testing either remain inside or may escape the Canvas box."),
        ],
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
        [
            I("Layout", "Attach each child to a Side", "Children consume the remaining rectangle in insertion order."),
            I("Focus", "Move focus with Tab or Shift+Tab", "Focus follows stable child order rather than changing with docked edges."),
            I("Resize", "Change the available bounds", "Edge sizes recompute and the filling child receives the remainder."),
        ],
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
        [
            I("Layout", "Set rows, columns, and spans", "Children receive committed cells from the shared track allocator."),
            I("Resize", "Change the available bounds", "Percentage and proportional tracks resolve from final cells with deterministic rounding."),
            I("Focus", "Move focus with Tab or Shift+Tab", "Traversal follows stable child order, not visual row or column order."),
        ],
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
        [
            I("Pointer", "Target an overlapping child", "The highest ZIndex receives the hit; equal values retain insertion order."),
            I("Rendering", "Change ZIndex", "The child redraws in the new stable layer order without changing ownership."),
            I("Resize", "Change the shared bounds", "Every child is rearranged into the same committed content box."),
        ],
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
        [
            I("Arrows and Page keys", "Move the focused viewport", "Offsets change by LineSize or page distance while remaining clamped."),
            I("Home or End", "Jump to an extent endpoint", "The selected axis offset moves to its minimum or maximum."),
            I("Wheel", "Scroll over nested content", "The nearest view consumes applicable delta and propagates only unused movement."),
            I("Bring into view", "Focus or request a descendant rectangle", "Offsets adjust until the target is visible inside the committed viewport."),
        ],
        [
            P("Content", "Control?", "null", "Owns the single scrollable child measured against the enabled unbounded axes."),
            P("HorizontalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the horizontal bar."),
            P("VerticalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the vertical bar."),
            P("ConstrainContentToViewport", "bool", "false", "Supplies the finite viewport width during measure so word-wrapping reading content reflows instead of expanding horizontally."),
            P("HorizontalOffset / VerticalOffset", "int", "0", "Store validated cell offsets clamped whenever extent or viewport changes."),
            P("LineSize / PageOverlap", "int", "1 / 1", "Control keyboard line movement and retained overlap between page movements."),
        ],
        Examples.ScrollView);

    private static Page Stack() => new(
        "Stack",
        "Arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable spacing.",
        [
            I("Layout", "Set Orientation, lengths, and Spacing", "Children receive deterministic sequential tracks along the selected axis."),
            I("Resize", "Change the available bounds", "Automatic and proportional children recompute without exceeding the stack."),
            I("Reverse", "Set Reverse to true", "Geometry, rendering, hit testing, and default focus traversal reverse together."),
        ],
        [
            P("Children", "Children", "empty", "Owns the sequential controls whose box requests participate in track allocation."),
            P("Orientation", "Orientation", "Vertical", "Chooses top-to-bottom or left-to-right sequential layout."),
            P("Spacing", "int", "0", "Adds non-negative terminal cells between participating children."),
            P("Reverse", "bool", "false", "Reverses geometry, rendering, hit testing, and default focus traversal consistently."),
            P("Width / Height", "Length", "Auto", "Supports fixed, percentage, automatic, and proportional requests on child border boxes."),
        ],
        Examples.Stack);

    private static Page Table() => new(
        "Table",
        "Owns typed rows and column definitions to render aligned rich terminal cells with optional headers and grid lines.",
        [
            I("Columns", "Choose fixed, automatic, percentage, or fill widths", "Each header and row cell shares one resolved track."),
            I("Rows", "Add detached controls matching the column count", "Cells are owned by generated borders while their semantic controls remain interactive."),
            I("Resize", "Change the available table width", "Percentage and fill columns recompute through the shared Grid allocator."),
            I("Pointer and keyboard", "Interact with a focusable cell control", "The cell control receives normal routed input without bypassing the table layout."),
        ],
        [
            P("Columns", "TableColumns", "empty", "Owns non-empty titled fixed, automatic, percentage, or proportional column definitions."),
            P("Rows", "TableRows", "empty", "Owns rows whose detached controls exactly match the defined column count."),
            P("ShowHeader", "bool", "true", "Renders a padded header row from each TableColumn Header value."),
            P("CellPadding", "Thickness", "0", "Deflates every header and data cell with non-negative terminal-cell padding."),
            P("RowSpacing / ColumnSpacing", "int", "0 / 0", "Add non-negative space between cells while preserving contained track geometry."),
            P("ShowGridLines", "bool", "true", "Draws light Unicode lines in available table gaps using the configurable grid-line foreground."),
        ],
        Examples.Table);

    #endregion

    private static PropertyDescription P(
        string name,
        string type,
        string defaultValue,
        string description) => new(name, type, defaultValue, description);

    private static InteractionDescription I(
        string input,
        string behavior,
        string result) => new(input, behavior, result);
}
