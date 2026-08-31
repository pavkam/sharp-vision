// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using Performance;


/// <summary>Verifies cached Text measurement, rendering, validation, and styling.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TextTests
{
    /// <summary>Verifies constructor content and documented defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var empty = new ControlText();
        var value = new ControlText("hello");

        empty.Content.ShouldBe(string.Empty);
        value.Content.ShouldBe("hello");
        value.Overflow.ShouldBe(Overflow.Visible);
        value.TextAlignment.ShouldBe(Alignment.Start);
        value.AmbiguousWidth.ShouldBe(Ambiguous.Narrow);
        value.Face.Foreground.ShouldBe(SemanticColor.ControlText);

        // Text never paints its own background; the code-owned default stays transparent so a
        // caption can inherit a state-ambient parent's face instead of masking it.
        value.Face.Background.ShouldBe(Color.Transparent);
        value.Face.Attributes.ShouldBe(SemanticDecoration.NormalText);
        value.Lines.Length.ShouldBe(0);
        value.CanFocus.ShouldBeFalse();
    }

    /// <summary>Verifies the content constructor rejects a null value before constructing the control.</summary>
    [Fact]
    public void Constructor_WhenContentIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => new ControlText(null!));

    /// <summary>Verifies TextChanged fires when Content changes and does not fire for identical assignment.</summary>
    [Fact]
    public void TextChanged_WhenContentChanges_Fires()
    {
        // Arrange
        var fired = 0;
        var text = new ControlText("before");
        text.TextChanged += (_, _) => fired++;

        // Act
        text.Content = "after";
        text.Content = "after"; // identical — should not fire

        // Assert
        fired.ShouldBe(1);
    }

    /// <summary>Verifies invalid content and enum values throw before mutation.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        var text = new ControlText("safe");

        _ = Should.Throw<ArgumentNullException>(() => text.Content = null!);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.Overflow = (Overflow) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.TextAlignment = (Alignment) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.AmbiguousWidth = (Ambiguous) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            text.Face = AppearanceTestValues.Face(attributes: (TerminalAttributes) int.MaxValue));

        text.Content.ShouldBe("safe");
        text.Overflow.ShouldBe(Overflow.Visible);
        text.Face.Attributes.ShouldBe(SemanticDecoration.NormalText);
    }

    /// <summary>Verifies wrapped Unicode commits exact line metrics and desired size.</summary>
    [Fact]
    public void Layout_WhenContentWraps_CommitsGraphemeSafeLines()
    {
        var text = new ControlText("e\u0301界x") { Overflow = Overflow.WrapAnywhere };

        new LayoutEngine().Layout(text, new Size(2, 4));

        text.DesiredSize.ShouldBe(new Size(2, 3));
        text.Lines.ToArray().ShouldBe([
            new Line(0, 2, 1, 0, false),
            new Line(2, 1, 2, 0, false),
            new Line(3, 1, 1, 0, false)
        ]);
    }

    /// <summary>Verifies final resize reflows lines and recomputes alignment.</summary>
    [Fact]
    public void Layout_WhenViewportResizes_ReflowsAndRealignsLines()
    {
        var text = new ControlText("abcd")
        {
            Overflow = Overflow.WrapAnywhere,
            TextAlignment = Alignment.End,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var engine = new LayoutEngine();

        engine.Layout(text, new Size(2, 3));
        text.Lines.Length.ShouldBe(2);
        engine.Layout(text, new Size(6, 1));

        text.Lines.Length.ShouldBe(1);
        text.Lines.Span[0].Leading.ShouldBe(2);
    }

    /// <summary>Verifies an arrange-only widening of a word-wrapped control reflows its wrapped
    /// lines back together instead of reusing a stale multi-line split. The skip-reformat
    /// optimization in EnsureLayout only compares the new width against the longest
    /// already-wrapped line, not the content's true unwrapped extent - a wrapped line that
    /// happened to leave slack under the previous width let a still-insufficient new width pass
    /// that check and keep the stale split even though the complete text now fits on one line.
    /// Mirrors <see cref="EnsureLayout_WhenArrangeWidthExceedsMeasuredContent_SkipsReformat"/>'s
    /// measure-once/arrange-wider shape, which is exactly how a parent panel (for example a Grid
    /// track) can hand a child a final slot wider than what it was measured against.</summary>
    [Fact]
    public void EnsureLayout_WhenWrappedArrangeWidensToFit_MergesLines()
    {
        var text = new ControlText("aa bbbb")
        {
            Overflow = Overflow.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        text.Measure(new Constraint(width: 6, height: 2));
        text.Arrange(new Rect(0, 0, 6, 2));

        text.Lines.Length.ShouldBe(2);

        text.Arrange(new Rect(0, 0, 7, 1));

        text.Lines.Length.ShouldBe(1);
        text.Lines.Span[0].Cells.ShouldBe(7);
    }

    /// <summary>Verifies multiline and ellipsis output occupy exact semantic cells.</summary>
    [Fact]
    public void Render_WhenContentIsTrimmedAndMultiline_WritesExpectedCells()
    {
        var text = new ControlText("ab界c\nZ") { Overflow = Overflow.Ellipsis };
        new LayoutEngine().Layout(text, new Size(4, 2));
        using Frame frame = new(new Size(4, 2));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("b");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("…");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("Z");
    }

    /// <summary>Verifies an ambiguous-wide ellipsis uses its one-cell theme fallback.</summary>
    [Fact]
    public void Render_WhenEllipsisIsAmbiguousWide_UsesThemeFallback()
    {
        var text = new ControlText("abcde") { Overflow = Overflow.Ellipsis, AmbiguousWidth = Ambiguous.Wide };
        new LayoutEngine().Layout(text, new Size(4, 1));
        using Frame frame = new(new Size(4, 1), ambiguousWidth: Ambiguous.Wide);

        text.Render(frame.Canvas);

        text.Lines.Span[0].Cells.ShouldBe(4);
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("c");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe(".");
        frame.GetCell(new Point(3, 0)).Continuation.ShouldBeFalse();
    }

    /// <summary>Verifies an ellipsis replacing the first wide cluster keeps that cluster's markup style.</summary>
    [Fact]
    public void Render_WhenMarkedWideClusterBecomesEllipsis_PreservesMarkupStyle()
    {
        var text = new ControlText("<red>界</red>") { Overflow = Overflow.Ellipsis };
        new LayoutEngine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("…");
        frame.GetCell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(1));
    }

    /// <summary>Verifies text without a declared background preserves the already-painted surface.</summary>
    [Fact]
    public void Render_WhenBackgroundIsUnset_PreservesDestinationSurface()
    {
        var text = new ControlText("A")
        {
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(45))
        };
        new LayoutEngine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));
        frame.Canvas.Fill(
            new Rect(0, 0, 1, 1),
            new Rune(' '),
            new TerminalStyle(ReferenceColors.Get(255), ReferenceColors.Get(238)));

        text.Render(frame.Canvas);

        frame.GetCell(default).Style.ShouldBe(new TerminalStyle(ReferenceColors.Get(45), ReferenceColors.Get(238)));
    }

    /// <summary>Verifies an active marked mnemonic registers a render-only dependency on the
    /// root theme's hotkey color.</summary>
    [Fact]
    public void Theme_WhenMarkedMnemonicIsActive_InvalidatesOnlyRenderForHotkeyChange()
    {
        // Arrange
        var mounted = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var replacement = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var text = new ControlText("&Save") { UseMnemonic = true };
        text.SetTheme(mounted);
        new LayoutEngine().Layout(text, new Size(4, 1));
        text.Clear(Invalidation.All);

        // Act
        text.SetTheme(replacement);

        // Assert
        text.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies text without an effective mnemonic marker does not subscribe to an
    /// unrelated root hotkey color.</summary>
    [Theory]
    [InlineData("Save", true)]
    [InlineData("&Save", false)]
    public void Theme_WhenMnemonicHighlightingIsInactive_IgnoresHotkeyOnlyChange(string content, bool useMnemonic)
    {
        // Arrange
        var mounted = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var replacement = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var text = new ControlText(content) { UseMnemonic = useMnemonic };
        text.SetTheme(mounted);
        new LayoutEngine().Layout(text, new Size(4, 1));
        text.Clear(Invalidation.All);

        // Act
        text.SetTheme(replacement);

        // Assert
        text.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies changing mnemonic participation removes and restores the conditional
    /// hotkey dependency after the corresponding layout is consumed.</summary>
    [Fact]
    public void UseMnemonic_WhenChanged_UpdatesTheHotkeyThemeDependency()
    {
        // Arrange
        var first = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var text = new ControlText("&Save") { UseMnemonic = true };
        var layout = new LayoutEngine();
        text.SetTheme(first);
        layout.Layout(text, new Size(4, 1));

        // Act - consume the disabled mnemonic state, then replace only Hotkey.
        text.UseMnemonic = false;
        layout.Layout(text, new Size(5, 1));
        text.Clear(Invalidation.All);
        text.SetTheme(second);

        // Assert
        text.Pending.ShouldBe(Invalidation.None);

        // Act - consume the active mnemonic state again, then replace only Hotkey.
        text.UseMnemonic = true;
        layout.Layout(text, new Size(4, 1));
        text.Clear(Invalidation.All);
        text.SetTheme(first);

        // Assert
        text.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies hidden and collapsed text do not draw stale cells.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void Render_WhenTextIsUnavailable_WritesNoCells(Visibility visibility)
    {
        var text = new ControlText("secret") { Visibility = visibility };
        new LayoutEngine().Layout(text, new Size(6, 1));
        using Frame frame = new(new Size(6, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe(string.Empty);
        text.DesiredSize.ShouldBe(visibility == Visibility.Collapsed ? default : new Size(6, 1));
    }

    /// <summary>Verifies a warmed unchanged layout/render cycle allocates no managed memory.</summary>
    [Fact]
    public void Render_WhenLayoutIsUnchanged_AllocatesNoManagedMemoryAfterWarmup()
    {
        var text = new ControlText("e\u0301 · 界 · 👩‍💻") { Overflow = Overflow.Wrap };
        var engine = new LayoutEngine();
        var size = new Size(80, 2);
        using Frame frame = new(size);
        Render();

        for (var index = 0; index < 1_000; index++)
        {
            Render();
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 1_000; index++)
            {
                Render();
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
        return;

        void Render()
        {
            engine.Layout(text, size);
            frame.Clear();
            text.Invalidate(Invalidation.Render);
            text.Render(frame.Canvas);
        }
    }

    /// <summary>Verifies valid tags are removed before measuring visible content.</summary>
    [Fact]
    public void Layout_WhenContentContainsMarkup_MeasuresVisibleTextOnly()
    {
        ControlText text = new("<b>hi</b>");

        new LayoutEngine().Layout(text, new Size(80, 1));

        text.DesiredSize.ShouldBe(new Size(2, 1));
        text.Lines.ToArray().ShouldBe([new Line(0, 2, 2, 0, false)]);
    }

    /// <summary>Verifies malformed markup remains visible and never fails during rendering.</summary>
    [Fact]
    public void Render_WhenMarkupIsMalformed_PreservesLiteralContent()
    {
        ControlText text = new("<unknown <b>x");
        new LayoutEngine().Layout(text, new Size(40, 1));
        using Frame frame = new(new Size(40, 1));

        Should.NotThrow(() => text.Render(frame.Canvas));

        FrameOracle.Get(frame, default).ShouldBe("<");
        text.DesiredSize.Width.ShouldBe(13);
    }

    /// <summary>Verifies dynamic text escapes into literal visible content.</summary>
    [Fact]
    public void Escape_WhenAssignedToContent_RendersLiteralText()
    {
        ControlText text = new(ControlText.Escape(@"a < b\c"));
        new LayoutEngine().Layout(text, new Size(20, 1));
        using Frame frame = new(new Size(20, 1));

        text.Render(frame.Canvas);

        text.DesiredSize.Width.ShouldBe(7);
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("<");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("\\");
    }

    /// <summary>Verifies markup facets compose into exact semantic cell metadata.</summary>
    [Fact]
    public void Render_WhenContentIsMarked_AppliesCompleteSemanticStyle()
    {
        ControlText text = new(
            "<fg=green><bg=#102030><u=curly><uc=#ffaf00><link=https://example.test>x</link></uc></u></bg></fg>");
        new LayoutEngine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        var style = frame.GetCell(default).Style;
        style.Foreground.ShouldBe(ReferenceColors.Get(2));
        style.Background.ShouldBe(Color.Rgb(16, 32, 48));
        style.Underline.ShouldBe(Underline.Curly);
        style.UnderlineColor.ShouldBe(ReferenceColors.Get(214));
        style.Hyperlink.ShouldBe("https://example.test");
    }

    /// <summary>Verifies an unescaped ampersand in a link tag's target survives mnemonic collapsing
    /// intact instead of corrupting the tag into literal markup syntax. UseMnemonic must scan
    /// mnemonic markers with tag-boundary awareness, so the query-string ampersand inside
    /// &lt;link=...&gt; is never mistaken for an access-key marker.</summary>
    [Fact]
    public void Render_WhenMnemonicContentHasUnescapedAmpersandInLinkTarget_PreservesTagAndHyperlink()
    {
        const string content = "<link=https://x?a=1&b=2>Click here</link>";
        ControlText withMnemonic = new(content) { UseMnemonic = true };
        ControlText withoutMnemonic = new(content);
        var engine = new LayoutEngine();
        engine.Layout(withMnemonic, new Size(60, 1));
        engine.Layout(withoutMnemonic, new Size(60, 1));
        using Frame frame = new(new Size(60, 1));
        using Frame reference = new(new Size(60, 1));

        withMnemonic.Render(frame.Canvas);
        withoutMnemonic.Render(reference.Canvas);

        for (var index = 0; index < "Click here".Length; index++)
        {
            var point = new Point(index, 0);
            FrameOracle.Get(frame, point).ShouldBe(FrameOracle.Get(reference, point));
        }

        FrameOracle.Get(frame, default).ShouldBe("C");
        frame.GetCell(default).Style.Hyperlink.ShouldBe("https://x?a=1&b=2");
        FrameOracle.Get(frame, new Point("Click here".Length, 0)).ShouldBe(string.Empty);
    }

    /// <summary>Verifies a markup boundary inside one grapheme never splits its cell ownership.</summary>
    [Fact]
    public void Render_WhenStyleBoundarySplitsGrapheme_UsesStyleAtClusterStart()
    {
        ControlText text = new("<red>e</red><blue>\u0301</blue>");
        new LayoutEngine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("e\u0301");
        frame.GetCell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(1));
    }

    /// <summary>Verifies adjacent multi-character style runs each render their own exact style and
    /// content, proving RenderLine's per-run batching draws each run's characters at
    /// the correct advancing position instead of only the first character of a batched run.</summary>
    [Fact]
    public void Render_WhenLineHasMultipleAdjacentStyledRuns_RendersEachRunAtItsCorrectPosition()
    {
        ControlText text = new("<red>abc</red><blue>de</blue>fg");
        new LayoutEngine().Layout(text, new Size(20, 1));
        using Frame frame = new(new Size(20, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("b");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("c");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("d");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("e");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("f");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("g");
        var redForeground = frame.GetCell(new Point(0, 0)).Style.Foreground;
        var blueForeground = frame.GetCell(new Point(3, 0)).Style.Foreground;
        var plainForeground = frame.GetCell(new Point(6, 0)).Style.Foreground;
        frame.GetCell(new Point(1, 0)).Style.Foreground.ShouldBe(redForeground);
        frame.GetCell(new Point(2, 0)).Style.Foreground.ShouldBe(redForeground);
        frame.GetCell(new Point(4, 0)).Style.Foreground.ShouldBe(blueForeground);
        blueForeground.ShouldNotBe(redForeground);
        plainForeground.ShouldNotBe(redForeground);
        plainForeground.ShouldNotBe(blueForeground);
    }

    /// <summary>Verifies an unstyled tab advances to a line-relative four-cell stop, matching the
    /// stops Layout reserves.</summary>
    [Fact]
    public void Render_WhenContentContainsUnstyledTab_AdvancesToLineRelativeFourCellStop()
    {
        ControlText text = new("a\tb");
        new LayoutEngine().Layout(text, new Size(6, 1));
        using Frame frame = new(new Size(6, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe(string.Empty);
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe(string.Empty);
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe(string.Empty);
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("b");
    }

    /// <summary>Verifies a tab following a style-run boundary at a non-multiple-of-four offset still
    /// advances to the line-relative stop instead of a stop relative to the run's own origin, which
    /// previously shifted trailing text.</summary>
    [Fact]
    public void Render_WhenTabFollowsStyleBoundaryAtNonMultipleOfFour_AdvancesToLineRelativeStop()
    {
        ControlText text = new("ab<red>\tc</red>");
        new LayoutEngine().Layout(text, new Size(24, 1));
        using Frame frame = new(new Size(24, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("b");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe(string.Empty);
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe(string.Empty);
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("c");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe(string.Empty);
    }

    /// <summary>Verifies a tab following a style-run boundary does not push trailing content past
    /// bounds Layout measured as sufficient, which previously clipped it away entirely.</summary>
    [Fact]
    public void Render_WhenTabFollowsStyleBoundaryAndWidthMatchesLayout_DoesNotClipTrailingContent()
    {
        ControlText text = new("ab<red>\tc</red>") { HorizontalAlignment = HorizontalAlignment.Left };
        new LayoutEngine().Layout(text, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("c");
    }

    /// <summary>Verifies markup blink tags override an incompatible inherited blink kind.</summary>
    [Fact]
    public void Render_WhenMarkupOverridesBlink_ProducesValidLatestBlinkStyle()
    {
        ControlText text = new("<rapidblink>x</rapidblink>")
        {
            Face = AppearanceTestValues.Face(attributes: TerminalAttributes.Blink)
        };
        new LayoutEngine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        Should.NotThrow(() => text.Render(frame.Canvas));

        frame.GetCell(default).Style.Attributes.ShouldBe(TerminalAttributes.RapidBlink);
    }

    /// <summary>Verifies a typed markup underline overrides an inherited legacy underline.</summary>
    [Fact]
    public void Render_WhenMarkupOverridesUnderline_UsesMarkupShape()
    {
        ControlText text = new("<u=dashed>x</u>")
        {
            Face = AppearanceTestValues.Face(attributes: TerminalAttributes.Underline)
        };
        new LayoutEngine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        var style = frame.GetCell(default).Style;
        style.Attributes.ShouldBe(TerminalAttributes.None);
        style.Underline.ShouldBe(Underline.Dashed);
    }

    /// <summary>Verifies text layout is not re-formatted when arrange width accommodates all lines.</summary>
    [Fact]
    public void EnsureLayout_WhenArrangeWidthExceedsMeasuredContent_SkipsReformat()
    {
        var text = new ControlText("hello")
        {
            TextAlignment = Alignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Measure at unconstrained width; arrange at a finite width that exceeds
        // the natural content. The optimization skips a redundant Format() call
        // and only re-aligns.
        text.Measure(new Constraint(width: null, height: 1));
        text.DesiredSize.ShouldBe(new Size(5, 1));

        text.Arrange(new Rect(0, 0, 20, 1));

        text.Lines.Length.ShouldBe(1);
        text.Lines.Span[0].Cells.ShouldBe(5);
        text.Lines.Span[0].Leading.ShouldBe(7);

        // Second cycle: measure/arrange widths swap back and forth.
        text.Invalidate(Invalidation.All);
        text.Measure(new Constraint(width: null, height: 1));
        text.Arrange(new Rect(0, 0, 40, 1));

        text.Lines.Span[0].Cells.ShouldBe(5);
        text.Lines.Span[0].Leading.ShouldBe(17);
    }

    /// <summary>Verifies widening past a previous Overflow.Ellipsis truncation reformats to the
    /// full text instead of reusing the stale truncated line. Ellipsis routinely leaves a line's
    /// Cells below the arrange width after truncation (word-boundary snap-back reserves the
    /// ellipsis cell), so the width>=_measuredMaxCells skip-reformat guard could mistake that gap
    /// for "nothing was truncated" and stay stuck re-aligning the truncated content forever, even
    /// once the arrange width is wide enough to show everything.</summary>
    [Fact]
    public void EnsureLayout_WhenEllipsisArrangeWidensPastTruncation_ShowsFullText()
    {
        const string content = "The quick brown fox jumps over the lazy dog";
        var text = new ControlText(content)
        {
            Overflow = Overflow.Ellipsis,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        text.Measure(new Constraint(width: 12, height: 1));

        text.Lines.Length.ShouldBe(1);
        var narrow = text.Lines.Span[0];
        narrow.HasEllipsis.ShouldBeTrue();
        content.AsSpan(narrow.Offset, narrow.Length).ToString().ShouldBe("The quick");

        text.Arrange(new Rect(0, 0, 50, 1));

        var wide = text.Lines.Span[0];
        wide.HasEllipsis.ShouldBeFalse();
        wide.Cells.ShouldBe(43);
        content.AsSpan(wide.Offset, wide.Length).ToString().ShouldBe(content);
    }

    /// <summary>Verifies widening past a previous Overflow.Clip truncation reformats to the full
    /// text instead of reusing the stale truncated line. A wide grapheme that does not fit the
    /// last available cell leaves Clip's Cells below the arrange width too (the same shape of gap
    /// as Ellipsis's word-boundary snap-back), so the fast path must not treat Clip as safe to
    /// skip either.</summary>
    [Fact]
    public void EnsureLayout_WhenClipArrangeWidensPastTruncation_ShowsFullText()
    {
        const string content = "ab界c";
        var text = new ControlText(content)
        {
            Overflow = Overflow.Clip,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        text.Measure(new Constraint(width: 3, height: 1));

        text.Lines.Length.ShouldBe(1);
        var narrow = text.Lines.Span[0];
        narrow.HasEllipsis.ShouldBeFalse();
        narrow.Cells.ShouldBe(2);
        content.AsSpan(narrow.Offset, narrow.Length).ToString().ShouldBe("ab");

        text.Arrange(new Rect(0, 0, 10, 1));

        var wide = text.Lines.Span[0];
        wide.Cells.ShouldBe(5);
        content.AsSpan(wide.Offset, wide.Length).ToString().ShouldBe(content);
    }

    /// <summary>Verifies repeated widening after an Overflow.Ellipsis truncation reformats at
    /// every step rather than getting stuck on the first stale line. _measuredMaxCells only ever
    /// updates inside Format(), so a fast path that wrongly skips Format() on the first widen
    /// would carry the ORIGINAL narrow-width measurement forward and could keep passing its own
    /// "already fits" check on every later widen too, permanently truncating the text.</summary>
    [Fact]
    public void EnsureLayout_WhenEllipsisArrangeWidensRepeatedly_ReformatsAtEachWidth()
    {
        const string content = "The quick brown fox jumps over the lazy dog";
        var text = new ControlText(content)
        {
            Overflow = Overflow.Ellipsis,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        text.Measure(new Constraint(width: 12, height: 1));
        content.AsSpan(text.Lines.Span[0].Offset, text.Lines.Span[0].Length)
            .ToString()
            .ShouldBe("The quick");

        // First widen: the text is still truncated, but the boundary must move — a stale reuse
        // of the width=12 line would incorrectly still read "The quick" here.
        text.Arrange(new Rect(0, 0, 20, 1));
        var medium = text.Lines.Span[0];
        medium.HasEllipsis.ShouldBeTrue();
        content.AsSpan(medium.Offset, medium.Length).ToString().ShouldBe("The quick brown");

        // Second widen: the full text now fits. This only works if _measuredMaxCells was
        // refreshed by the previous widen's reformat rather than left stale from width=12.
        text.Arrange(new Rect(0, 0, 50, 1));
        var wide = text.Lines.Span[0];
        wide.HasEllipsis.ShouldBeFalse();
        content.AsSpan(wide.Offset, wide.Length).ToString().ShouldBe(content);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes compute the effective
    /// disabled state Text's mounted disabled contract depends on.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        var text = new ControlText("Text");
        text.EffectiveIsEnabled.ShouldBeTrue();

        text.IsEnabled = false;
        text.EffectiveIsEnabled.ShouldBeFalse();

        text.IsEnabled = true;
        var stack = new Stack { Children = { text } };
        text.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        text.EffectiveIsEnabled.ShouldBeFalse();
    }
}
