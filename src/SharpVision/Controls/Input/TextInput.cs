// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Scrolling;

using SharpVision.Runtime;
using SharpVision.Terminal.Input;

using Terminal.Rendering;

using Text;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using UnicodeWidth = Width;

/// <summary>Defines a focusable grapheme-safe single- or multiline text editor.</summary>
[PublicAPI]
public sealed class TextInput: ControlBase, IClipboardCopySource
{
    private readonly List<EditResult> _undo = [];
    private readonly List<EditResult> _redo = [];
    private bool _coalescing;
    private bool _committingEditSelection;
    private int _coalescingCaret;
    private bool _coalescingWasWhitespace;
    private int _editVersion;
    private string _text = string.Empty;
    private int[]? _boundaryRowCache;
    private int[]? _boundaryColumnCache;
    private string? _boundaryCacheSource;
    private Rune? _boundaryCachePasswordCharacter;
    private UnicodePolicy? _boundaryCacheCellPolicy;
    private int _contentWidth;
    private int _contentHeight = 1;
    private readonly OwnedControlSlot _chrome;
    private readonly ScrollBar _horizontal;
    private readonly ScrollBar _vertical;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private Rect _editorBounds;
    private VisualLine[] _visualLines = [];

    /// <inheritdoc/>
    protected override bool CaptureTextSelectionOnPress => true;

