// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies TextInput validation, editing, events, input, rendering, and history.</summary>
public sealed class TextInputTests
{
    /// <summary>Verifies the shared affix-gap resolver registers its layout dependency rather than
    /// relying on each consuming input to duplicate Theme comparison plumbing.</summary>
    [Fact]
    public void SetTheme_WhenResolvedAffixGapChanges_InvalidatesMeasure()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create(inputExtra: ", \"affixGap\": 1"));
        var current = ThemeCatalog.Parse(ThemeJson.Create(inputExtra: ", \"affixGap\": 3"));
        var input = new TextInput { Text = "value", StartAffix = new Affix("!") };
        input.SetTheme(previous);
        new LayoutEngine().Layout(input, new Size(20, 1));
        input.Clear(Invalidation.All);

        input.SetTheme(current);

        input.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies editable input selection is exposed through the common non-mutating text-selection contract.</summary>
    [Fact]
    public void TextSelection_WhenAccessedThroughControlBase_UsesInputSelectionState()
    {
        // Arrange
        var input = new TextInput { Text = "Alpha Beta" };
        ControlBase control = input;

        // Act
        control.SetTextSelection(new Selection(6, 10));

        // Assert
        control.IsTextSelectionEnabled.ShouldBeTrue();
        control.TextSelection.ShouldBe(new Selection(6, 10));
        control.SelectedText.ShouldBe("Beta");
        control.CopySelectedText().ShouldBe("Beta");
    }

    /// <summary>Verifies the common copy surface preserves password-disclosure policy.</summary>
    [Fact]
    public void CopySelectedText_WhenInputIsPasswordMasked_ReturnsEmpty()
    {
        // Arrange
        ControlBase control = new TextInput
        {
            Text = "secret",
            PasswordCharacter = new Rune('*')
        };
        control.SetTextSelection(new Selection(0, 6));

        // Act
        var copied = control.CopySelectedText();

        // Assert
        copied.ShouldBeEmpty();
    }

    /// <summary>Verifies a text field is discoverable through light intrinsic chrome by default.</summary>
    [Fact]
    public void Properties_WhenConstructed_UsesLightFieldBorder()
    {
        // Arrange
        var control = new TextInput();

        // Act

        // Assert
        control.ActualBorder.Sides.ShouldBe(BorderSide.All);
        control.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies the chrome-authoring surface stays sealed to TextInput - only the
    /// well-known chrome/container primitives call EnableChromeAuthoring, and Border/Shadow flow
    /// entirely through the themed Style/ActualStyle pair like every other leaf input control.</summary>
    [Fact]
    public void Constructor_WhenCreated_ExposesThemedStyleInsteadOfRawChromeAuthoring()
    {
        // Arrange
        using var control = new TextInput();

        // Act

        // Assert
        control.Style.ShouldBeNull();
        control.ActualStyle.AffixGap.ShouldBe(TextInputStyle.Default.AffixGap);
        control.ActualStyle.DropDownGlyph.ShouldBe(TextInputStyle.Default.DropDownGlyph);
        control.ActualStyle.Border.Sides.ShouldBe(TextInputStyle.Default.Border.Sides);
        control.ActualStyle.Border.GlyphStyle.ShouldBe(TextInputStyle.Default.Border.GlyphStyle);
        _ = Should.Throw<InvalidOperationException>(() => control.Border);
        _ = Should.Throw<InvalidOperationException>(() => control.Shadow);
    }

    /// <summary>Verifies a local style assignment round-trips through Style/ActualStyle and is
    /// reflected by the resolved chrome the rest of the control renders from.</summary>
    [Fact]
    public void Style_WhenLocalValueIsAssigned_RoundTripsAndDrivesActualChrome()
    {
        // Arrange
        var local = TextInputStyle.Default with
        {
            Border = new Border(
                BorderSide.None,
                BorderGlyphStyle.Ascii,
                Color.Default,
                Color.Transparent,
                TerminalAttributes.None)
        };
        using var control = new TextInput();

        // Act
        control.Style = local;

        // Assert
        control.Style.ShouldBe(local);
        control.ActualStyle.ShouldBe(local);
        control.ActualBorder.Sides.ShouldBe(BorderSide.None);
    }

    /// <summary>Verifies TextInput keeps its structural default while a Theme supplies its semantic
    /// input profile, matching every other leaf control migrated onto the Style/ActualStyle pair.</summary>
    [Fact]
    public void Style_WhenThemeChanges_UsesSemanticInputAppearanceWithoutReplacingControlStructure()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            palette: "\"inputFace\":\"#0c2238\"",
            inputGlyphStyle: "\"rounded\"",
            inputExtra: """, "face": { "foreground": "inputFace" }"""));
        using var control = new TextInput();

        control.SetTheme(theme);
        var expected = TextInputStyle.Definition.Resolve(null, theme);

        control.Style.ShouldBeNull();
        control.ActualStyle.ShouldBe(expected);
        control.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(12, 34, 56));
        control.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);

        control.Style = TextInputStyle.Default;
        control.ActualStyle.ShouldBe(TextInputStyle.Default);

        control.Style = null;
        control.Style.ShouldBeNull();
        control.ActualStyle.ShouldBe(expected);
    }

    /// <summary>Verifies style structure invalidates measurement while color-only changes invalidate rendering.</summary>
    [Fact]
    public void Style_WhenStructureOrColorChanges_InvalidatesTheExactPhase()
    {
        var coloredFace = new Face(
            Color.Rgb(1, 2, 3),
            TextInputStyle.Default.Face.Background,
            TextInputStyle.Default.Face.Attributes,
            TextInputStyle.Default.Face.Underline,
            TextInputStyle.Default.Face.UnderlineColor);
        var colored = TextInputStyle.Default with { Face = coloredFace };
        var widerGap = TextInputStyle.Default with { AffixGap = TextInputStyle.Default.AffixGap + 1 };
        using var control = new TextInput { Style = TextInputStyle.Default };
        control.Clear(Invalidation.All);

        control.Style = colored;

        control.Pending.ShouldBe(Invalidation.Render);
        control.Clear(Invalidation.All);

        control.Style = widerGap;

        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a disabled TextInput ignores character input and leaves committed text unchanged.</summary>
    [Fact]
    public void Dispatch_WhenDisabled_IgnoresCharacterInput()
    {
        // Arrange
        var control = new TextInput { Text = "Base", IsEnabled = false };

        // Act
        CharacterKey(control, new Rune('X'), Modifiers.None);

        // Assert
        control.Text.ShouldBe("Base");
    }

    /// <summary>Verifies conservative defaults and every direct assignment validates before mutation.</summary>
    [Fact]
    public void Properties_WhenAssignmentsAreInvalid_PreservePreviousState()
    {
        var control = new TextInput();

        control.Text.ShouldBeEmpty();
        control.CaretIndex.ShouldBe(0);
        control.SelectionStart.ShouldBe(0);
        control.SelectionLength.ShouldBe(0);
        control.MaxLength.ShouldBe(0);
        control.IsReadOnly.ShouldBeFalse();
        control.AcceptsReturn.ShouldBeFalse();
        control.AcceptsTab.ShouldBeFalse();
        control.CanFocus.ShouldBeTrue();
        control.StartAffix.ShouldBeNull();
        control.EndAffix.ShouldBeNull();

        _ = Should.Throw<ArgumentNullException>(() => control.Text = null!);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.MaxLength = -1);
        control.Text = "Ae\u0301Z";
        _ = Should.Throw<ArgumentException>(() => control.CaretIndex = 2);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SelectionStart = 20);
        _ = Should.Throw<ArgumentException>(() => control.PasswordCharacter = new Rune('\n'));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.CursorShape = (CursorShape) 99);

        control.Text.ShouldBe("Ae\u0301Z");
        control.CaretIndex.ShouldBe(control.Text.Length);
        control.PasswordCharacter.ShouldBeNull();
        control.CursorShape.ShouldBe(CursorShape.Block);
    }

    /// <summary>Verifies Text rejects a value that violates the current return policy, leaving the
    /// previously committed text unchanged, matching the "value violates policy" documented
    /// exception.</summary>
    [Fact]
    public void Text_WhenViolatesReturnPolicy_ThrowsBeforeMutation()
    {
        // Arrange
        var control = new TextInput { Text = "start" };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.Text = "a\nb");
        control.Text.ShouldBe("start");
    }

    /// <summary>Verifies MaxLength rejects a value smaller than the current text's grapheme
    /// count, leaving the previous unlimited/positive value unchanged.</summary>
    [Fact]
    public void MaxLength_WhenBelowCurrentTextLength_ThrowsBeforeMutation()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => control.MaxLength = 3);
        control.MaxLength.ShouldBe(0);
    }

    /// <summary>Verifies ScrollBars rejects flag combinations outside the defined axis mask.</summary>
    [Fact]
    public void ScrollBars_WhenValueContainsUndefinedFlags_ThrowsBeforeMutation()
    {
        // Arrange
        var control = new TextInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ScrollBars = (ScrollBars) 99);
        control.ScrollBars.ShouldBe(ScrollBars.Both);
    }

    /// <summary>Verifies ShowScrollBars rejects an undefined value.</summary>
    [Fact]
    public void ShowScrollBars_WhenValueIsUnknown_ThrowsBeforeMutation()
    {
        // Arrange
        var control = new TextInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ShowScrollBars = (ShowScrollBars) 99);
        control.ShowScrollBars.ShouldBe(ShowScrollBars.WhenNeeded);
    }

    /// <summary>Verifies Placeholder round-trips and defaults to null.</summary>
    [Fact]
    public void Placeholder_WhenSet_RoundTripsAndDefaultsToNull()
    {
        // Arrange
        var control = new TextInput();
        control.Placeholder.ShouldBeNull();

        // Act
        control.Placeholder = "Name";

        // Assert
        control.Placeholder.ShouldBe("Name");
    }

    /// <summary>Verifies ScrollBarStyle round-trips and resolves ActualScrollBarStyle, matching
    /// the same local-or-theme precedence every other complete local style property uses.</summary>
    [Fact]
    public void ScrollBarStyle_WhenAssigned_RoundTripsAndResolvesActualScrollBarStyle()
    {
        // Arrange
        var control = new TextInput();
        control.ScrollBarStyle.ShouldBeNull();

        // Act
        control.ScrollBarStyle = ScrollBarStyle.ThinLine;

        // Assert
        control.ScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
        control.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);

        // Act - clearing restores theme ownership
        control.ScrollBarStyle = null;

        // Assert
        control.ScrollBarStyle.ShouldBeNull();
    }

    /// <summary>Verifies SelectionStart moves the normalized range start while preserving the
    /// current selection length.</summary>
    [Fact]
    public void SelectionStart_WhenSet_MovesRangeStartAndPreservesLength()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };
        control.Select(1, 2);

        // Act
        control.SelectionStart = 3;

        // Assert
        control.SelectionStart.ShouldBe(3);
        control.SelectionLength.ShouldBe(2);
        control.CaretIndex.ShouldBe(5);
    }

    /// <summary>Verifies SelectionLength extends the range from the current SelectionStart with
    /// the caret landing at the range end.</summary>
    [Fact]
    public void SelectionLength_WhenSet_ExtendsRangeFromCurrentStart()
    {
        // Arrange and act
        var control = new TextInput { Text = "abcdef", SelectionStart = 1, SelectionLength = 3 };

        // Assert
        control.SelectionStart.ShouldBe(1);
        control.SelectionLength.ShouldBe(3);
        control.CaretIndex.ShouldBe(4);
    }

    /// <summary>Verifies SelectionLength rejects a range that exceeds the current text.</summary>
    [Fact]
    public void SelectionLength_WhenExceedsText_ThrowsBeforeMutation()
    {
        // Arrange
        var control = new TextInput { Text = "abc" };

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SelectionLength = 10);
        control.SelectionLength.ShouldBe(0);
    }

    /// <summary>Verifies CaretIndex collapses any existing selection to the given boundary.</summary>
    [Fact]
    public void CaretIndex_WhenSet_CollapsesSelectionToTheGivenBoundary()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };
        control.Select(1, 3);

        // Act
        control.CaretIndex = 2;

        // Assert
        control.CaretIndex.ShouldBe(2);
        control.SelectionStart.ShouldBe(2);
        control.SelectionLength.ShouldBe(0);
    }

    /// <summary>Verifies WordWrap defaults to false and a change invalidates every layout phase.</summary>
    [Fact]
    public void WordWrap_WhenChanged_InvalidatesMeasure()
    {
        // Arrange
        var control = new TextInput { Text = "Hello" };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.WordWrap.ShouldBeFalse();
        control.Clear(Invalidation.All);

        // Act
        control.WordWrap = true;

        // Assert
        control.WordWrap.ShouldBeTrue();
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies enabling WordWrap reflows content across multiple visual lines at a
    /// constrained width, while an otherwise identical unwrapped editor stays single-line.</summary>
    [Fact]
    public void Measure_WhenWordWrapIsEnabled_WrapsContentAcrossMultipleLines()
    {
        // Arrange
        var wrapped = new TextInput { Text = "one two three four", WordWrap = true };
        var unwrapped = new TextInput { Text = "one two three four" };
        wrapped.SetTheme(TestThemes.BorderlessInput);
        unwrapped.SetTheme(TestThemes.BorderlessInput);

        // Act
        new LayoutEngine().Layout(wrapped, new Size(8, 10));
        new LayoutEngine().Layout(unwrapped, new Size(8, 10));

        // Assert
        wrapped.DesiredSize.Width.ShouldBe(8);
        wrapped.DesiredSize.Height.ShouldBeGreaterThan(1);
        unwrapped.DesiredSize.Height.ShouldBe(1);
    }

    /// <summary>Verifies a word-break rewind that pushes down a word containing a wide (double-
    /// cell) grapheme re-measures that word bounded by the viewport, rather than accumulating an
    /// unchecked cell count past it: at a 3-cell viewport, " xy我" fills " xy" up to the break,
    /// then the wide 我 (2 cells) overflows and forces a rewind to "xy我", which itself no longer
    /// fits as a whole (2 + 2 &gt; 3) and must fall back to a grapheme boundary.</summary>
    [Fact]
    public void Measure_WhenWordWrapRewindPushesDownAWideGrapheme_KeepsEveryVisualLineWithinTheViewport()
    {
        // Arrange
        var control = new TextInput { Text = " xy我", WordWrap = true };
        control.SetTheme(TestThemes.BorderlessInput);

        // Act
        new LayoutEngine().Layout(control, new Size(3, 10));

        // Assert
        var lines = GetVisualLines(control);
        lines.ShouldNotBeEmpty();

        foreach (var line in lines)
        {
            line.Cells.ShouldBeLessThanOrEqualTo(3);
        }
    }

    /// <summary>Verifies that when the word pushed down by a rewind is itself still too wide for
    /// the viewport - here "我我我我我", 10 cells of wide CJK graphemes rewound into a 4-cell
    /// viewport - the same placement logic keeps breaking it across further visual lines instead
    /// of recording one line whose Cells silently overflows the viewport.</summary>
    [Fact]
    public void Measure_WhenPushedDownWordItselfOverflowsTheViewport_BreaksAgainAtGraphemeBoundaries()
    {
        // Arrange
        var control = new TextInput { Text = " 我我我我我", WordWrap = true };
        control.SetTheme(TestThemes.BorderlessInput);

        // Act
        new LayoutEngine().Layout(control, new Size(4, 10));

        // Assert
        var lines = GetVisualLines(control);
        lines.Length.ShouldBeGreaterThan(2);

        foreach (var line in lines)
        {
            line.Cells.ShouldBeLessThanOrEqualTo(4);
        }
    }

    /// <summary>Reads TextInput's private per-line word-wrap layout (offset/length/cell-width
    /// triples) via reflection, since <c>BuildVisualLines</c> and its <c>VisualLine</c> result are
    /// implementation details with no public surface.</summary>
    private static VisualLineSnapshot[] GetVisualLines(TextInput control)
    {
        var field = typeof(TextInput).GetField(
            "_visualLines",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var lines = (Array) field.GetValue(control)!;
        var snapshots = new VisualLineSnapshot[lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines.GetValue(i)!;
            var type = line.GetType();
            var offset = (int) type.GetProperty("Offset")!.GetValue(line)!;
            var length = (int) type.GetProperty("Length")!.GetValue(line)!;
            var cells = (int) type.GetProperty("Cells")!.GetValue(line)!;
            snapshots[i] = new VisualLineSnapshot(offset, length, cells);
        }

        return snapshots;
    }

    private readonly record struct VisualLineSnapshot(int Offset, int Length, int Cells);

    /// <summary>Verifies Select throws the documented ArgumentOutOfRangeException - not an
    /// unchecked OverflowException - when start plus length overflows a 32-bit integer, matching
    /// the "range overflows" case its own XML documentation promises.</summary>
    [Fact]
    public void Select_WhenStartPlusLengthOverflows_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var control = new TextInput();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Select(int.MaxValue, 1));
    }

    /// <summary>Verifies cursor-shape mutation requests only a new semantic render.</summary>
    [Fact]
    public void CursorShape_WhenChanged_InvalidatesRenderOnly()
    {
        var control = new TextInput();
        control.Measure(new Constraint(4, 3));
        control.Arrange(new Rect(0, 0, 4, 3));
        using Frame frame = new(new Size(4, 3));
        control.Render(frame.Canvas);

        control.CursorShape = CursorShape.Underline;

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure - the
    /// reserved viewport width changes between zero and non-zero cells.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        var control = new TextInput { Text = "Hello" };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.Clear(Invalidation.All);

        control.StartAffix = new Affix("!");

        control.Pending.ShouldBe(Invalidation.All);
        control.Clear(Invalidation.All);

        control.StartAffix = null;

        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only,
    /// the exact grading an animated affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public void EndAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        var control = new TextInput { Text = "Hello", EndAffix = new Affix("|") };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.Clear(Invalidation.All);

        control.EndAffix = new Affix("/");

        control.Pending.ShouldBe(Invalidation.Render);
        control.Clear(Invalidation.All);

        control.EndAffix = new Affix("/", "?", SemanticColor.Warning);

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change invalidates Measure again, not just Render, even
    /// though both affix values are non-null.</summary>
    [Fact]
    public void StartAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        var control = new TextInput { Text = "Hello", StartAffix = new Affix("!") };
        control.Measure(new Constraint(10, 3));
        control.Arrange(new Rect(0, 0, 10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);
        control.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        control.StartAffix = new Affix("世");

        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent affix-less TextInput - the same measure/arrange
    /// parity every sibling affix-hosting control (Button, ComboBox, NumberInput) already keeps.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        // Arrange
        var control = new TextInput
        {
            Text = "Hello",
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };
        var bare = new TextInput { Text = "Hello" };

        // Act
        new LayoutEngine().Layout(control, new Size(30, 3));
        new LayoutEngine().Layout(bare, new Size(30, 3));

        // Assert
        (control.DesiredSize.Width - bare.DesiredSize.Width).ShouldBe(expectedExtraWidth);
    }

    /// <summary>Verifies an auto-sized editor with a start affix reserves an unstarved caret
    /// viewport instead of shrinking to the affix-less DesiredSize and leaving ArrangeChrome to
    /// deflate an already-too-narrow content box down toward nothing. This is the actual
    /// user-visible symptom behind the measure/arrange asymmetry the Theory test above only proves
    /// through the DesiredSize number: before the fix, "!" still claims columns 0-1 at arrange
    /// time, but MeasureOverride never reserved room for it, so the caret/selection viewport - and
    /// therefore the caret itself - was starved down to a sliver instead of sitting past "AB".</summary>
    [Fact]
    public void Render_WhenAutoSizedWithStartAffix_KeepsCaretViewportUnstarved()
    {
        // Arrange
        var control = new TextInput { Text = "AB", StartAffix = new Affix("!") };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);

        // Act
        new LayoutEngine().Layout(control, new Size(30, 3));
        using Frame frame = new(new Size(30, 3));
        control.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("!");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("B");
        frame.Cursor.Visible.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(4, 0));
    }

    /// <summary>Verifies cancellable proposal precedes one atomic committed notification sequence.</summary>
    [Fact]
    public void Text_WhenChangingIsCancelled_PreservesStateAndEventOrder()
    {
        var control = new TextInput { Text = "A" };
        List<string> order = [];
        control.TextChanging += (_, eventArgs) =>
        {
            order.Add($"changing:{control.Text}:{eventArgs.Proposal.Text}");
            eventArgs.Cancel = eventArgs.Proposal.Text == "blocked";
        };
        control.TextChanged += (_, eventArgs) =>
            order.Add($"text:{eventArgs.PreviousText}>{eventArgs.Text}:{control.CaretIndex}");
        control.SelectionChanged += (_, eventArgs) =>
            order.Add($"selection:{eventArgs.Previous.Caret}>{eventArgs.Selection.Caret}");

        control.Text = "blocked";
        control.Text.ShouldBe("A");
        control.Text = "界";

        order.ShouldBe([
            "changing:A:blocked",
            "changing:A:界",
            "text:A>界:1"
        ]);
    }

    /// <summary>Verifies reentry from the editor-specific event cannot publish an obsolete common
    /// transition after the newer committed selection.</summary>
    [Fact]
    public void TextSelectionChanged_WhenSelectionChangedReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var control = new TextInput { Text = "abcd" };
        var observed = new List<(Selection EventSelection, Selection LiveSelection)>();
        control.SelectionChanged += (_, eventArgs) =>
        {
            if (eventArgs.Selection == new Selection(0, 1))
            {
                control.Select(0, 2);
            }
        };
        control.TextSelectionChanged += (_, eventArgs) =>
            observed.Add((eventArgs.Selection, control.TextSelection));

        // Act
        control.Select(0, 1);

        // Assert
        observed.ShouldBe([(new Selection(0, 2), new Selection(0, 2))]);
    }

    /// <summary>Verifies typed text and owned paste share policy and grapheme maximum handling.</summary>
    [Fact]
    public void Dispatch_WhenTextAndPasteArrive_AppliesPolicyAndMaximum()
    {
        var control = new TextInput { MaxLength = 3 };

        Route(control, new TextEventArgs(new TerminalText(new Rune('界'))), Events.Text);
        Route(control, new PasteEventArgs(new Paste("e\u0301👩‍💻Z"u8)), Events.Paste);

        control.Text.ShouldBe("界e\u0301👩‍💻");
        Edit.GraphemeCount(control.Text).ShouldBe(3);
        Route(control, new TextEventArgs(new TerminalText(new Rune('\n'))), Events.Text);
        control.Text.ShouldBe("界e\u0301👩‍💻");

        control.AcceptsReturn = true;
        control.MaxLength = 0;
        Route(control, new TextEventArgs(new TerminalText(new Rune('\n'))), Events.Text);
        control.Text.ShouldEndWith("\n");
    }

    /// <summary>Verifies keys outside the editor command set remain available to routed input.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        var control = new TextInput();
        var raised = 0;
        control.KeyDown += (_, _) => raised++;
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.F1,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        // Act
        Route(control, eventArgs, Events.Key);

        // Assert
        eventArgs.IsHandled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies AcceptsTab treats Tab as editable text only when no application-command
    /// modifier accompanies it; Shift and lock state remain ordinary text-entry state.</summary>
    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.Shift, true)]
    [InlineData(Modifiers.CapsLock, true)]
    [InlineData(Modifiers.NumLock, true)]
    [InlineData(Modifiers.Shift | Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    [InlineData(Modifiers.Super, false)]
    [InlineData(Modifiers.Hyper, false)]
    [InlineData(Modifiers.Meta, false)]
    [InlineData(Modifiers.Control | Modifiers.Shift | Modifiers.CapsLock, false)]
    public void Dispatch_WhenAcceptsTabCarriesModifiers_InsertsOnlyForTextEntryState(
        Modifiers modifiers,
        bool expectedInsert)
    {
        var control = new TextInput { Text = "value", AcceptsTab = true };
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Tab,
            character: null,
            nativeCode: 0,
            modifiers,
            KeyAction.Press));

        Route(control, eventArgs, Events.Key);

        control.Text.ShouldBe(expectedInsert ? "value\t" : "value");
        eventArgs.IsHandled.ShouldBe(expectedInsert);
    }

    /// <summary>Verifies navigation, extension, word movement, and deletion use grapheme boundaries.</summary>
    [Fact]
    public void Dispatch_WhenEditingKeysArrive_UsesDirectionalGraphemeSelection()
    {
        var control = new TextInput { Text = "one e\u0301👩‍💻" };

        Key(control, Code.Left, Modifiers.Shift);
        Key(control, Code.Left, Modifiers.Shift);
        control.SelectionLength.ShouldBe(7);
        Key(control, Code.Left, Modifiers.None);
        control.SelectionLength.ShouldBe(0);
        control.CaretIndex.ShouldBe(4);
        Key(control, Code.Right, Modifiers.Control);
        control.CaretIndex.ShouldBe(control.Text.Length);
        Key(control, Code.Backspace, Modifiers.None);

        control.Text.ShouldBe("one e\u0301");
        Edit.IsBoundary(control.Text, control.CaretIndex).ShouldBeTrue();
    }

    /// <summary>Verifies Left visits every grapheme boundary exactly once across mixed ASCII,
    /// combining-mark, and emoji ZWJ graphemes, proving the cached binary-search fast path in
    /// MoveCaretPrevious matches Edit's own boundary ground truth exactly.</summary>
    [Fact]
    public void Dispatch_WhenHoldingLeftAcrossMixedGraphemeKinds_VisitsEveryGraphemeBoundaryExactlyOnce()
    {
        var text = "aé👩‍💻界ébb";
        var control = new TextInput { Text = text };
        Key(control, Code.End, Modifiers.None);
        var steps = 0;

        while (control.CaretIndex > 0)
        {
            var before = control.CaretIndex;
            Key(control, Code.Left, Modifiers.None);
            control.CaretIndex.ShouldBeLessThan(before);
            Edit.IsBoundary(text, control.CaretIndex).ShouldBeTrue();
            steps++;
        }

        steps.ShouldBe(Edit.GraphemeCount(text));
    }

    /// <summary>Verifies Ctrl+Left's cached fast path (MoveCaretPreviousWord) lands on exactly the
    /// same caret index as Edit.MovePreviousWord's own ground truth at every step across mixed
    /// word/whitespace/punctuation/emoji graphemes.</summary>
    [Fact]
    public void Dispatch_WhenHoldingControlLeftAcrossMixedGraphemeKinds_MatchesEditMovePreviousWordAtEveryStep()
    {
        var text = "one  two_3 👩‍💻! four";
        var control = new TextInput { Text = text };
        Key(control, Code.End, Modifiers.None);
        var expected = new Selection(text.Length, text.Length);

        while (expected.Caret > 0)
        {
            expected = Edit.MovePreviousWord(text, expected, extend: false).Selection;
            Key(control, Code.Left, Modifiers.Control);
            control.CaretIndex.ShouldBe(expected.Caret);
        }
    }

    /// <summary>Verifies the boundary cache backing Left rebuilds for a completely replaced Text
    /// value instead of serving offsets computed for the previous string instance - a stale cache
    /// from a combining-mark grapheme would skip a boundary once the text becomes plain ASCII of
    /// the same UTF-16 length.</summary>
    [Fact]
    public void Dispatch_WhenTextIsReplacedAfterCachingBoundaries_RebuildsForTheNewGraphemeStructure()
    {
        var control = new TextInput { Text = "ébb" };
        Key(control, Code.End, Modifiers.None);
        Key(control, Code.Left, Modifiers.None);

        control.Text = "aaaa";
        Key(control, Code.End, Modifiers.None);

        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(3);
        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(2);
        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(1);
        Key(control, Code.Left, Modifiers.None);
        control.CaretIndex.ShouldBe(0);
    }

    /// <summary>Verifies bounded undo and redo retain immutable text and selection snapshots.</summary>
    [Fact]
    public void Undo_WhenHistoryExists_RestoresTextSelectionAndRedo()
    {
        var control = new TextInput { UndoLimit = 2, Text = "A" };
        control.Text = "AB";
        control.Text = "ABC";

        control.CanUndo.ShouldBeTrue();
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("AB");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("A");
        control.Undo().ShouldBeFalse();
        control.Redo().ShouldBeTrue();
        control.Text.ShouldBe("AB");
    }

    /// <summary>Verifies ordinary forward typing coalesces into one undo entry per run, so one
    /// Undo() after typing a whole word reverts all the way to empty rather than one character
    /// at a time.</summary>
    [Fact]
    public void Undo_WhenTypedCharactersCoalesce_OneUndoRevertsToEmpty()
    {
        var control = new TextInput();

        Route(control, new TextEventArgs(new TerminalText(new Rune('a'))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune('b'))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune('c'))), Events.Text);

        control.Text.ShouldBe("abc");
        control.CanUndo.ShouldBeTrue();
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.Undo().ShouldBeFalse();
    }

    /// <summary>Verifies an intervening commit that lands back on the same caret position still
    /// ends the active coalescing run - pure caret adjacency alone is never sufficient, any commit
    /// between two typed characters breaks the run.</summary>
    [Fact]
    public void Undo_WhenInterveningCommitInterruptsTyping_DoesNotCoalesce()
    {
        var control = new TextInput();

        Route(control, new TextEventArgs(new TerminalText(new Rune('a'))), Events.Text);
        control.Select(1, 0);
        Route(control, new TextEventArgs(new TerminalText(new Rune('b'))), Events.Text);

        control.Text.ShouldBe("ab");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("a");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.Undo().ShouldBeFalse();
    }

    /// <summary>Verifies a whitespace/non-whitespace boundary breaks the coalescing run in both
    /// directions, so a word and the space after it land in separate undo entries.</summary>
    [Fact]
    public void Undo_WhenWhitespaceBoundaryCrossed_BreaksCoalescingRunBothWays()
    {
        var control = new TextInput();

        Route(control, new TextEventArgs(new TerminalText(new Rune('a'))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune(' '))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune('b'))), Events.Text);

        control.Text.ShouldBe("a b");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("a ");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("a");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.Undo().ShouldBeFalse();
    }

    /// <summary>Verifies two consecutive typed spaces - matching whitespace classification and
    /// pure adjacency - coalesce into one undo entry.</summary>
    [Fact]
    public void Undo_WhenConsecutiveSpacesTyped_CoalesceIntoOneEntry()
    {
        var control = new TextInput();

        Route(control, new TextEventArgs(new TerminalText(new Rune(' '))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune(' '))), Events.Text);

        control.Text.ShouldBe("  ");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.Undo().ShouldBeFalse();
    }

    /// <summary>Verifies pasted text never coalesces with adjacent typed characters on either
    /// side, even though the paste lands immediately next to them.</summary>
    [Fact]
    public void Undo_WhenPasteInterruptsTyping_NeverCoalescesWithAdjacentTyping()
    {
        var control = new TextInput();

        Route(control, new TextEventArgs(new TerminalText(new Rune('a'))), Events.Text);
        Route(control, new PasteEventArgs(new Paste("bc"u8)), Events.Paste);
        Route(control, new TextEventArgs(new TerminalText(new Rune('d'))), Events.Text);

        control.Text.ShouldBe("abcd");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("abc");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("a");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.Undo().ShouldBeFalse();
    }

    /// <summary>Verifies overtyping a selection right after a coalesced typed run starts a fresh
    /// undo entry instead of merging into the prior run - a non-collapsed selection before the
    /// edit always breaks coalescing.</summary>
    [Fact]
    public void Undo_WhenOvertypingSelectionAfterTypedRun_DoesNotCoalesce()
    {
        var control = new TextInput();

        Route(control, new TextEventArgs(new TerminalText(new Rune('a'))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune('b'))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune('c'))), Events.Text);
        control.Select(0, 3);
        Route(control, new TextEventArgs(new TerminalText(new Rune('x'))), Events.Text);

        control.Text.ShouldBe("x");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("abc");
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.Undo().ShouldBeFalse();
    }

    /// <summary>Verifies Redo() after undoing a coalesced run restores the entire run atomically
    /// in one step, matching the single merged entry Undo() consumed.</summary>
    [Fact]
    public void Redo_AfterCoalescedRunUndone_RestoresEntireRunAtomically()
    {
        var control = new TextInput();

        Route(control, new TextEventArgs(new TerminalText(new Rune('a'))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune('b'))), Events.Text);
        Route(control, new TextEventArgs(new TerminalText(new Rune('c'))), Events.Text);

        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.CanRedo.ShouldBeTrue();
        control.Redo().ShouldBeTrue();
        control.Text.ShouldBe("abc");
        control.CanRedo.ShouldBeFalse();
    }

    /// <summary>Verifies typing more characters than UndoLimit as one continuous word still
    /// leaves exactly one undo entry - the coalescing run - so CanUndo remains true and a single
    /// Undo() clears the whole word. This is the direct regression test for undo coalescing:
    /// before it, each keystroke consumed one UndoLimit slot and the run would have been
    /// partially evicted long before 150 characters.</summary>
    [Fact]
    public void Undo_WhenMoreThanUndoLimitCharactersTypedAsOneWord_SingleUndoClearsWholeWord()
    {
        var control = new TextInput();
        var word = new string('a', control.UndoLimit + 50);

        foreach (var character in word)
        {
            Route(control, new TextEventArgs(new TerminalText(new Rune(character))), Events.Text);
        }

        control.Text.ShouldBe(word);
        control.CanUndo.ShouldBeTrue();
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe(string.Empty);
        control.CanUndo.ShouldBeFalse();
    }

    /// <summary>Verifies UndoLimit = 0 disables both retained undo and retained redo, not just undo.</summary>
    [Fact]
    public void UndoLimit_WhenSetToZero_DisablesBothUndoAndRedo()
    {
        var control = new TextInput { UndoLimit = 1, Text = "A" };
        control.Text = "AB";
        _ = control.Undo();
        control.CanUndo.ShouldBeFalse();
        control.CanRedo.ShouldBeTrue();

        control.UndoLimit = 0;

        control.CanUndo.ShouldBeFalse();
        control.CanRedo.ShouldBeFalse();
        control.Redo().ShouldBeFalse();
        control.Text.ShouldBe("A");
    }

    /// <summary>Verifies lowering UndoLimit while both stacks are populated trims the redo stack
    /// too, dropping its oldest entries rather than only the undo stack's, so a redo entry that
    /// Push would later drop cannot survive as an executable, un-undoable edit.</summary>
    [Fact]
    public void UndoLimit_WhenLoweredWithBothStacksPopulated_TrimsRedoToo()
    {
        var control = new TextInput { UndoLimit = 5, Text = "A" };
        control.Text = "AB";
        control.Text = "ABC";
        _ = control.Undo();
        _ = control.Undo();
        control.Text.ShouldBe("A");

        control.UndoLimit = 1;

        // Only the most recently pushed redo entry ("AB") survives the trim; the older
        // ("ABC") entry is dropped, so exactly one redo succeeds and no more remain.
        control.CanRedo.ShouldBeTrue();
        control.Redo().ShouldBeTrue();
        control.Text.ShouldBe("AB");
        control.CanRedo.ShouldBeFalse();
        control.Redo().ShouldBeFalse();
    }

    /// <summary>Verifies setting IsReadOnly clears retained history, so Undo() cannot delete text
    /// from an editor the application just locked.</summary>
    [Fact]
    public void IsReadOnly_WhenSetTrueWithLiveHistory_ClearsHistorySoUndoCannotMutate()
    {
        var control = new TextInput { Text = "value" };
        control.CanUndo.ShouldBeTrue();

        control.IsReadOnly = true;

        control.CanUndo.ShouldBeFalse();
        control.Undo().ShouldBeFalse();
        control.Text.ShouldBe("value");
    }

    /// <summary>Verifies lowering MaxLength below the length an undo entry would restore clears
    /// retained history, so Undo() cannot recreate text the control could not otherwise hold and
    /// then reject its own configuration being re-applied.</summary>
    [Fact]
    public void MaxLength_WhenLoweredBelowAnUndoEntry_ClearsHistorySoUndoCannotExceedIt()
    {
        var control = new TextInput { Text = "abcdef" };
        control.Text = "abc";
        control.CanUndo.ShouldBeTrue();

        control.MaxLength = 3;

        control.CanUndo.ShouldBeFalse();
        control.Undo().ShouldBeFalse();
        control.Text.ShouldBe("abc");
        _ = Should.NotThrow(() => control.MaxLength = 3);
    }

    /// <summary>Verifies clearing AcceptsReturn clears retained history, so Undo() cannot
    /// reintroduce an embedded line feed into a single-line editor - and the next Enter still
    /// submits single-line text, per the Submitted contract.</summary>
    [Fact]
    public void AcceptsReturn_WhenClearedWithLiveHistory_ClearsHistorySoUndoCannotReintroduceNewline()
    {
        var control = new TextInput { AcceptsReturn = true, Text = "a\nb" };
        control.Text = "ab";
        control.CanUndo.ShouldBeTrue();

        control.AcceptsReturn = false;

        control.CanUndo.ShouldBeFalse();
        control.Undo().ShouldBeFalse();
        control.Text.ShouldBe("ab");

        SubmittedEventArgs? submitted = null;
        control.Submitted += (_, eventArgs) => submitted = eventArgs;
        Key(control, Code.Enter, Modifiers.None);

        _ = submitted.ShouldNotBeNull();
        submitted.Text.ShouldBe("ab");
    }

    /// <summary>Verifies clearing AcceptsTab clears retained history, so Undo() cannot
    /// reintroduce an embedded tab into an editor that now rejects them.</summary>
    [Fact]
    public void AcceptsTab_WhenClearedWithLiveHistory_ClearsHistorySoUndoCannotReintroduceTab()
    {
        var control = new TextInput { AcceptsTab = true, Text = "a\tb" };
        control.Text = "ab";
        control.CanUndo.ShouldBeTrue();

        control.AcceptsTab = false;

        control.CanUndo.ShouldBeFalse();
        control.Undo().ShouldBeFalse();
        control.Text.ShouldBe("ab");
    }

    /// <summary>Verifies read-only suppresses mutation while single-line Enter submits committed text.</summary>
    [Fact]
    public void Dispatch_WhenReadOnlyOrSubmitted_UsesDocumentedBehavior()
    {
        var control = new TextInput { Text = "value", IsReadOnly = true };
        SubmittedEventArgs? submitted = null;
        control.Submitted += (_, eventArgs) => submitted = eventArgs;

        Route(control, new TextEventArgs(new TerminalText(new Rune('X'))), Events.Text);
        Key(control, Code.Backspace, Modifiers.None);
        Key(control, Code.Enter, Modifiers.None);

        control.Text.ShouldBe("value");
        _ = submitted.ShouldNotBeNull();
        submitted.Text.ShouldBe("value");
    }

    /// <summary>Verifies read-only state suppresses mutation without changing whether Enter is a
    /// multiline editing command or a single-line submission command.</summary>
    [Theory]
    [InlineData(false, false, "value", 1)]
    [InlineData(false, true, "value", 1)]
    [InlineData(true, false, "value\n", 0)]
    [InlineData(true, true, "value", 0)]
    public void Dispatch_WhenEnterModeAndReadOnlyStateVary_PreservesEditorMode(
        bool acceptsReturn,
        bool isReadOnly,
        string expectedText,
        int expectedSubmissions)
    {
        var control = new TextInput
        {
            Text = "value",
            AcceptsReturn = acceptsReturn,
            IsReadOnly = isReadOnly
        };
        var submissions = 0;
        control.Submitted += (_, _) => submissions++;

        Key(control, Code.Enter, Modifiers.None);

        control.Text.ShouldBe(expectedText);
        submissions.ShouldBe(expectedSubmissions);
    }

    /// <summary>Verifies Enter, Backspace, and Delete accept only text-entry modifier state and
    /// leave application-command chords unhandled without mutation or submission.</summary>
    [Theory]
    [InlineData(Code.Enter, Modifiers.Control)]
    [InlineData(Code.Enter, Modifiers.Alt)]
    [InlineData(Code.Enter, Modifiers.Super)]
    [InlineData(Code.Enter, Modifiers.Hyper)]
    [InlineData(Code.Enter, Modifiers.Meta)]
    [InlineData(Code.Backspace, Modifiers.Control)]
    [InlineData(Code.Backspace, Modifiers.Alt)]
    [InlineData(Code.Backspace, Modifiers.Super)]
    [InlineData(Code.Delete, Modifiers.Control)]
    [InlineData(Code.Delete, Modifiers.Alt)]
    [InlineData(Code.Delete, Modifiers.Super | Modifiers.Shift | Modifiers.CapsLock)]
    public void Dispatch_WhenEditingCommandCarriesApplicationModifiers_LeavesTextAndRouteUnchanged(
        Code code,
        Modifiers modifiers)
    {
        var control = new TextInput { Text = "value", CaretIndex = 2, AcceptsReturn = true };
        var submissions = 0;
        control.Submitted += (_, _) => submissions++;
        var eventArgs = new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            modifiers,
            KeyAction.Press));

        Route(control, eventArgs, Events.Key);

        control.Text.ShouldBe("value");
        control.CaretIndex.ShouldBe(2);
        submissions.ShouldBe(0);
        eventArgs.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies a held Enter cannot submit the same single-line value repeatedly.</summary>
    [Fact]
    public void Dispatch_WhenEnterRepeats_RaisesSubmittedOnlyForTheInitialKeyDown()
    {
        var control = new TextInput { Text = "value" };
        var submissions = 0;
        control.Submitted += (_, _) => submissions++;

        Key(control, Code.Enter, Modifiers.None);
        Key(control, Code.Enter, Modifiers.None, KeyAction.Repeat);

        submissions.ShouldBe(1);
    }

    /// <summary>Verifies multiline newline insertion remains intentionally repeatable even though
    /// single-line submission is single-shot.</summary>
    [Fact]
    public void Dispatch_WhenMultilineEnterRepeats_InsertsEachRepeatedNewline()
    {
        // Arrange
        var control = new TextInput { AcceptsReturn = true };

        // Act
        Key(control, Code.Enter, Modifiers.None);
        Key(control, Code.Enter, Modifiers.None, KeyAction.Repeat);
        Key(control, Code.Enter, Modifiers.None, KeyAction.Repeat);

        // Assert
        control.Text.ShouldBe("\n\n\n");
    }

    /// <summary>Verifies held shortcut reports do not consume multiple undo history entries.</summary>
    [Fact]
    public void Dispatch_WhenUndoShortcutRepeats_UndoesOnlyTheInitialKeyDown()
    {
        var control = new TextInput { AcceptsReturn = true };
        Route(control, new TextEventArgs(new TerminalText(new Rune('a'))), Events.Text);
        Key(control, Code.Enter, Modifiers.None);
        Route(control, new TextEventArgs(new TerminalText(new Rune('b'))), Events.Text);

        CharacterKey(control, new Rune('z'), Modifiers.Control);
        CharacterKey(control, new Rune('z'), Modifiers.Control, KeyAction.Repeat);

        control.Text.ShouldBe("a\n");
    }

    /// <summary>Verifies password rendering masks every cluster and focused caret reaches the frame.</summary>
    [Fact]
    public void Render_WhenPasswordIsFocused_MasksSourceAndSetsVisibleCursor()
    {
        var control = new TextInput
        {
            Text = "Ae\u0301👩‍💻",
            PasswordCharacter = new Rune('*'),
            Width = Length.Cells(6)
        };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);
        new LayoutEngine().Layout(control, new Size(6, 1));
        using Frame frame = new(new Size(6, 1));

        control.Render(frame.Canvas);

        Cells(frame, 3).ShouldBe("***");
        frame.Cursor.Visible.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(3, 0));
        Encoding.UTF8.GetString(CopyOccupied(frame)).ShouldNotContain("A");
    }

    /// <summary>Verifies a focused editor clipped above the viewport never requests an off-frame cursor.</summary>
    [Fact]
    public void Render_WhenFocusedCaretIsOutsideCanvas_LeavesCursorHidden()
    {
        var control = new TextInput { Bounds = new Rect(0, -1, 12, 1), Text = "Scrolled out" };
        control.SetFocused(true);
        using Frame frame = new(new Size(12, 2));

        Should.NotThrow(() => control.Render(frame.Canvas));

        frame.Cursor.Visible.ShouldBeFalse();
    }

    /// <summary>Verifies selected cells render reversed without splitting a wide grapheme.</summary>
    [Fact]
    public void Render_WhenSelectionContainsWideRune_StylesCompleteOwnedCells()
    {
        var control = new TextInput { Text = "A界Z" };
        control.SetTheme(TestThemes.BorderlessInput);
        control.Select(start: 1, length: 1);
        new LayoutEngine().Layout(control, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        control.Render(frame.Canvas);

        (frame.GetCell(new Point(1, 0)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.Reverse);
        (frame.GetCell(new Point(2, 0)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.Reverse);
        frame.GetCell(new Point(2, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies default intrinsic chrome reserves editor text and caret exactly once.</summary>
    [Fact]
    public void Render_WhenConstructed_InsetsEditorAndCaretInsideHeavyFrame()
    {
        var control = new TextInput { Width = Length.Cells(6), Height = Length.Cells(3), Text = "A" };
        control.SetFocused(true);
        new LayoutEngine().Layout(control, new Size(6, 3));
        using Frame frame = new(new Size(6, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("┏");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("┓");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("┗");
        FrameOracle.Get(frame, new Point(5, 2)).ShouldBe("┛");
        frame.Cursor.Visible.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(2, 1));
    }

    /// <summary>Verifies a TextInput framework rail remains inside its intrinsic border.</summary>
    [Fact]
    public void ScrollBars_WhenBorderIsSet_ContainsRailInsideBorder()
    {
        var control = new TextInput
        {
            Width = Length.Cells(8),
            Height = Length.Cells(5),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour",
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always
        };

        new LayoutEngine().Layout(control, new Size(8, 5));

        control.HitTest(new Point(6, 1)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
        control.HitTest(new Point(7, 1)).ShouldBeSameAs(control);
    }

    /// <summary>Verifies pointer press and inferred-pixel drag focus, capture, and select boundaries.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerDrags_SelectsByRenderedCellsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new TextInput { Bounds = new Rect(0, 0, 8, 1), Text = "A界e\u0301Z" };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using PointerManager capture = new(control);

            _ = capture.Dispatch(Pointer(new Point(0, 0), PointerAction.Press, new Point(5, 5)));
            _ = capture.Dispatch(Pointer(new Point(4, 0), PointerAction.Move, new Point(45, 5)));
            _ = capture.Dispatch(Pointer(new Point(4, 0), PointerAction.Release, new Point(45, 5)));

            focus.Focused.ShouldBeSameAs(control);
            capture.Captured.ShouldBeNull();
            control.SelectionStart.ShouldBe(0);
            control.SelectionLength.ShouldBe(4);
            Edit.IsBoundary(control.Text, control.CaretIndex).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a primary double-click selects the complete Unicode word beneath the pointer.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerDoubleClicksWord_SelectsCompleteWordAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new TextInput { Bounds = new Rect(0, 0, 20, 1), Text = "alpha cafe\u0301 beta" };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            var clock = new ManualTimeProvider();
            using PointerManager capture = new(control, clock);
            var point = new Point(8, 0);

            _ = capture.Dispatch(Pointer(point, PointerAction.Press, new Point(85, 5)));
            _ = capture.Dispatch(Pointer(point, PointerAction.Release, new Point(85, 5)));
            clock.Advance(TimeSpan.FromMilliseconds(200));
            _ = capture.Dispatch(Pointer(point, PointerAction.Press, new Point(85, 5)));
            _ = capture.Dispatch(Pointer(point, PointerAction.Release, new Point(85, 5)));

            focus.Focused.ShouldBeSameAs(control);
            control.SelectedText.ShouldBe("cafe\u0301");
            control.SelectionStart.ShouldBe(6);
            control.SelectionLength.ShouldBe(5);
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies caret visibility scrolls correctly from selection-only key navigation with no
    /// intervening layout pass, so the content-width cache Commit reuses instead of remeasuring
    /// stays correct across repeated moves.</summary>
    [Fact]
    public void Dispatch_WhenArrowKeyMovesCaretPastViewportWithoutRelayout_ScrollsUsingCachedWidth()
    {
        var control = new TextInput { Text = "abcdefghij", CaretIndex = 0 };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);
        new LayoutEngine().Layout(control, new Size(4, 1));

        control.HorizontalOffset.ShouldBe(0);

        for (var index = 0; index < 6; index++)
        {
            Key(control, Code.Right, Modifiers.None);
        }

        control.CaretIndex.ShouldBe(6);
        control.HorizontalOffset.ShouldBeGreaterThan(0);

        for (var index = 0; index < 6; index++)
        {
            Key(control, Code.Left, Modifiers.None);
        }

        control.CaretIndex.ShouldBe(0);
        control.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies caret visibility updates horizontal and vertical offsets after resize.</summary>
    [Fact]
    public void Arrange_WhenCaretExceedsViewport_ScrollsAndClampsAfterResize()
    {
        var control = new TextInput
        {
            AcceptsReturn = true,
            Text = "123456\nabcdef\nXYZ",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);
        var engine = new LayoutEngine();

        engine.Layout(control, new Size(3, 2));
        control.HorizontalOffset.ShouldBe(2);
        control.VerticalOffset.ShouldBe(2);

        control.CaretIndex = 6;
        engine.Layout(control, new Size(3, 2));
        control.HorizontalOffset.ShouldBe(5);
        control.VerticalOffset.ShouldBe(0);
        engine.Layout(control, new Size(10, 5));
        control.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies an unfocused editor never chases a caret into view: a narrow explicit
    /// Width holding a longer, programmatically-assigned Text stays scrolled to the start after
    /// layout instead of jumping to reveal the caret the assignment moved to the text's end.</summary>
    [Fact]
    public void Layout_WhenUnfocusedNarrowWidthGetsLongerText_KeepsHorizontalOffsetAtZero()
    {
        var control = new TextInput { Width = Length.Cells(4) };
        control.SetTheme(TestThemes.BorderlessInput);
        control.Text = "abcdefghij";

        new LayoutEngine().Layout(control, new Size(4, 1));

        control.IsFocused.ShouldBeFalse();
        control.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies a wheel-scrolled offset on an unfocused editor survives a subsequent
    /// layout pass unchanged: the caret-reveal chase never runs while unfocused, and wheel
    /// scrolling itself stays focus-independent, so nothing pulls the offset back to 0.</summary>
    [Fact]
    public void Layout_WhenUnfocusedEditorIsWheelScrolled_PreservesOffsetAcrossRelayout()
    {
        var control = new TextInput { Text = "abcdefghij", CaretIndex = 0, Width = Length.Cells(4) };
        control.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(4, 1));

        Route(control, Wheel(wheelX: 2, wheelY: 0), Events.Pointer);
        control.IsFocused.ShouldBeFalse();
        var scrolled = control.HorizontalOffset;
        scrolled.ShouldBeGreaterThan(0);

        new LayoutEngine().Layout(control, new Size(4, 1));

        control.HorizontalOffset.ShouldBe(scrolled);
    }

    /// <summary>Verifies an unfocused editor re-snaps a mid-cluster horizontal offset to the
    /// nearest cluster start on relayout instead of merely clamping it into range. A wheel scroll
    /// never cluster-aligns (ScrollBy/Move are plain arithmetic), so it can land the offset inside
    /// a double-width glyph; without the unfocused path also re-aligning, that glyph's first cell
    /// would stay scrolled off the left edge indefinitely, unlike the old unconditional chase
    /// which self-healed this on every arrange. The snapped value staying above zero (rather than
    /// resetting to 0) proves this is realignment, not a regression back to that unconditional
    /// chase.</summary>
    [Fact]
    public void Layout_WhenUnfocusedWheelScrollLandsMidCluster_SnapsToClusterStartOnRelayout()
    {
        var control = new TextInput
        {
            Text = "日本語テキスト",
            CaretIndex = 0,
            Width = Length.Cells(4),
            ScrollBars = ScrollBars.None
        };
        control.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(4, 1));

        Route(control, Wheel(wheelX: 3, wheelY: 0), Events.Pointer);
        control.IsFocused.ShouldBeFalse();
        control.HorizontalOffset.ShouldBe(3);

        // A genuine resize, not a same-size re-layout, so arrange actually reruns
        // instead of being skipped as a no-op; the width stays 4, so this changes
        // nothing about the clamp/align math itself.
        new LayoutEngine().Layout(control, new Size(4, 2));

        control.HorizontalOffset.ShouldBe(2);
    }

    /// <summary>Verifies gaining focus forces one caret-reveal pass: an editor left unfocused
    /// with its caret past the viewport (and therefore never chased into view) scrolls the
    /// instant it receives focus, without waiting for a subsequent edit or layout pass.</summary>
    [Fact]
    public void SetFocused_WhenGainingFocusWithCaretOutOfView_RevealsCaret()
    {
        var control = new TextInput { Text = "abcdefghij", Width = Length.Cells(4) };
        control.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(4, 1));

        control.IsFocused.ShouldBeFalse();
        control.HorizontalOffset.ShouldBe(0);

        control.SetFocused(true);

        control.HorizontalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies losing focus preserves whatever offset the focused chase produced,
    /// instead of resetting it the way an unconditional re-chase on the next pass would.</summary>
    [Fact]
    public void SetFocused_WhenLosingFocusAfterChase_PreservesOffset()
    {
        var control = new TextInput { Text = "abcdefghij", Width = Length.Cells(4) };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);
        new LayoutEngine().Layout(control, new Size(4, 1));

        var chased = control.HorizontalOffset;
        chased.ShouldBeGreaterThan(0);

        control.SetFocused(false);

        control.HorizontalOffset.ShouldBe(chased);
    }

    /// <summary>Verifies a wheel scroll moves the editor while movement remains and bubbles at its endpoint.</summary>
    [Fact]
    public void Dispatch_WhenWheelTargetsOverflowingEditor_ScrollsAndBubblesAtEndpoint()
    {
        var control = new TextInput
        {
            AcceptsReturn = true,
            Text = "abcdef\none\ntwo\nthree",
            CaretIndex = 0
        };
        control.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(4, 2));

        var first = Wheel(wheelX: 1, wheelY: -1);
        Route(control, first, Events.Pointer);

        control.HorizontalOffset.ShouldBe(1);
        control.VerticalOffset.ShouldBe(1);
        first.IsHandled.ShouldBeTrue();

        Route(control, Wheel(wheelX: 100, wheelY: -100), Events.Pointer);
        control.HorizontalOffset.ShouldBe(4);
        control.VerticalOffset.ShouldBe(4);

        var endpoint = Wheel(wheelX: 1, wheelY: -1);
        Route(control, endpoint, Events.Pointer);

        endpoint.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies overflowing multiline input exposes a configured canonical vertical scrollbar.</summary>
    [Fact]
    public void ScrollBars_WhenMultilineContentOverflows_ExposesCanonicalVerticalRail()
    {
        var control = new TextInput
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour\nfive",
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine
        };
        control.PropagateTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(control, new Size(8, 3));

        var rail = control.HitTest(new Point(7, 0)).ShouldBeOfType<ScrollBar>();
        rail.Orientation.ShouldBe(Orientation.Vertical);
        rail.ActualStyle.ShouldBe(ScrollBarStyle.ThinLine);
    }

    /// <summary>Verifies an editor at its wheel endpoint leaves the routed delta for its enclosing viewport.</summary>
    [Fact]
    public void Dispatch_WhenEditorWheelReachesEndpoint_OffersNextDeltaToEnclosingViewport()
    {
        var input = new TextInput
        {
            Width = Length.Cells(5),
            Height = Length.Cells(2),
            AcceptsReturn = true,
            Text = "one\ntwo\nthree\nfour",
            CaretIndex = 0
        };
        var content = new Stack();
        content.Children.Add(input);
        content.Children.Add(new ProbeControl(new Size(5, 8)));
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { content }
        };
        input.SetTheme(TestThemes.BorderlessInput);
        new LayoutEngine().Layout(outer, new Size(5, 3));

        Route(input, Wheel(wheelX: 0, wheelY: -100), Events.Pointer);
        input.VerticalOffset.ShouldBe(4);
        outer.VerticalOffset.ShouldBe(0);

        var endpoint = Wheel(wheelX: 0, wheelY: -1);
        Route(input, endpoint, Events.Pointer);

        outer.VerticalOffset.ShouldBe(1);
        endpoint.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies a notification exception preserves the committed atomic state.</summary>
    [Fact]
    public void Text_WhenChangedHandlerThrows_PreservesCommittedStateAndFutureEdits()
    {
        var control = new TextInput();
        var failure = new InvalidOperationException("observer");

        void Handler(object? sender, TextChangedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            throw failure;
        }

        control.TextChanged += Handler;

        Should.Throw<InvalidOperationException>(() => control.Text = "A").ShouldBeSameAs(failure);
        control.Text.ShouldBe("A");
        control.CaretIndex.ShouldBe(1);
        control.TextChanged -= Handler;
        control.Text = "B";
        control.Text.ShouldBe("B");
    }

    /// <summary>Verifies typed-input observers cannot be mistaken for rejected edit policy.</summary>
    [Fact]
    public void Dispatch_WhenObserverThrowsArgumentException_PropagatesAfterCommit()
    {
        var control = new TextInput();
        var failure = new ArgumentException("observer");
        control.TextChanged += (_, _) => throw failure;

        Should.Throw<ArgumentException>(() =>
                Route(control, new TextEventArgs(new TerminalText(new Rune('A'))), Events.Text))
            .ShouldBeSameAs(failure);

        control.Text.ShouldBe("A");
        control.CaretIndex.ShouldBe(1);
    }

    /// <summary>Verifies standard select-all, undo, and redo shortcuts use immutable snapshots.</summary>
    [Fact]
    public void Dispatch_WhenControlShortcutsArrive_SelectsAndRestoresHistory()
    {
        var control = new TextInput { Text = "A" };
        control.Text = "AB";

        CharacterKey(control, new Rune('a'), Modifiers.Control);
        control.SelectionStart.ShouldBe(0);
        control.SelectionLength.ShouldBe(2);
        CharacterKey(control, new Rune('z'), Modifiers.Control);
        control.Text.ShouldBe("A");
        CharacterKey(control, new Rune('y'), Modifiers.Control);
        control.Text.ShouldBe("AB");
    }

    /// <summary>Verifies undo and redo require the exact Control chord after lock-key
    /// normalization, leaving larger application chords unhandled.</summary>
    [Theory]
    [InlineData('z', Modifiers.Control, true)]
    [InlineData('Z', Modifiers.Control | Modifiers.CapsLock, true)]
    [InlineData('y', Modifiers.Control | Modifiers.NumLock, true)]
    [InlineData('Y', Modifiers.Control | Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData('z', Modifiers.Control | Modifiers.Shift, false)]
    [InlineData('z', Modifiers.Control | Modifiers.Alt, false)]
    [InlineData('z', Modifiers.Control | Modifiers.Super, false)]
    [InlineData('z', Modifiers.Control | Modifiers.Hyper, false)]
    [InlineData('z', Modifiers.Control | Modifiers.Meta, false)]
    [InlineData('y', Modifiers.Control | Modifiers.Shift, false)]
    [InlineData('y', Modifiers.Control | Modifiers.Alt, false)]
    [InlineData('y', Modifiers.Control | Modifiers.Super, false)]
    [InlineData('y', Modifiers.Control | Modifiers.Hyper, false)]
    [InlineData('y', Modifiers.Control | Modifiers.Meta, false)]
    public void Dispatch_WhenUndoOrRedoCarriesModifiers_MatchesExactNormalizedCommand(
        char command,
        Modifiers modifiers,
        bool expectedExecution)
    {
        // Arrange
        var control = new TextInput { Text = "A" };
        control.Text = "AB";
        var isRedo = char.ToLowerInvariant(command) == 'y';

        if (isRedo)
        {
            control.Undo().ShouldBeTrue();
        }

        var key = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(command),
            nativeCode: 0,
            modifiers,
            KeyAction.Press));

        // Act
        Route(control, key, Events.Key);

        // Assert
        control.Text.ShouldBe(expectedExecution
            ? isRedo ? "AB" : "A"
            : isRedo ? "A" : "AB");
        key.IsHandled.ShouldBe(expectedExecution);
    }

    /// <summary>Verifies copy/cut ownership, read-only behavior, and password secrecy defaults.</summary>
    [Fact]
    public void CutSelection_WhenSelectionExists_ReturnsOwnedTextAndHonorsSecurityPolicy()
    {
        var control = new TextInput { Text = "A界Z" };
        control.Select(1, 1);

        control.CopySelection().ShouldBe("界");
        control.CutSelection().ShouldBe("界");
        control.Text.ShouldBe("AZ");

        control.Text = "secret";
        control.Select(0, control.Text.Length);
        control.IsReadOnly = true;
        control.CutSelection().ShouldBe("secret");
        control.Text.ShouldBe("secret");
        control.PasswordCharacter = new Rune('*');
        control.CopySelection().ShouldBeEmpty();
        control.CutSelection().ShouldBeEmpty();
        control.Text.ShouldBe("secret");
    }

    /// <summary>Verifies a consumer can replace a selection through the public primitive and reach
    /// the same undo/redo history as ordinary keyboard-driven edits.</summary>
    [Fact]
    public void ReplaceSelection_WhenSelectionExists_ReplacesAndParticipatesInUndoHistory()
    {
        var control = new TextInput { Text = "Hello World" };
        control.Select(6, 5);

        control.ReplaceSelection("there").ShouldBeTrue();

        control.Text.ShouldBe("Hello there");
        control.CaretIndex.ShouldBe("Hello there".Length);
        control.SelectionLength.ShouldBe(0);
        control.CanUndo.ShouldBeTrue();
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("Hello World");
    }

    /// <summary>Verifies a direct edit committed from TextChanging supersedes the stale outer
    /// proposal and owns the single undo snapshot.</summary>
    [Fact]
    public void Text_WhenTextChangingReenters_PreservesNewerEditAndUndoHistory()
    {
        // Arrange
        var control = new TextInput { Text = "A" };
        var reentered = false;
        var changed = new List<string>();
        control.TextChanging += (_, _) =>
        {
            if (!reentered)
            {
                reentered = true;
                control.Text = "handler";
            }
        };
        control.TextChanged += (_, eventArgs) => changed.Add(eventArgs.Text);

        // Act
        control.Text = "outer";

        // Assert
        control.Text.ShouldBe("handler");
        control.CaretIndex.ShouldBe("handler".Length);
        changed.ShouldBe(["handler"]);
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("A");
        control.Redo().ShouldBeTrue();
        control.Text.ShouldBe("handler");
    }

    /// <summary>Verifies a replacement committed from TextChanging supersedes the outer
    /// replacement without adding an intermediate undo entry.</summary>
    [Fact]
    public void ReplaceSelection_WhenTextChangingReenters_PreservesNewerReplacement()
    {
        // Arrange
        var control = new TextInput { Text = "A", CaretIndex = 1 };
        var reentered = false;
        control.TextChanging += (_, _) =>
        {
            if (!reentered)
            {
                reentered = true;
                control.ReplaceSelection("handler").ShouldBeTrue();
            }
        };

        // Act
        var committed = control.ReplaceSelection("outer");

        // Assert
        committed.ShouldBeFalse();
        control.Text.ShouldBe("Ahandler");
        control.CaretIndex.ShouldBe("Ahandler".Length);
        control.Undo().ShouldBeTrue();
        control.Text.ShouldBe("A");
        control.Redo().ShouldBeTrue();
        control.Text.ShouldBe("Ahandler");
    }

    /// <summary>Verifies replacement inserts at the caret when there is no selection, and that
    /// grapheme-unsafe Unicode (combining marks, ZWJ emoji sequences) commits as complete clusters
    /// rather than splitting them.</summary>
    [Fact]
    public void ReplaceSelection_WhenNoSelection_InsertsCompleteGraphemeClustersAtCaret()
    {
        var control = new TextInput { Text = "AZ", CaretIndex = 1 };

        control.ReplaceSelection("é👩‍💻").ShouldBeTrue();

        control.Text.ShouldBe("Aé👩‍💻Z");
        Edit.IsBoundary(control.Text, control.CaretIndex).ShouldBeTrue();
    }

    /// <summary>Verifies a read-only control declines the edit and reports no commit.</summary>
    [Fact]
    public void ReplaceSelection_WhenReadOnly_DeclinesAndPreservesText()
    {
        var control = new TextInput { Text = "value", IsReadOnly = true };
        control.Select(0, control.Text.Length);

        control.ReplaceSelection("changed").ShouldBeFalse();

        control.Text.ShouldBe("value");
    }

    /// <summary>Verifies a TextChanging cancellation declines the edit exactly as it does for
    /// keyboard-driven edits, preserving the prior text and reporting no commit.</summary>
    [Fact]
    public void ReplaceSelection_WhenTextChangingCancels_DeclinesAndPreservesText()
    {
        var control = new TextInput { Text = "A" };
        control.TextChanging += (_, eventArgs) => eventArgs.Cancel = true;

        control.ReplaceSelection("B").ShouldBeFalse();

        control.Text.ShouldBe("A");
    }

    /// <summary>Verifies content that would exceed MaxLength after retaining the untouched prefix
    /// and suffix declines the edit rather than silently truncating.</summary>
    [Fact]
    public void ReplaceSelection_WhenRetainedTextAlreadyMeetsMaxLength_Declines()
    {
        var control = new TextInput { Text = "ABC", MaxLength = 3, CaretIndex = 3 };

        control.ReplaceSelection("D").ShouldBeFalse();

        control.Text.ShouldBe("ABC");
    }

    /// <summary>Verifies a multiline control accepts an embedded line break through the same
    /// primitive used for AcceptsReturn-gated Enter handling.</summary>
    [Fact]
    public void ReplaceSelection_WhenMultilineAcceptsReturn_InsertsEmbeddedLineBreak()
    {
        var control = new TextInput { Text = "AB", AcceptsReturn = true, CaretIndex = 1 };

        control.ReplaceSelection("\n").ShouldBeTrue();

        control.Text.ShouldBe("A\nB");
    }

    /// <summary>Verifies password masking does not block editing through the public primitive —
    /// only CopySelection/CutSelection source disclosure is suppressed.</summary>
    [Fact]
    public void ReplaceSelection_WhenPasswordMasked_StillCommitsTheEdit()
    {
        var control = new TextInput { Text = "secret", PasswordCharacter = new Rune('*') };
        control.Select(0, control.Text.Length);

        control.ReplaceSelection("hunter2").ShouldBeTrue();

        control.Text.ShouldBe("hunter2");
    }

    /// <summary>Verifies a mounted, focused control reflects a programmatic replacement in its
    /// rendered content exactly as a keyboard-driven edit would.</summary>
    [Fact]
    public void ReplaceSelection_WhenMounted_RendersReplacedContent()
    {
        var control = new TextInput { Text = "Hello", Width = Length.Cells(8) };
        control.SetTheme(TestThemes.BorderlessInput);
        control.SetFocused(true);
        control.Select(0, control.Text.Length);
        new LayoutEngine().Layout(control, new Size(8, 1));
        using Frame frame = new(new Size(8, 1));

        control.ReplaceSelection("Bye").ShouldBeTrue();
        control.Render(frame.Canvas);

        Cells(frame, 3).ShouldBe("Bye");
    }

    /// <summary>Verifies vertical navigation maps the current rendered column to an adjacent line.</summary>
    [Fact]
    public void Dispatch_WhenUpArrives_MovesToNearestBoundaryOnPreviousLine()
    {
        var control = new TextInput { AcceptsReturn = true, Text = "abc\n12345" };

        Key(control, Code.Up, Modifiers.None);

        control.CaretIndex.ShouldBe(3);
    }

    /// <summary>Verifies Up and Down snap to the nearest cell column across rows mixing single-cell
    /// ASCII and a two-cell CJK grapheme, proving the cached row-lookup fast path
    /// (<c>IndexAtRowFast</c>, backing <c>MoveVertical</c>) matches the exact boundary and half-cell
    /// snap the prior full-document-per-row scan produced.</summary>
    [Fact]
    public void Dispatch_WhenHoldingUpAndDownAcrossMixedGraphemeWidths_SnapsToTheExactExpectedBoundary()
    {
        // Row 0 "ab" (columns 0-2) ends at offset 2.
        // Row 1 "界c" (界: two cells at column 0-2; c: one cell at column 2-3) spans offsets 3-5.
        // Row 2 "xy" (columns 0-2) spans offsets 6-8, the document end.
        var control = new TextInput { AcceptsReturn = true, Text = "ab\n界c\nxy" };

        // End of row 0 (column 2) descends to the exact matching column on row 1, past 界.
        control.Select(2, 0);
        Key(control, Code.Down, Modifiers.None);
        control.CaretIndex.ShouldBe(4);

        // The same column descends again to the document end on row 2.
        Key(control, Code.Down, Modifiers.None);
        control.CaretIndex.ShouldBe(8);

        // Ascending retraces the identical boundaries back to row 0.
        Key(control, Code.Up, Modifiers.None);
        control.CaretIndex.ShouldBe(4);
        Key(control, Code.Up, Modifiers.None);
        control.CaretIndex.ShouldBe(2);

        // Column 1 on row 0 (after 'a', mid-cluster of the two-cell 界 on row 1) snaps to 界's far
        // half-cell boundary, matching the original scan's exact midpoint tie-break rule.
        control.Select(1, 0);
        Key(control, Code.Down, Modifiers.None);
        control.CaretIndex.ShouldBe(4);
    }

    /// <summary>Verifies losing focus during pointer selection releases capture and held state.</summary>
    [Fact]
    public async Task Dispatch_WhenFocusLeavesDuringPointerDrag_CancelsCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 2) };
            var control = new TextInput { Bounds = new Rect(0, 0, 8, 1), Text = "select" };
            var other = new ProbeControl { Bounds = new Rect(10, 0, 2, 1), IsFocusable = true };
            root.Children.Add(control);
            root.Children.Add(other);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(new Point(0, 0), PointerAction.Press, new Point(5, 5)));
            _ = capture.Dispatch(Pointer(new Point(2, 0), PointerAction.Move, new Point(7, 5)));
            capture.Captured.ShouldBeSameAs(control);
            focus.Focus(other).ShouldBeTrue();

            capture.Captured.ShouldBeNull();
            control.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Select rejects a negative start before mutating the current selection.</summary>
    [Fact]
    public void Select_WhenStartIsNegative_ThrowsAndPreservesSelection()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };
        control.Select(1, 2);

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => control.Select(-1, 2));

        // Assert
        exception.ParamName.ShouldBe("start");
        control.SelectionStart.ShouldBe(1);
        control.SelectionLength.ShouldBe(2);
    }

    /// <summary>Verifies Select rejects a negative length before mutating the current selection.</summary>
    [Fact]
    public void Select_WhenLengthIsNegative_ThrowsAndPreservesSelection()
    {
        // Arrange
        var control = new TextInput { Text = "abcdef" };
        control.Select(1, 2);

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => control.Select(1, -2));

        // Assert
        exception.ParamName.ShouldBe("length");
        control.SelectionStart.ShouldBe(1);
        control.SelectionLength.ShouldBe(2);
    }

    /// <summary>Verifies Select rejects a range that exceeds the current text without mutating the
    /// prior selection, matching its documented "range overflows or exceeds text" contract for the
    /// non-overflowing exceeds-text case.</summary>
    [Fact]
    public void Select_WhenRangeExceedsText_ThrowsArgumentOutOfRangeExceptionAndPreservesSelection()
    {
        // Arrange
        var control = new TextInput { Text = "abc" };
        control.Select(1, 1);

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => control.Select(0, 10));

        // Assert
        exception.ParamName.ShouldBe("selection");
        control.SelectionStart.ShouldBe(1);
        control.SelectionLength.ShouldBe(1);
    }

    /// <summary>Verifies Select rejects an endpoint that splits an extended grapheme cluster
    /// (landing inside a ZWJ emoji sequence) without mutating the prior selection.</summary>
    [Fact]
    public void Select_WhenEndpointSplitsGraphemeCluster_ThrowsArgumentExceptionAndPreservesSelection()
    {
        // Arrange - "👩‍💻" is one extended grapheme cluster spanning multiple UTF-16 code units.
        var control = new TextInput { Text = "A👩‍💻Z" };
        control.Select(0, 1);

        // Act
        var exception = Should.Throw<ArgumentException>(() => control.Select(2, 1));

        // Assert
        exception.ParamName.ShouldBe("selection");
        control.SelectionStart.ShouldBe(0);
        control.SelectionLength.ShouldBe(1);
    }

    /// <summary>Verifies CopySelection and CutSelection both return empty and leave text untouched
    /// when there is no active selection, matching their documented "no selection" empty-return
    /// case.</summary>
    [Fact]
    public void CopySelectionAndCutSelection_WhenNoSelectionExists_ReturnEmptyWithoutMutation()
    {
        // Arrange
        var control = new TextInput { Text = "abc" };

        // Act and assert
        control.SelectionLength.ShouldBe(0);
        control.CopySelection().ShouldBeEmpty();
        control.CutSelection().ShouldBeEmpty();
        control.Text.ShouldBe("abc");
    }

    /// <summary>Verifies ReplaceSelection rejects a null replacement value.</summary>
    [Fact]
    public void ReplaceSelection_WhenValueIsNull_Throws()
    {
        // Arrange
        var control = new TextInput { Text = "abc" };

        // Act and assert
        var exception = Should.Throw<ArgumentNullException>(() => control.ReplaceSelection(null!));
        exception.ParamName.ShouldBe("value");
        control.Text.ShouldBe("abc");
    }

    /// <summary>Verifies every documented public editing and history method rejects use after
    /// disposal with the documented ObjectDisposedException, instead of silently operating on a
    /// disposed control's stale state.</summary>
    [Fact]
    public void Methods_WhenControlIsDisposed_ThrowObjectDisposedException()
    {
        // Arrange
        var control = new TextInput { Text = "abc" };
        control.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => control.Select(0, 1));
        _ = Should.Throw<ObjectDisposedException>(control.CopySelection);
        _ = Should.Throw<ObjectDisposedException>(control.CutSelection);
        _ = Should.Throw<ObjectDisposedException>(() => control.ReplaceSelection("x"));
        _ = Should.Throw<ObjectDisposedException>(() => control.Undo());
        _ = Should.Throw<ObjectDisposedException>(() => control.Redo());
    }

    private static void Key(
        TextInput control,
        Code code,
        Modifiers modifiers,
        KeyAction action = KeyAction.Press) =>
        Route(
            control,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                modifiers,
                action)),
            Events.Key);

    private static void CharacterKey(
        TextInput control,
        Rune character,
        Modifiers modifiers,
        KeyAction action = KeyAction.Press) =>
        Route(
            control,
            new KeyEventArgs(new Stroke(
                Code.Character,
                character,
                nativeCode: 0,
                modifiers,
                action)),
            Events.Key);

    private static void Route<T>(TextInput control, T eventArgs, Event<T> routedEvent)
        where T : RoutedEventArgs => Router.Route(control, routedEvent, eventArgs);

    private static Pointer Pointer(Point cells, PointerAction action, Point pixels) => new(
        cells,
        pixels,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: true);

    private static PointerEventArgs Wheel(int wheelX, int wheelY) => new(new Pointer(
        cells: default,
        pixels: null,
        Buttons.None,
        PointerAction.Wheel,
        wheelX,
        wheelY,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false));

    private static string Cells(Frame frame, int count)
    {
        var result = new StringBuilder(count);

        for (var x = 0; x < count; x++)
        {
            _ = result.Append(FrameOracle.Get(frame, new Point(x, 0)));
        }

        return result.ToString();
    }

    private static byte[] CopyOccupied(Frame frame)
    {
        List<byte> result = [];

        for (var x = 0; x < frame.Size.Width; x++)
        {
            var point = new Point(x, 0);

            if (frame.GetCell(point).Continuation)
            {
                continue;
            }

            var length = frame.GetGraphemeByteCount(point);

            if (length == 0)
            {
                continue;
            }

            var bytes = new byte[length];
            _ = frame.CopyGrapheme(point, bytes);
            result.AddRange(bytes);
        }

        return [.. result];
    }
}