    /// <summary>Initializes an empty focusable single-line editor with a light one-cell border.</summary>
    public TextInput()
    {
        EnableChromeAuthoring();
        _chrome = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "editor-scroll-bars",
                InvalidationImpact.Arrange),
            capacity: 2);
        _horizontal = new ScrollBar { Orientation = Orientation.Horizontal };
        _vertical = new ScrollBar { Orientation = Orientation.Vertical };
        _horizontal.ValueChanged += OnHorizontalChanged;
        _vertical.ValueChanged += OnVerticalChanged;
        _chrome.Add(_horizontal);
        _chrome.Add(_vertical);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ArrangePartDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _horizontal);
        BindStyle(_scrollBarStyle, _vertical);
        IsFocusable = true;
        IsTabStop = true;
        ContextMenu = new TextInputContextMenu(this);
        IsTextSelectionEnabled = true;
    }

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    /// <inheritdoc/>
    /// <summary>Raised before a text mutation and cancellable before commit.</summary>
    public event EventHandler<TextChangingEventArgs>? TextChanging;

    /// <summary>Raised after text and selection commit atomically.</summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    /// <summary>Raised after a changed directional selection commits.</summary>
    public event EventHandler<InputSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when Enter submits a single-line editor.</summary>
    public event EventHandler<SubmittedEventArgs>? Submitted;

    /// <summary>Gets or sets non-null owned text under current return/tab/maximum policy.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value violates policy or maximum length.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var validated = Edit.Replace(
                string.Empty,
                default,
                value,
                MaxLength,
                AcceptsReturn,
                AcceptsTab);

            if (!string.Equals(validated.Text, value, StringComparison.Ordinal))
            {
                throw new ArgumentException("Text exceeds MaxLength.", nameof(value));
            }

            _ = Commit(new EditResult(value, new Selection(value.Length, value.Length), true), true);
        }
    }

    /// <summary>Gets or sets optional placeholder text shown when the input is empty.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? Placeholder
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inboard of the
    /// border and outboard of the caret/selection viewport - it never scrolls with the text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set
        {
            var impact = GetAffixChangeImpact(field, value);

            if (SetProperty(ref field, value, impact))
            {
                ArrangeChrome();
            }
        }
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inboard of the
    /// border and outboard of the caret/selection viewport - it never scrolls with the text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set
        {
            var impact = GetAffixChangeImpact(field, value);

            if (SetProperty(ref field, value, impact))
            {
                ArrangeChrome();
            }
        }
    }

    /// <summary>Gets or sets whether user input may mutate text.</summary>
    /// <remarks>Setting this to true clears the retained undo and redo history, since a
    /// pre-existing entry could otherwise re-commit a state this policy would now refuse to
    /// create by any other route.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsReadOnly
    {
        get;
        set
        {
            var tightened = value && !field;

            if (SetProperty(ref field, value, InvalidationImpact.Render) && tightened)
            {
                ClearHistory();
            }
        }
    }

    /// <summary>Gets or sets whether inserted CR or LF values are accepted.</summary>
    /// <remarks>Clearing this clears the retained undo and redo history, since a pre-existing
    /// entry could otherwise re-commit embedded CR/LF text this policy would now refuse to
    /// create by any other route.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsReturn
    {
        get;
        set
        {
            var tightened = !value && field;

            if (SetProperty(ref field, value, InvalidationImpact.Measure) && tightened)
            {
                ClearHistory();
            }
        }
    }

    /// <summary>Gets or sets whether inserted tab values are accepted.</summary>
    /// <remarks>Clearing this clears the retained undo and redo history, since a pre-existing
    /// entry could otherwise re-commit embedded tab text this policy would now refuse to create
    /// by any other route.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsTab
    {
        get;
        set
        {
            var tightened = !value && field;

            if (SetProperty(ref field, value, InvalidationImpact.Measure) && tightened)
            {
                ClearHistory();
            }
        }
    }

    /// <summary>Gets or sets the optional printable narrow display mask.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune? PasswordCharacter
    {
        get;
        set
        {
            if (value is { } mask)
            {
                _ = Edit.ProjectPassword(string.Empty, mask, CellPolicy.AmbiguousWidth);
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets zero for unlimited or a positive maximum grapheme count.</summary>
    /// <remarks>Lowering this to a smaller positive value, or from unlimited (zero) to any
    /// positive value, clears the retained undo and redo history, since a pre-existing entry
    /// could otherwise re-commit text exceeding this policy by any other route.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The current text exceeds a non-zero value.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int MaxLength
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value > 0 && Edit.GraphemeCount(Text) > value)
            {
                throw new ArgumentException("MaxLength cannot exclude current text.", nameof(value));
            }

            var tightened = value > 0 && (field == 0 || value < field);

            if (SetProperty(ref field, value, InvalidationImpact.None) && tightened)
            {
                ClearHistory();
            }
        }
    }

    /// <summary>Gets or sets the collapsed caret at a grapheme boundary.</summary>
    /// <exception cref="ArgumentException">The value splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value exceeds text.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int CaretIndex
    {
        get => CommittedTextSelection.Caret;
        set => SetSelection(new Selection(value, value));
    }

    /// <summary>Gets or sets the normalized selection start.</summary>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The resulting range exceeds text.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SelectionStart
    {
        get => CommittedTextSelection.Start;
        set => Select(value, SelectionLength);
    }

    /// <summary>Gets or sets the normalized selection length with caret at the range end.</summary>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The resulting range exceeds text.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SelectionLength
    {
        get => CommittedTextSelection.Length;
        set => Select(SelectionStart, value);
    }

    /// <summary>Gets the current horizontal cell offset.</summary>
    public int HorizontalOffset { get; private set; }

    /// <summary>Gets the current vertical line offset.</summary>
    public int VerticalOffset { get; private set; }

    /// <inheritdoc/>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();

        if (PasswordCharacter is not null)
        {
            return new SelectableTextSnapshot(string.Empty, [], isAuthoritative: true);
        }

        if (!EffectiveIsVisible)
        {
            return new SelectableTextSnapshot(Text, [], isAuthoritative: true);
        }

        var glyphs = new List<SelectableTextGlyph>();
        var bounds = _editorBounds == default ? Bounds : _editorBounds.Intersect(Bounds);
        var clip = bounds.Intersect(SelectableTextAggregation.GetEffectiveClip(this));

        if (bounds.Width > 0 && bounds.Height > 0)
        {
            if (WordWrap && _visualLines.Length > 0)
            {
                ProjectWrappedGlyphs(bounds, clip, glyphs);
            }
            else
            {
                ProjectUnwrappedGlyphs(bounds, clip, glyphs);
            }
        }

        return new SelectableTextSnapshot(Text, glyphs, isAuthoritative: true);
    }

    /// <inheritdoc/>
    internal override TextSelectionMap GetTextSelectionMap()
    {
        var glyphs = new List<TextSelectionGlyph>();

        if (WordWrap && _visualLines.Length > 0)
        {
            for (var row = 0; row < _visualLines.Length; row++)
            {
                var line = _visualLines[row];
                var span = Text.AsSpan(line.Offset, line.Length);
                var x = 0;

                foreach (var grapheme in Graphemes.Enumerate(span))
                {
                    var cluster = span.Slice(grapheme.Offset, grapheme.Length);

                    if (IsLineBreak(cluster))
                    {
                        continue;
                    }

                    var width = ClusterWidth(cluster, x);
                    glyphs.Add(new TextSelectionGlyph(
                        new Selection(line.Offset + grapheme.Offset, line.Offset + grapheme.Offset + grapheme.Length),
                        new Rect(x, row, width, 1)));
                    x += width;
                }
            }

            return new TextSelectionMap(Text, [.. glyphs], [], _visualLines.Length);
        }

        var column = 0;
        var visualRow = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                column = 0;
                visualRow++;
                continue;
            }

            var width = ClusterWidth(cluster, column);
            glyphs.Add(new TextSelectionGlyph(
                new Selection(grapheme.Offset, grapheme.Offset + grapheme.Length),
                new Rect(column, visualRow, width, 1)));
            column += width;
        }

        return new TextSelectionMap(Text, [.. glyphs], [], visualRow + 1);
    }

    /// <inheritdoc/>
    protected override int HitTestTextSelectionCore(Point cells)
    {
        var editor = _editorBounds == default ? Bounds : _editorBounds;
        var x = Math.Max(0, cells.X - editor.X + (WordWrap ? 0 : HorizontalOffset));
        var y = Math.Max(0, cells.Y - editor.Y + VerticalOffset);
        return GetTextSelectionMap().HitTest(new Point(x, y));
    }

    /// <inheritdoc/>
    protected override Rect GetTextSelectionAdornmentBounds(Rect bounds)
    {
        var editor = _editorBounds == default ? Bounds : _editorBounds;
        return new Rect(
            editor.X.Add(bounds.X).Add(-(WordWrap ? 0 : HorizontalOffset)),
            editor.Y.Add(bounds.Y).Add(-VerticalOffset),
            bounds.Width,
            bounds.Height);
    }

    /// <summary>Projects visible wrapped source graphemes through the committed visual-line cache.</summary>
    private void ProjectWrappedGlyphs(Rect bounds, Rect clip, List<SelectableTextGlyph> glyphs)
    {
        for (var row = 0; row < _visualLines.Length; row++)
        {
            var screenY = bounds.Y.Add(row).Add(-VerticalOffset);

            if (screenY < bounds.Y || screenY >= bounds.Bottom)
            {
                continue;
            }

            var line = _visualLines[row];
            var span = Text.AsSpan(line.Offset, line.Length);
            var x = 0;

            foreach (var grapheme in Graphemes.Enumerate(span))
            {
                var cluster = span.Slice(grapheme.Offset, grapheme.Length);

                if (IsLineBreak(cluster))
                {
                    continue;
                }

                var width = ClusterWidth(cluster, x);
                AddVisibleGlyph(
                    clip,
                    glyphs,
                    line.Offset + grapheme.Offset,
                    grapheme.Length,
                    bounds.X.Add(x),
                    screenY,
                    width);
                x += width;
            }
        }
    }

    /// <summary>Projects visible unwrapped source graphemes through the committed scroll offsets.</summary>
    private void ProjectUnwrappedGlyphs(Rect bounds, Rect clip, List<SelectableTextGlyph> glyphs)
    {
        var x = 0;
        var y = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                x = 0;
                y++;
                continue;
            }

            var width = ClusterWidth(cluster, x);
            AddVisibleGlyph(
                clip,
                glyphs,
                grapheme.Offset,
                grapheme.Length,
                bounds.X.Add(x).Add(-HorizontalOffset),
                bounds.Y.Add(y).Add(-VerticalOffset),
                width);
            x += width;
        }
    }

    /// <summary>Adds one complete grapheme when all of its rendered cells lie in the editor viewport.</summary>
    private void AddVisibleGlyph(
        Rect clip,
        List<SelectableTextGlyph> glyphs,
        int offset,
        int length,
        int x,
        int y,
        int width)
    {
        var candidate = new Rect(x, y, Math.Max(0, width), 1);

        if (width <= 0 || !SelectableTextAggregation.ContainsCompleteGlyph(clip, candidate))
        {
            return;
        }

        glyphs.Add(new SelectableTextGlyph(
            new Selection(offset, offset + length),
            new Rect(x - Bounds.X, y - Bounds.Y, width, 1)));
    }

    /// <summary>Gets or sets the protocol-neutral cursor shape requested while this editor has focus.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CursorShape CursorShape
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The cursor shape is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the axes eligible for editor overflow scrolling.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfUndefinedFlags(value, ScrollBars.Both, nameof(value), "The scrollbar axes contain unknown flags.");

            if (SetProperty(ref field, value, InvalidationImpact.Arrange))
            {
                ArrangeChrome();
            }
        }
    } = ScrollBars.Both;

    /// <summary>Gets or sets the scrollbar reservation policy for enabled editor axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The scrollbar visibility policy is unknown.");

            if (SetProperty(ref field, value, InvalidationImpact.Arrange))
            {
                ArrangeChrome();
            }
        }
    } = ShowScrollBars.WhenNeeded;

    /// <summary>Gets or sets the complete local style requested for editor rails.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the resolved editor-rail style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>Gets or sets the maximum retained snapshots, applied independently to the undo
    /// stack and the redo stack. Zero disables both retained undo and retained redo.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int UndoLimit
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
            Trim(_undo);
            Trim(_redo);
        }
    } = 100;

    /// <summary>Gets or sets whether long lines are visually wrapped at word boundaries.</summary>
    public bool WordWrap
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets whether one undo snapshot is available.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets whether one redo snapshot is available.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Selects a normalized grapheme-aligned range with caret at its end.</summary>
    /// <param name="start">The UTF-16 start boundary.</param>
    /// <param name="length">The non-negative UTF-16 range length.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range overflows or exceeds text.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void Select([NonNegativeValue] int start, [NonNegativeValue] int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var sum = (long) start + length;

        if (sum > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "The selection range overflows.");
        }

        _coalescing = false;
        SetSelection(new Selection(start, (int) sum));
    }

    /// <summary>Copies selected text unless password policy suppresses source disclosure.</summary>
    /// <returns>An owned selected string, or empty when no selection or password masking is active.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string CopySelection() => CopySelectedText();

    /// <inheritdoc/>
    protected override string GetTextSelectionCopyText()
    {
        VerifyMutable();
        return PasswordCharacter.HasValue ? string.Empty : SelectedText;
    }

    /// <summary>Copies and deletes selection unless read-only or password policy suppresses cutting.</summary>
    /// <returns>The owned copied text, or empty when password masking is active.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string CutSelection()
    {
        var copied = CopySelection();

        if (copied.Length > 0 && !IsReadOnly)
        {
            _ = Commit(Edit.Delete(Text, CommittedTextSelection), true);
        }

        return copied;
    }

    /// <summary>Replaces the current selection, or inserts at the caret when there is none, through
    /// the same edit transaction every other edit path uses.</summary>
    /// <param name="value">The non-null replacement text.</param>
    /// <returns>
    /// True when the edit committed; false when read-only, rejected by policy (a disallowed control
    /// character, or retained length exceeding <see cref="MaxLength"/>), or cancelled by
    /// <see cref="TextChanging"/>.
    /// </returns>
    /// <remarks>
    /// Reuses the control's ordinary validation, <see cref="MaxLength"/> truncation, grapheme-safe
    /// boundaries, undo recording, <see cref="TextChanging"/>/<see cref="TextChanged"/> sequencing,
    /// and scroll repair — the same primitive keyboard input, bracketed paste, context-menu paste,
    /// and cut already route through. This is the composition seam for virtual keyboards, clipboard
    /// adapters, input-method components, and find/replace UI that need to edit content without
    /// reconstructing <see cref="Text"/> externally and bypassing those guarantees.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ReplaceSelection(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        VerifyMutable();
        return Insert(value);
    }

    /// <summary>Inserts application-owned clipboard text through the normal edit transaction.</summary>
    /// <param name="value">The non-null clipboard text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    internal void PasteClipboard(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        VerifyMutable();
        _ = Insert(value);
    }

    /// <summary>Restores the newest retained undo snapshot.</summary>
    /// <returns>True when a snapshot committed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Undo() => Restore(_undo, _redo);

    /// <summary>Restores the newest retained redo snapshot.</summary>
    /// <returns>True when a snapshot committed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Redo() => Restore(_redo, _undo);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        var affixInset = affixes.StartCells + affixes.EndCells;

        if (WordWrap && constraint.Width is { } maxWidth)
        {
            // Deflated by the same reservation ArrangeChrome's own WordWrap branch applies to
            // its already-deflated bounds, so lines wrap against the width the affix columns
            // will actually leave for text instead of the full undeflated constraint.
            var wrapWidth = Math.Max(0, maxWidth - affixInset);
            BuildVisualLines(wrapWidth);
            _contentHeight = _visualLines.Length;
            _contentWidth = 0;

            foreach (var line in _visualLines)
            {
                _contentWidth = Math.Max(_contentWidth, line.Cells);
            }

            return new Size(maxWidth, Math.Max(1, _contentHeight));
        }

        MeasureText(out _contentWidth, out _contentHeight);

        // Reserve one extra cell beyond the text for the end-of-text caret; without
        // it, arrange-time caret-reveal scrolls column 0 out of view for auto-sized inputs.
        // The affix reservation folds in here too - otherwise an auto-sized input under-measures
        // by exactly what ArrangeChrome later deflates for StartAffix/EndAffix, starving the
        // caret/selection viewport of the width this control already promised its affixes.
        return new Size(
            Math.Max(1, _contentWidth + 1).Add(affixInset),
            Math.Max(1, _contentHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        _editorBounds = bounds;
        ArrangeChrome();
        EnsureCaretVisible(_editorBounds);
    }

    /// <inheritdoc/>
    internal override ControlBase? HitTest(Point point)
    {
        return IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled || !Bounds.Contains(point)
            ? null
            : _vertical.HitTest(point) ?? _horizontal.HitTest(point) ?? this;
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        _horizontal.Render(canvas, contentClip);
        _vertical.Render(canvas, contentClip);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        // Rendered against the undeflated content box - not _editorBounds - so an affix keeps
        // drawing even where the caret/selection viewport it deflated has shrunk to nothing.
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        RenderAffixes(canvas, ContentBounds, affixes, StartAffix, EndAffix, ResolvedStyle);

        var bounds = _editorBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        // An editor owns its complete visible surface so configured backgrounds
        // remain continuous beyond short text, selection, and the caret.
        canvas.Clear(bounds, ResolvedStyle);

        if (Text.Length == 0 && !IsFocused && Placeholder is { Length: > 0 } placeholder)
        {
            var placeholderStyle = PlaceholderStyle();
            var px = 0;

            foreach (var grapheme in Graphemes.Enumerate(placeholder))
            {
                var cluster = placeholder.AsSpan(grapheme.Offset, grapheme.Length);

                if (IsLineBreak(cluster))
                {
                    break;
                }

                var width = UnicodeWidth.Measure(cluster, CellPolicy.AmbiguousWidth).Cells;
                var point = new Point(bounds.X.Add(px), bounds.Y);

                if (point.X.Add(width) > bounds.Right)
                {
                    break;
                }

                _ = canvas.Draw(cluster, point, placeholderStyle);
                px += width;
            }

            return;
        }

        // Clipped to the deflated editor viewport, not the wider content box RenderContent
        // received: without this, a scrolled-past-the-window character draws past bounds.Width
        // with nothing to stop it, bleeding into the affix columns the viewport was deflated to
        // avoid - the same viewport-clip idiom Container uses for its own scrollable content.
        var editor = canvas.Clip(bounds);

        if (WordWrap && _visualLines.Length > 0)
        {
            RenderWrapped(editor, bounds);
        }
        else
        {
            RenderUnwrapped(editor, bounds);
        }
    }

    private void RenderWrapped(TerminalCanvas canvas, Rect bounds)
    {
        for (var i = 0; i < _visualLines.Length; i++)
        {
            var screenY = bounds.Y.Add(i).Add(-VerticalOffset);

            if (screenY < bounds.Y || screenY >= bounds.Bottom)
            {
                continue;
            }

            var line = _visualLines[i];
            var span = Text.AsSpan(line.Offset, line.Length);
            var x = 0;

            foreach (var g in Graphemes.Enumerate(span))
            {
                var c = span.Slice(g.Offset, g.Length);

                if (IsLineBreak(c))
                {
                    continue;
                }

                var width = ClusterWidth(c, x);
                var point = new Point(bounds.X.Add(x), screenY);
                var style = ResolvedStyle;

                if (PasswordCharacter is { } mask)
                {
                    Draw(canvas, point, mask, style);
                }
                else if (c.Length == 1 && c[0] == '\t')
                {
                    DrawSpaces(canvas, point, width, style);
                }
                else
                {
                    _ = canvas.Draw(c, point, style);
                }

                x += width;
            }
        }

        if (IsFocused)
        {
            Position(CommittedTextSelection.Caret, out var caretX, out var caretY);
            var position = new Point(
                bounds.X.Add(caretX),
                bounds.Y.Add(caretY).Add(-VerticalOffset));

            if (bounds.Contains(position) && canvas.Bounds.Contains(position))
            {
                canvas.SetCursor(position, visible: true, CursorShape);
            }
        }
    }

    private void RenderUnwrapped(TerminalCanvas canvas, Rect bounds)
    {
        var x = 0;
        var y = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                x = 0;
                y++;
                continue;
            }

            var width = ClusterWidth(cluster, x);
            var point = new Point(
                bounds.X.Add(x).Add(-HorizontalOffset),
                bounds.Y.Add(y).Add(-VerticalOffset));
            var style = ResolvedStyle;

            if (PasswordCharacter is { } mask)
            {
                Draw(canvas, point, mask, style);
            }
            else if (cluster.Length == 1 && cluster[0] == '\t')
            {
                DrawSpaces(canvas, point, width, style);
            }
            else
            {
                _ = canvas.Draw(cluster, point, style);
            }

            x += width;
        }

        if (IsFocused)
        {
            Position(CommittedTextSelection.Caret, out var caretX, out var caretY);
            var position = new Point(
                bounds.X.Add(caretX).Add(-HorizontalOffset),
                bounds.Y.Add(caretY).Add(-VerticalOffset));

            if (bounds.Contains(position) && canvas.Bounds.Contains(position))
            {
                canvas.SetCursor(position, visible: true, CursorShape);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return;
        }

        switch (eventArgs)
        {
            case KeyEventArgs key:
                Handle(key);
                break;
            case TextEventArgs text:
                _ = Insert(text.Text.Value.ToString(), typed: true);
                text.IsHandled = true;
                break;
            case PasteEventArgs paste:
                _ = Insert(Encoding.UTF8.GetString(paste.Paste.Utf8.Span));
                paste.IsHandled = true;
                break;
            case PointerEventArgs pointer:
                Handle(pointer);
                break;
            default:
                break;
        }

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _horizontal.ValueChanged -= OnHorizontalChanged;
            _vertical.ValueChanged -= OnVerticalChanged;
            TextChanging = null;
            TextChanged = null;
            SelectionChanged = null;
            Submitted = null;
            ClearHistory();
        }
    }

    /// <summary>Clears both retained history stacks, so CanUndo/CanRedo report false and neither
    /// Undo() nor Redo() can execute a stale entry. Also ends any active typed-character
    /// coalescing run, since its top-of-stack merge target no longer exists.</summary>
    private void ClearHistory()
    {
        _undo.Clear();
        _redo.Clear();
        _coalescing = false;
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (focused)
        {
            // The caret-reveal chase resumes on focus, but nothing else forces a
            // pass immediately - the next arrange or edit could be arbitrarily far
            // off. Force one now so the caret becomes visible the instant focus
            // lands, matching what a sighted user expects when they tab or click
            // into a field whose caret is currently out of view.
            //
            // A default Rect means ArrangeOverride has never run (construction, or
            // detached from layout), so there is nothing to reveal yet - the
            // upcoming arrange's own EnsureCaretVisible call covers that case. One
            // axis legitimately sitting at 0 post-arrange (for example a scrollbar
            // consuming a viewport's only row) is not the same as never-arranged,
            // so it still gets a chase pass on whichever axis remains usable.
            if (_editorBounds != default)
            {
                EnsureCaretVisible(_editorBounds);
            }

            return;
        }

        // Losing focus does not touch either offset - a caret nobody can see has
        // nothing left to reveal, and resetting would discard a position the user
        // may have reached by wheel-scrolling (which stays focus-independent).
    }

    private bool Commit(EditResult proposal, bool recordHistory, bool coalesce = false)
    {
        VerifyMutable();

        // Every edit that reaches Commit is presumed to break the active typed-character
        // coalescing run - only a matching typed insertion below re-arms it. This runs before
        // either early return so even a same-position no-op commit (a caret move that lands back
        // where it started, for instance) still ends the run: adjacency alone is never enough,
        // any intervening commit is.
        var wasCoalescing = _coalescing;
        var coalescingCaret = _coalescingCaret;
        var coalescingWasWhitespace = _coalescingWasWhitespace;
        _coalescing = false;

        var textChanged = !string.Equals(Text, proposal.Text, StringComparison.Ordinal);

        if (!textChanged && CommittedTextSelection == proposal.Selection)
        {
            return false;
        }

        var observedVersion = _editVersion;

        if (textChanged)
        {
            var changing = new TextChangingEventArgs(proposal);
            TextChanging?.Invoke(this, changing);

            if (changing.Cancel || IsDisposed || _editVersion != observedVersion)
            {
                return false;
            }
        }

        _editVersion++;

        var previousText = Text;
        var previousSelection = CommittedTextSelection;

        if (recordHistory && textChanged)
        {
            var merge = coalesce &&
                TryCoalesce(
                    previousText,
                    previousSelection,
                    proposal,
                    wasCoalescing,
                    coalescingCaret,
                    coalescingWasWhitespace);

            if (!merge)
            {
                Push(_undo, new EditResult(previousText, previousSelection, false));
            }

            _redo.Clear();
        }

        _text = proposal.Text;

        if (textChanged && WordWrap && _editorBounds.Width > 0)
        {
            BuildVisualLines(_editorBounds.Width);
        }

        var selectionChanged = previousSelection != proposal.Selection;

        void PublishTextAndPropertyChanges()
        {
            EnsureCaretVisible(_editorBounds, remeasure: textChanged);

            if (textChanged)
            {
                NotifyPropertyChanged(nameof(Text), InvalidationImpact.Measure);
            }

            if (textChanged)
            {
                TextChanged?.Invoke(this, new TextChangedEventArgs(previousText, Text));
            }
        }

        if (selectionChanged)
        {
            _committingEditSelection = true;

            try
            {
                _ = CommitTextSelectionForAuthoritativeText(
                    proposal.Selection,
                    Text,
                    PublishTextAndPropertyChanges);
            }
            finally
            {
                _committingEditSelection = false;
            }
        }
        else
        {
            PublishTextAndPropertyChanges();
        }

        return true;
    }

    /// <inheritdoc/>
    protected override void OnTextSelectionStateChanged(TextSelectionChangedEventArgs eventArgs)
    {
        _ = eventArgs;
        EstablishTextSelectionCaret();
        if (!_committingEditSelection)
        {
            _coalescing = false;
        }
        NotifyPropertyChanged(nameof(CaretIndex), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectionStart), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectionLength), InvalidationImpact.Render);
    }

    /// <inheritdoc/>
    protected override void OnTextSelectionCommitted(TextSelectionChangedEventArgs eventArgs)
    {
        EnsureCaretVisible(_editorBounds, remeasure: false);
        SelectionChanged?.Invoke(
            this,
            new InputSelectionChangedEventArgs(eventArgs.PreviousSelection, eventArgs.Selection));
    }

    /// <inheritdoc/>
    protected override void RevealTextSelectionCaret(int caret)
    {
        _ = caret;
        EnsureCaretVisible(_editorBounds, remeasure: false);
    }

    private void SetSelection(Selection selection) => SetTextSelection(selection);

    /// <inheritdoc/>
    protected override void CommitTextSelectionNavigation(Selection selection) =>
        _ = CommitTextSelectionForAuthoritativeText(selection, Text);

    /// <inheritdoc/>
    protected override int MoveTextSelectionCaret(Code code, bool extend, bool word)
    {
        var selection = CommittedTextSelection;

        if (code == Code.Left)
        {
            return !extend && !selection.IsEmpty
                ? selection.Start
                : word
                    ? MovePreviousWordFast(selection.Caret)
                    : PreviousBoundaryFast(selection.Caret);
        }

        if (code == Code.Right)
        {
            return !extend && !selection.IsEmpty
                ? selection.End
                : word
                    ? Edit.MoveNextWord(Text, selection, extend).Selection.Caret
                    : NextBoundaryFast(selection.Caret);
        }

        if (code == Code.Home)
        {
            return Edit.MoveHome(Text, selection, extend).Selection.Caret;
        }

        if (code == Code.End)
        {
            return Edit.MoveEnd(Text, selection, extend).Selection.Caret;
        }

        if (code is Code.Up or Code.Down && !WordWrap)
        {
            PositionFast(selection.Caret, out var column, out var row);
            return IndexAtRowFast(Math.Max(0, row + (code == Code.Up ? -1 : 1)), column);
        }

        return base.MoveTextSelectionCaret(code, extend, word);
    }

    /// <summary>Finds the preceding cached grapheme boundary in logarithmic time.</summary>
    private int PreviousBoundaryFast(int index)
    {
        var (offsets, _, _) = BoundaryCache();
        var position = Array.BinarySearch(offsets, index);
        Debug.Assert(position >= 0, "The caret is always a valid cached boundary.");
        return position > 0 ? offsets[position - 1] : 0;
    }

    /// <summary>Finds the following cached grapheme boundary in logarithmic time.</summary>
    private int NextBoundaryFast(int index)
    {
        var (offsets, _, _) = BoundaryCache();
        var position = Array.BinarySearch(offsets, index);
        Debug.Assert(position >= 0, "The caret is always a valid cached boundary.");
        return position < offsets.Length - 1 ? offsets[position + 1] : Text.Length;
    }

    /// <summary>Walks cached grapheme boundaries to the preceding Unicode word start.</summary>
    private int MovePreviousWordFast(int caret)
    {
        var (offsets, _, _) = BoundaryCache();
        var index = Array.BinarySearch(offsets, caret);
        Debug.Assert(index >= 0, "The caret is always a valid cached boundary.");

        while (index > 0 && Edit.Kind(Text, offsets[index - 1]) != 2)
        {
            index--;
        }

        while (index > 0 && Edit.Kind(Text, offsets[index - 1]) == 2)
        {
            index--;
        }

        return offsets[index];
    }

    /// <summary>Finds the nearest cached boundary on one unwrapped logical row.</summary>
    private int IndexAtRowFast(int targetRow, int targetColumn)
    {
        var (offsets, rows, columns) = BoundaryCache();
        var startIndex = LowerBoundByRow(rows, targetRow);

        if (startIndex >= rows.Length || rows[startIndex] != targetRow)
        {
            return Text.Length;
        }

        var lastIndex = startIndex;

        for (var index = startIndex + 1; index < rows.Length && rows[index] == targetRow; index++)
        {
            lastIndex = index;
            var before = columns[index - 1];
            var after = columns[index];

            if (targetColumn < after)
            {
                return targetColumn < before + ((after - before + 1) / 2)
                    ? offsets[index - 1]
                    : offsets[index];
            }
        }

        return offsets[lastIndex];
    }

    /// <summary>Looks up the non-word-wrap caret cell position for <paramref name="index"/> in
    /// O(log n) via the cached boundary/row/column arrays, replacing <see cref="Position"/>'s O(n)
    /// full-prefix rescan on every keystroke.</summary>
    private void PositionFast(int index, out int x, out int y)
    {
        var (offsets, rows, columns) = BoundaryCache();
        var position = Array.BinarySearch(offsets, index);
        Debug.Assert(position >= 0, "The queried index is always a valid cached boundary.");
        x = columns[position];
        y = rows[position];
    }

    /// <summary>Gets the cached grapheme boundary offsets backing <see cref="PositionFast"/>.
    /// Also exposed internally so regression coverage can assert the
    /// same array instance is reused, not rebuilt, across repeated navigation.</summary>
    internal int[]? BoundaryOffsets { get; private set; }

    /// <summary>Gets every grapheme boundary offset in <see cref="Text"/> (including 0 and the
    /// source length) paired with the non-word-wrap cell column and row at that offset, computed
    /// with one forward pass and reused while the source text and every other <see cref="ClusterWidth"/>
    /// input - <see cref="PasswordCharacter"/> and <see cref="ControlBase.CellPolicy"/> - are unchanged.
    /// The <see cref="Text"/> check is a reference comparison, safe because every assignment routes
    /// through <see cref="Commit"/>, which either keeps the same string reference or replaces it
    /// wholesale. Repeatedly committing a selection-only change reuses the same projection instead
    /// of rescanning the full source merely to reposition the caret.</summary>
    [MemberNotNull(nameof(BoundaryOffsets), nameof(_boundaryRowCache), nameof(_boundaryColumnCache))]
    private (int[] Offsets, int[] Rows, int[] Columns) BoundaryCache()
    {
        if (ReferenceEquals(_boundaryCacheSource, Text) &&
            _boundaryCachePasswordCharacter == PasswordCharacter &&
            _boundaryCacheCellPolicy == CellPolicy &&
            BoundaryOffsets is { } cachedOffsets &&
            _boundaryRowCache is { } cachedRows &&
            _boundaryColumnCache is { } cachedColumns)
        {
            return (cachedOffsets, cachedRows, cachedColumns);
        }

        var offsets = new List<int> { 0 };
        var rows = new List<int> { 0 };
        var columns = new List<int> { 0 };
        var x = 0;
        var y = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                x = 0;
                y++;
            }
            else
            {
                x += ClusterWidth(cluster, x);
            }

            offsets.Add(grapheme.Offset + grapheme.Length);
            rows.Add(y);
            columns.Add(x);
        }

        BoundaryOffsets = [.. offsets];
        _boundaryRowCache = [.. rows];
        _boundaryColumnCache = [.. columns];
        _boundaryCacheSource = Text;
        _boundaryCachePasswordCharacter = PasswordCharacter;
        _boundaryCacheCellPolicy = CellPolicy;
        return (BoundaryOffsets, _boundaryRowCache, _boundaryColumnCache);
    }

    private bool Insert(string value, bool typed = false)
    {
        if (IsReadOnly)
        {
            return false;
        }

        EditResult result;

        try
        {
            result = Edit.Replace(
                Text,
                CommittedTextSelection,
                value,
                MaxLength,
                AcceptsReturn,
                AcceptsTab);
        }
        catch (ArgumentException)
        {
            // Terminal text that policy rejects is ignored as one complete input transaction.
            return false;
        }

        // Observer exceptions happen after a valid proposal and must propagate;
        // they are never edit-policy rejections. Only the caller feeding raw typed
        // TextEventArgs characters opts into undo coalescing - paste, Tab, Enter, and the
        // ReplaceSelection/PasteClipboard composition seam all keep recording one entry per edit.
        return Commit(result, true, coalesce: typed);
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (!eventArgs.IsKeyDown)
        {
            return;
        }

        var word = (eventArgs.Stroke.Modifiers & Modifiers.Control) != 0;
        var command = KeyboardModifierPolicy.MatchesCommand(
            eventArgs.Stroke.Modifiers,
            Modifiers.Control);

        if (command && eventArgs.Stroke is { Code: Code.Character, Character: { } character })
        {
            var value = Rune.ToLowerInvariant(character);

            if (value == new Rune('z'))
            {
                if (eventArgs.IsInitialKeyDown)
                {
                    _ = Undo();
                }

                eventArgs.IsHandled = true;
                return;
            }

            if (value == new Rune('y'))
            {
                if (eventArgs.IsInitialKeyDown)
                {
                    _ = Redo();
                }

                eventArgs.IsHandled = true;
                return;
            }
        }

        EditResult? result = null;

        if (eventArgs.Stroke.Code == Code.Backspace && !IsReadOnly)
        {
            result = Edit.Backspace(Text, CommittedTextSelection);
        }
        else if (eventArgs.Stroke.Code == Code.Delete && !IsReadOnly)
        {
            result = Edit.Delete(Text, CommittedTextSelection);
        }

        if (result.HasValue)
        {
            _ = Commit(result.Value, result.Value.Text != Text);
            eventArgs.IsHandled = true;
            return;
        }

        if (eventArgs.Stroke.Code == Code.Enter)
        {
            if (AcceptsReturn && !IsReadOnly)
            {
                _ = Insert("\n");
            }
            else
            {
                if (eventArgs.IsInitialKeyDown)
                {
                    Submitted?.Invoke(this, new SubmittedEventArgs(Text));
                }
            }

            eventArgs.IsHandled = true;
        }
        else if (eventArgs.Stroke.Code == Code.Tab && AcceptsTab && !IsReadOnly)
        {
            _ = Insert("\t");
            eventArgs.IsHandled = true;
        }
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action == PointerAction.Wheel)
        {
            eventArgs.IsHandled = ScrollBy(
                pointer.WheelX,
                pointer.WheelY.Negate());
        }
    }

    private void EnsureCaretVisible(Rect bounds, bool remeasure = true)
    {
        if (WordWrap && _visualLines.Length > 0)
        {
            _contentHeight = _visualLines.Length;

            // The caret-reveal chase only runs while focused - nobody can see an
            // unfocused caret, so there is nothing to chase into view. The offset
            // is still clamped unconditionally below so a shrunken viewport (a
            // resize, or text deleted out from under a wheel-scrolled position)
            // never leaves a stale out-of-range offset behind.
            if (IsFocused)
            {
                Position(CommittedTextSelection.Caret, out _, out var y);
                VerticalOffset = Offset(VerticalOffset, y, bounds.Height, _contentHeight);
            }
            else
            {
                VerticalOffset = ClampOffset(VerticalOffset, bounds.Height, _contentHeight);
            }

            return;
        }

        if (remeasure)
        {
            MeasureText(out _contentWidth, out _contentHeight);
        }

        // Same focus gate as the word-wrap branch above: chase the caret only while
        // focused, otherwise just clamp and cluster-align, never chase. This is
        // what lets an unfocused, never-chased field keep showing its value from
        // the first character, and lets a blurred field keep whatever offset a
        // wheel scroll (which stays focus-independent) last left it at. The
        // unfocused branch still re-snaps to a cluster start below: a wheel scroll
        // never cluster-aligns (ScrollBy/Move are plain arithmetic), and clamping
        // alone can also land mid-cluster when content or the viewport shrinks, so
        // without the snap a double-width glyph could sit half-scrolled off the
        // left edge indefinitely instead of self-healing the way the old
        // unconditional chase always did.
        if (IsFocused)
        {
            Position(CommittedTextSelection.Caret, out var x, out var caretY);

            HorizontalOffset = AlignToClusterStart(
                Offset(HorizontalOffset, x, bounds.Width, _contentWidth),
                caretY);
            VerticalOffset = Offset(VerticalOffset, caretY, bounds.Height, _contentHeight);
        }
        else
        {
            Position(CommittedTextSelection.Caret, out _, out var caretY);

            HorizontalOffset = AlignToClusterStart(
                ClampOffset(HorizontalOffset, bounds.Width, _contentWidth),
                caretY);
            VerticalOffset = ClampOffset(VerticalOffset, bounds.Height, _contentHeight);
        }
    }

    /// <summary>Clamps an offset into the valid scroll range for a given viewport and content
    /// size without chasing any caret position - the unfocused counterpart of <see
    /// cref="Offset(int, int, int, int)"/>. Keeps an offset left behind by a wheel scroll (or any
    /// prior focused chase) in range after the viewport or content size changes, without pulling
    /// it toward a caret nobody can currently see.</summary>
    [Pure]
    private static int ClampOffset(int current, int viewport, int content) =>
        viewport <= 0 ? 0 : Math.Clamp(current, 0, Math.Max(0, content - viewport + 1));

    /// <summary>Snaps a rightward-scrolled horizontal offset down to the start of whichever cluster
    /// it lands inside, on the given row.</summary>
    /// <remarks>
    /// The leftward branch of <see cref="Offset(int, int, int, int)"/> always assigns the caret's own
    /// cell x, which is inherently a cluster start; only the rightward branch's viewport-relative
    /// arithmetic (<c>caret - viewport + 1</c>) can land mid-cluster. Re-snapping unconditionally is
    /// therefore a no-op for every already-aligned value and only changes the one case that needs it.
    /// Looks the target row up in the same cached boundary/row/column arrays
    /// <see cref="PositionFast"/> uses instead of rescanning <see cref="Text"/> from its start on
    /// every call - the prior linear scan cost O(document) per navigation event that calls
    /// <see cref="EnsureCaretVisible"/>, the other half of the O(n^2) total this cache was built to
    /// eliminate.
    /// </remarks>
    private int AlignToClusterStart(int offset, int row)
    {
        if (offset <= 0)
        {
            return offset;
        }

        var (_, rows, columns) = BoundaryCache();

        for (var index = LowerBoundByRow(rows, row); index < rows.Length && rows[index] == row; index++)
        {
            if (index == 0 || rows[index - 1] != row)
            {
                continue;
            }

            var before = columns[index - 1];
            var after = columns[index];

            if (offset > before && offset < after)
            {
                return before;
            }
        }

        return offset;
    }

    /// <summary>Finds the first index whose cached row is at least <paramref name="row"/> via binary
    /// search over the non-decreasing row array, so <see cref="AlignToClusterStart"/> starts scanning
    /// at the target row instead of the document start.</summary>
    [Pure]
    private static int LowerBoundByRow(int[] rows, int row) => LowerBoundByRow(rows, row, out _);

    /// <summary>
    /// Implements <see cref="LowerBoundByRow(int[], int)"/>, additionally reporting how many
    /// comparisons the binary search performed. Returning the count as an ordinary out parameter -
    /// rather than a static or instance counter field - keeps this a ordinary pure function; a test
    /// only needs to call this overload directly to observe it.
    /// </summary>
    /// <param name="rows">The non-decreasing cached row array.</param>
    /// <param name="row">The target row.</param>
    /// <param name="iterations">
    /// Receives the number of comparisons performed. Exposed internally only so a test can prove
    /// this stays logarithmic in <c>rows.Length</c> instead of scanning linearly, without a flaky
    /// wall-clock timing gate.
    /// </param>
    /// <returns>The first index whose cached row is at least <paramref name="row"/>.</returns>
    [Pure]
    internal static int LowerBoundByRow(int[] rows, int row, out int iterations)
    {
        var low = 0;
        var high = rows.Length;
        iterations = 0;

        while (low < high)
        {
            iterations++;
            var mid = low + ((high - low) / 2);

            if (rows[mid] < row)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    /// <summary>Finds the largest <see cref="_visualLines"/> index whose offset does not exceed
    /// <paramref name="textOffset"/> via binary search over the ascending-offset array, replacing
    /// <see cref="Position"/>'s reverse linear scan across every wrapped line.</summary>
    /// <remarks>Requires <c>_visualLines.Length &gt; 0</c>; the first visual line always starts at
    /// offset 0, so <paramref name="textOffset"/> is always found at or after index 0.</remarks>
    private int VisualLineIndexAt(int textOffset)
    {
        var low = 0;
        var high = _visualLines.Length - 1;

        while (low < high)
        {
            var mid = low + ((high - low + 1) / 2);

            if (_visualLines[mid].Offset <= textOffset)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    private bool ScrollBy(int horizontal, int vertical)
    {
        if (WordWrap && _visualLines.Length > 0)
        {
            _contentHeight = _visualLines.Length;
        }
        else
        {
            MeasureText(out _contentWidth, out _contentHeight);
        }

        var bounds = _editorBounds;
        var nextHorizontal = WordWrap
            ? HorizontalOffset
            : Move(HorizontalOffset, horizontal, bounds.Width, _contentWidth);
        var nextVertical = Move(
            VerticalOffset,
            vertical,
            bounds.Height,
            _contentHeight);

        if (nextHorizontal == HorizontalOffset && nextVertical == VerticalOffset)
        {
            return false;
        }

        // Only consume a wheel event while this editor has moved. At an edge,
        // the untouched event continues through bubble routing to an ancestor viewport.
        HorizontalOffset = nextHorizontal;
        VerticalOffset = nextVertical;
        Invalidate(Invalidation.Render);
        return true;
    }

    private void ArrangeChrome()
    {
        // Deflated once, before any scroll/viewport math runs, so the affix columns sit inboard of
        // the border and outboard of the caret/selection viewport - and, because every downstream
        // offset and scrollbar extent below is computed from this already-deflated box, affixes
        // never scroll with the text content.
        var bounds = DeflateForAffixes(
            ContentBounds,
            MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap()));

        if (WordWrap)
        {
            // In word-wrap mode, first determine vertical scrollbar presence to get final viewport width.
            var vertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
                           ShowScrollBars == ShowScrollBars.Always;
            var viewport = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vertical ? 1 : 0)),
                bounds.Height);

            BuildVisualLines(viewport.Width);
            _contentHeight = _visualLines.Length;
            _contentWidth = viewport.Width;

            if (!vertical && ShowScrollBars == ShowScrollBars.WhenNeeded)
            {
                vertical = (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > viewport.Height;
                viewport = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vertical ? 1 : 0)),
                    bounds.Height);

                // Rebuild with potentially narrower width after vertical scrollbar appeared.
                BuildVisualLines(viewport.Width);
                _contentHeight = _visualLines.Length;
                _contentWidth = viewport.Width;
            }

            _editorBounds = viewport;
            _horizontal.Visibility = Visibility.Collapsed;
            _vertical.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
            ArrangeChild(
                _horizontal,
                new Rect(bounds.X, bounds.Y.Add(viewport.Height), viewport.Width, 0),
                ResolvedAxes.Both);
            ArrangeChild(
                _vertical,
                new Rect(bounds.X.Add(viewport.Width), bounds.Y, vertical ? 1 : 0, viewport.Height),
                ResolvedAxes.Both);
            HorizontalOffset = 0;
            Configure(_horizontal, 0, viewport.Width, 0);
            Configure(_vertical, Math.Max(0, _contentHeight - viewport.Height + 1), viewport.Height, VerticalOffset);
            return;
        }

        MeasureText(out _contentWidth, out _contentHeight);
        var horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
                         ShowScrollBars == ShowScrollBars.Always;
        var vert = (ScrollBars & ScrollBars.Vertical) != 0 &&
                   ShowScrollBars == ShowScrollBars.Always;
        var vp = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vert ? 1 : 0)),
            Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));

        if (ShowScrollBars == ShowScrollBars.WhenNeeded)
        {
            horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 && _contentWidth > vp.Width;
            vert = (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > vp.Height;
            vp = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vert ? 1 : 0)),
                Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));
            horizontal |= (ScrollBars & ScrollBars.Horizontal) != 0 && _contentWidth > vp.Width;
            vert |= (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > vp.Height;
            vp = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vert ? 1 : 0)),
                Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));
        }

        _editorBounds = vp;
        _horizontal.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        _vertical.Visibility = vert ? Visibility.Visible : Visibility.Collapsed;
        ArrangeChild(
            _horizontal,
            new Rect(bounds.X, bounds.Y.Add(vp.Height), vp.Width, horizontal ? 1 : 0),
            ResolvedAxes.Both);
        ArrangeChild(
            _vertical,
            new Rect(bounds.X.Add(vp.Width), bounds.Y, vert ? 1 : 0, vp.Height),
            ResolvedAxes.Both);
        Configure(_horizontal, Math.Max(0, _contentWidth - vp.Width + 1), vp.Width, HorizontalOffset);
        Configure(_vertical, Math.Max(0, _contentHeight - vp.Height + 1), vp.Height, VerticalOffset);
    }

    private void OnHorizontalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;
        HorizontalOffset = eventArgs.Value;
        Invalidate(Invalidation.Render);
    }

    private void OnVerticalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;
        VerticalOffset = eventArgs.Value;
        Invalidate(Invalidation.Render);
    }

    private static void Configure(ScrollBar bar, int maximum, int viewport, int value)
    {
        if (bar.Value > maximum)
        {
            bar.Value = maximum;
        }

        bar.Maximum = maximum;
        bar.ViewportSize = viewport;
        bar.LargeChange = viewport;
        bar.Value = Math.Min(value, maximum);
    }

    private void Position(int index, out int x, out int y)
    {
        if (WordWrap && _visualLines.Length > 0)
        {
            y = VisualLineIndexAt(index);
            var line = _visualLines[y];
            x = 0;
            var end = Math.Min(index, line.Offset + line.Length);
            var span = Text.AsSpan(line.Offset, end - line.Offset);

            foreach (var g in Graphemes.Enumerate(span))
            {
                var c = span.Slice(g.Offset, g.Length);

                if (!IsLineBreak(c))
                {
                    x += ClusterWidth(c, x);
                }
            }

            return;
        }

        PositionFast(index, out x, out y);
    }

    private void MeasureText(out int width, out int height)
    {
        var x = 0;
        width = 0;
        height = 1;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                width = Math.Max(width, x);
                x = 0;
                height++;
            }
            else
            {
                x += ClusterWidth(cluster, x);
            }
        }

        width = Math.Max(width, x);
    }

    private void BuildVisualLines(int viewportWidth)
    {
        if (Text.Length == 0)
        {
            _visualLines = [new VisualLine(0, 0, 0)];

            return;
        }

        if (viewportWidth <= 0)
        {
            viewportWidth = int.MaxValue;
        }

        var lines = new List<VisualLine>();
        var lineStart = 0;
        var x = 0;
        var lastBreakOffset = -1;
        var lastBreakCells = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                lines.Add(new VisualLine(lineStart, grapheme.Offset - lineStart, x));
                lineStart = grapheme.Offset + grapheme.Length;
                x = 0;
                lastBreakOffset = -1;
                lastBreakCells = 0;
                continue;
            }

            var width = ClusterWidth(cluster, x);

            // Would this grapheme exceed the viewport?
            if (x + width > viewportWidth && lineStart < grapheme.Offset)
            {
                if (lastBreakOffset > lineStart)
                {
                    lines.Add(new VisualLine(lineStart, lastBreakOffset - lineStart, lastBreakCells));
                    lineStart = lastBreakOffset;
                }
                else
                {
                    lines.Add(new VisualLine(lineStart, grapheme.Offset - lineStart, x));
                    lineStart = grapheme.Offset;
                }

                x = 0;
                lastBreakOffset = -1;
                lastBreakCells = 0;
                var remaining = Text.AsSpan(lineStart, grapheme.Offset + grapheme.Length - lineStart);

                foreach (var g in Graphemes.Enumerate(remaining))
                {
                    var c = remaining.Slice(g.Offset, g.Length);

                    if (!IsLineBreak(c))
                    {
                        var w = ClusterWidth(c, x);

                        if (IsWordBreak(c))
                        {
                            lastBreakOffset = lineStart + g.Offset + g.Length;
                            lastBreakCells = x + w;
                        }

                        x += w;
                    }
                }

                continue;
            }

            // Track whitespace as potential break point (after the whitespace).
            if (IsWordBreak(cluster))
            {
                lastBreakOffset = grapheme.Offset + grapheme.Length;
                lastBreakCells = x + width;
            }

            x += width;
        }

        lines.Add(new VisualLine(lineStart, Text.Length - lineStart, x));
        _visualLines = [.. lines];
    }

    private static bool IsWordBreak(ReadOnlySpan<char> cluster) =>
        cluster.Length == 1 && char.IsWhiteSpace(cluster[0]) && cluster[0] is not ('\r' or '\n');

    private int ClusterWidth(ReadOnlySpan<char> cluster, int x)
    {
        return PasswordCharacter is { } mask
            ? PasswordWidth(mask)
            : cluster.Length == 1 && cluster[0] == '\t'
                ? 4 - (x % 4)
                : UnicodeWidth.Measure(cluster, CellPolicy.AmbiguousWidth).Cells;
    }

    private int PasswordWidth(Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        return UnicodeWidth.Measure(buffer[..length], CellPolicy.AmbiguousWidth).Cells;
    }

    /// <inheritdoc/>
    protected override TerminalStyle ApplyTextSelectionStyle(TerminalStyle current) => new(
        current.Foreground,
        current.Background,
        current.Attributes | TerminalAttributes.Reverse,
        current.Hyperlink,
        current.Underline,
        current.UnderlineColor);

    private TerminalStyle PlaceholderStyle()
    {
        var style = ResolvedStyle;
        return new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Dim,
            style.Hyperlink,
            style.Underline,
            style.UnderlineColor);
    }

    private static bool IsLineBreak(ReadOnlySpan<char> cluster) =>
        cluster.IndexOfAny('\r', '\n') >= 0;

    private static int Offset(int current, int caret, int viewport, int content)
    {
        if (viewport <= 0)
        {
            return 0;
        }

        var next = caret < current
            ? caret
            : caret >= current + viewport
                ? caret - viewport + 1
                : current;
        return Math.Clamp(next, 0, Math.Max(0, content - viewport + 1));
    }

    private static int Move(int current, int delta, int viewport, int content)
    {
        if (viewport <= 0)
        {
            return 0;
        }

        var next = (int) Math.Clamp((long) current + delta, 0, int.MaxValue);
        return Math.Clamp(next, 0, Math.Max(0, content - viewport + 1));
    }

    private static void Draw(
        TerminalCanvas canvas,
        Point point,
        Rune rune,
        TerminalStyle style)
    {
        Span<char> buffer = stackalloc char[2];
        var length = rune.EncodeToUtf16(buffer);
        _ = canvas.Draw(buffer[..length], point, style);
    }

    private static void DrawSpaces(
        TerminalCanvas canvas,
        Point point,
        int count,
        TerminalStyle style)
    {
        for (var index = 0; index < count; index++)
        {
            _ = canvas.Draw(" ", new Point(point.X.Add(index), point.Y), style);
        }
    }

    private bool Restore(List<EditResult> source, List<EditResult> destination)
    {
        VerifyMutable();

        // Restore is the only edit path that commits a stored snapshot without going through
        // Edit.Replace, so it is the only one that can re-commit a state a currently tightened
        // policy (IsReadOnly, MaxLength, AcceptsReturn, AcceptsTab) would refuse to create by
        // any other route. Snapshots are validated against the policy in force when
        // recorded, then re-committed under whatever policy is in force later; without this
        // check, tightening a policy after edits exist is retroactively bypassable by one undo.
        if (IsReadOnly || source.Count == 0)
        {
            return false;
        }

        var snapshot = source[^1];

        if (!IsPermittedByPolicy(snapshot.Text))
        {
            return false;
        }

        var current = new EditResult(Text, CommittedTextSelection, false);

        if (!Commit(new EditResult(snapshot.Text, snapshot.Selection, true), false))
        {
            return false;
        }

        source.RemoveAt(source.Count - 1);
        Push(destination, current);
        return true;
    }

    /// <summary>Determines whether a text value survives the current editing policy unchanged,
    /// reusing the same validator the Text setter applies rather than duplicating policy logic.</summary>
    private bool IsPermittedByPolicy(string text)
    {
        var validated = Edit.Replace(string.Empty, default, text, MaxLength, AcceptsReturn, AcceptsTab);
        return string.Equals(validated.Text, text, StringComparison.Ordinal);
    }

    /// <summary>Decides whether a typed single-character insertion continues the active undo
    /// coalescing run, and unconditionally re-arms the run's tracked state for whichever typed
    /// insertion comes next - including this one, when it turns out to be the first character of
    /// a fresh run rather than a continuation.</summary>
    /// <param name="previousText">The committed text immediately before this edit.</param>
    /// <param name="previousSelection">The committed selection immediately before this edit.</param>
    /// <param name="proposal">The proposed post-edit text and selection.</param>
    /// <param name="wasCoalescing">Whether a run was active going into this edit.</param>
    /// <param name="coalescingCaret">The caret position the active run last left off at.</param>
    /// <param name="coalescingWasWhitespace">The active run's whitespace classification.</param>
    /// <returns>
    /// True when this edit merges into the current top-of-undo-stack entry, so the caller must
    /// skip <see cref="Push"/>; false when it does not, either because it fails the coalescing
    /// rule entirely or because it is the first character of a new run.
    /// </returns>
    private bool TryCoalesce(
        string previousText,
        Selection previousSelection,
        EditResult proposal,
        bool wasCoalescing,
        int coalescingCaret,
        bool coalescingWasWhitespace)
    {
        // Overtyping a selection - or landing on a non-collapsed selection for any other reason -
        // always starts a fresh entry and never joins (or seeds) a run.
        if (!previousSelection.IsEmpty || !proposal.Selection.IsEmpty)
        {
            return false;
        }

        var start = previousSelection.Caret;
        var end = proposal.Selection.Caret;
        var insertedLength = end - start;

        if (insertedLength <= 0 || start > previousText.Length || end > proposal.Text.Length)
        {
            return false;
        }

        // The edit must be a pure insertion at the prior caret position: everything before it and
        // everything after it carries over unchanged. This also rejects a truncated insertion (an
        // empty or partial replacement policy or MaxLength would otherwise allow through).
        if (!proposal.Text.AsSpan(0, start).SequenceEqual(previousText.AsSpan(0, start)) ||
            !proposal.Text.AsSpan(end).SequenceEqual(previousText.AsSpan(start)))
        {
            return false;
        }

        var insertedSpan = proposal.Text.AsSpan(start, insertedLength);
        var enumerator = Graphemes.Enumerate(insertedSpan).GetEnumerator();

        // Exactly one grapheme cluster - the whole inserted span, not a prefix of it - must have
        // landed verbatim, reusing the same allocation-free segmentation the rest of the file uses
        // rather than any ad-hoc Unicode logic.
        if (!enumerator.MoveNext() || enumerator.Current.Length != insertedLength)
        {
            return false;
        }

        var status = Rune.DecodeFromUtf16(insertedSpan, out var rune, out _);
        var isWhitespace = status == OperationStatus.Done && Rune.IsWhiteSpace(rune);

        // This insertion is a valid single typed character. It re-arms the run for whatever comes
        // next regardless of whether it merges into an existing one, so a rejected first character
        // still seeds a run the next matching character can join.
        _coalescing = true;
        _coalescingCaret = proposal.Selection.Caret;
        _coalescingWasWhitespace = isWhitespace;

        // Merging additionally requires an already-active run whose last character left off
        // exactly where this one starts (pure adjacency - any intervening edit already cleared
        // wasCoalescing above), with a matching whitespace classification, and an actual
        // top-of-stack entry to merge into (UndoLimit could be zero, recording nothing).
        return wasCoalescing &&
            coalescingCaret == start &&
            coalescingWasWhitespace == isWhitespace &&
            _undo.Count > 0;
    }

    private void Push(List<EditResult> history, EditResult value)
    {
        if (UndoLimit == 0)
        {
            return;
        }

        history.Add(value);
        Trim(history);
    }

    private void Trim(List<EditResult> history)
    {
        var remove = history.Count - UndoLimit;

        if (remove > 0)
        {
            history.RemoveRange(0, remove);
        }
    }

    private readonly record struct VisualLine(int Offset, int Length, int Cells);
}
