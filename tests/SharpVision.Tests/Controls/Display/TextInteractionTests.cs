// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Text markup composition and recovery, every overflow mode, alignment, wide and
/// combining graphemes at clip edges, semantic selection, runtime face and content changes, degenerate
/// bounds, reflow on resize, and access-key targeting through mounted surfaces.</summary>
public sealed class TextInteractionTests
{
    /// <summary>Verifies nested and overlapping tags compose per cell and the generic close ends only
    /// the most recently opened facet.</summary>
    [Fact]
    public async Task Render_WhenTagsNestAndOverlap_ComposesFacetsPerCellAsync()
    {
        // Arrange
        var text = new ControlText("<b><i>ab</i>c</b>d<u>e</><s>f</s>");

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("abcdef");
        Attributes(surface, 0).ShouldBe(TerminalAttributes.Bold | TerminalAttributes.Italic);
        Attributes(surface, 1).ShouldBe(TerminalAttributes.Bold | TerminalAttributes.Italic);
        Attributes(surface, 2).ShouldBe(TerminalAttributes.Bold);
        Attributes(surface, 3).ShouldBe(TerminalAttributes.None);
        Attributes(surface, 4).ShouldBe(TerminalAttributes.Underline);
        Attributes(surface, 5).ShouldBe(TerminalAttributes.Strike);
    }

    /// <summary>Verifies malformed markup stays literal and stray closes are ignored, so an invalid
    /// tag never eats visible content.</summary>
    [Theory]
    [InlineData("<bogus>x", "<bogus>x")]
    [InlineData("<b<i>x", "<b<i>x")]
    [InlineData("<bx", "<bx")]
    [InlineData("</b>x", "x")]
    [InlineData("<fg=#zz>x", "<fg=#zz>x")]
    [InlineData("a > b", "a > b")]
    [InlineData("<b>open", "open")]
    public async Task Render_WhenMarkupIsInvalid_RecoversLiterallyAsync(string content, string expected)
    {
        // Arrange
        var text = new ControlText(content);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(expected.PadRight(10));
    }

    /// <summary>Verifies escaped metacharacters and Text.Escape output render literally while an
    /// adjacent real tag still applies.</summary>
    [Fact]
    public async Task Render_WhenContentIsEscaped_RendersLiteralMetacharactersAsync()
    {
        // Arrange
        var text = new ControlText($"<b>{ControlText.Escape("<x>\\")}</b>\\<u>");

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("<x>\\<u> ");
        Attributes(surface, 0).ShouldBe(TerminalAttributes.Bold);
        Attributes(surface, 3).ShouldBe(TerminalAttributes.Bold);
        Attributes(surface, 4).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies each overflow policy formats the same content differently at a narrow
    /// width: word wrap, anywhere wrap, clip, ellipsis, and visible (clipped only by bounds).</summary>
    [Theory]
    [InlineData(Overflow.Wrap, "alpha ", "beta  ")]
    [InlineData(Overflow.WrapAnywhere, "alpha ", "beta  ")]
    [InlineData(Overflow.Clip, "alpha ", "      ")]
    [InlineData(Overflow.Ellipsis, "alpha…", "      ")]
    [InlineData(Overflow.Visible, "alpha ", "      ")]
    public async Task Render_WhenOverflowPolicyVaries_FormatsAccordinglyAsync(Overflow overflow, string firstRow, string secondRow)
    {
        // Arrange
        var text = new ControlText("alpha beta") { Overflow = overflow };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(6, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(firstRow + "\n" + secondRow);
    }

    /// <summary>Verifies WrapAnywhere breaks inside a word that word wrap could not place.</summary>
    [Fact]
    public async Task Render_WhenOverflowIsWrapAnywhere_BreaksInsideLongWordsAsync()
    {
        // Arrange
        var text = new ControlText("abcdefgh") { Overflow = Overflow.WrapAnywhere };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(3, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("abc\ndef\ngh ");
    }

    /// <summary>Verifies Start, Center, and End alignment place each line's leading cells and that
    /// changing alignment after layout realigns without changing the line content.</summary>
    [Fact]
    public async Task TextAlignment_WhenChangedAfterLayout_RealignsEveryLineAsync()
    {
        // Arrange
        var text = new ControlText("ab\ncdef")
        {
            Overflow = Overflow.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("ab    \ncdef  ");

        // Act and assert
        await surface.UpdateAsync(() => text.TextAlignment = Alignment.End, "align to the end");
        surface.ShouldRender("    ab\n  cdef");
        await surface.UpdateAsync(() => text.TextAlignment = Alignment.Center, "align to the center");
        surface.ShouldRender("  ab  \n cdef ");
        await surface.UpdateAsync(() => text.TextAlignment = Alignment.Start, "align back to the start");
        surface.ShouldRender("ab    \ncdef  ");
    }

    /// <summary>Verifies a wide grapheme, an emoji, and a combining sequence are never split at a
    /// clip or ellipsis edge.</summary>
    [Theory]
    [InlineData("a界b", Overflow.Clip, 2, "a ")]
    [InlineData("x👍y", Overflow.Clip, 2, "x ")]
    [InlineData("éxyz", Overflow.Clip, 2, "éx")]
    [InlineData("a界b", Overflow.Ellipsis, 3, "a… ")]
    [InlineData("界界", Overflow.Ellipsis, 3, "界…")]
    [InlineData("界界", Overflow.Wrap, 2, "界")]
    public async Task Render_WhenGraphemeMeetsTheEdge_NeverSplitsItAsync(string content, Overflow overflow, int width, string firstRow)
    {
        // Arrange
        var text = new ControlText(content) { Overflow = overflow };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(width, 2),
            TestContext.Current.CancellationToken);

        // Assert
        var row = new StringBuilder();

        for (var x = 0; x < width; x++)
        {
            var cell = surface.Cell(new Point(x, 0));

            if (!cell.Continuation)
            {
                _ = row.Append(cell.Text.Length == 0 ? " " : cell.Text);
            }
        }

        row.ToString().ShouldBe(firstRow);
    }

    /// <summary>Verifies wrapping a wide-only string at a 2-cell width places one wide cluster per
    /// line, keeping the continuation cell owned by its lead.</summary>
    [Fact]
    public async Task Render_WhenWideClustersWrap_KeepsOneClusterPerLineAsync()
    {
        // Arrange
        var text = new ControlText("界界界") { Overflow = Overflow.Wrap };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(2, 3),
            TestContext.Current.CancellationToken);

        // Assert
        for (var y = 0; y < 3; y++)
        {
            surface.Cell(new Point(0, y)).Text.ShouldBe("界");
            surface.Cell(new Point(1, y)).Continuation.ShouldBeTrue();
        }
    }

    /// <summary>Verifies pointer selection over marked-up text selects visible words only: tags never
    /// leak into the selected text and the selection paints the marked cells.</summary>
    [Fact]
    public async Task Selection_WhenEnabledOverMarkup_SelectsVisibleTextOnlyAsync()
    {
        // Arrange
        var text = new ControlText("<b>alpha</b> <i>beta</i>");
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { text }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act double-click the second word
        await surface.Pointer.ClickAsync(text, new Point(7, 0));
        await surface.Pointer.ClickAsync(text, new Point(7, 0));

        // Assert
        var word = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.SelectedText,
            TestContext.Current.CancellationToken);
        word.ShouldBe("beta");
        var selectedBackground = TerminalPalette.Project(
            surface.Application.Theme.ResolveColor(SemanticColor.SelectedControl),
            ColorDepth.Basic16);
        surface.Cell(new Point(6, 0)).Style.Background.ShouldBe(selectedBackground);
        surface.Cell(new Point(0, 0)).Style.Background.ShouldNotBe(selectedBackground);

        // Act drag across both words
        await surface.Pointer.MoveToAsync(text, new Point(1, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(text, new Point(8, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert
        var range = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.SelectedText,
            TestContext.Current.CancellationToken);
        range.ShouldBe("lpha be");
    }

    /// <summary>Verifies drag selection across wrapped lines follows the visible line order.</summary>
    [Fact]
    public async Task Selection_WhenDraggedAcrossWrappedLines_FollowsVisibleOrderAsync()
    {
        // Arrange
        var text = new ControlText("alpha beta") { Overflow = Overflow.Wrap };
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { text }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(6, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(text, new Point(3, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(text, new Point(2, 1));
        await surface.Pointer.ReleaseAsync();

        // Assert
        var selected = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.SelectedText,
            TestContext.Current.CancellationToken);
        selected.ShouldStartWith("ha");
        selected.ShouldEndWith("be");
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a local Face assigned after layout repaints the foreground and clearing it
    /// restores the themed color, while markup colors still win over the face.</summary>
    [Fact]
    public async Task Face_WhenAssignedAfterLayout_RepaintsAndMarkupStillWinsAsync()
    {
        // Arrange
        var text = new ControlText("ab<fg=#ff0000>c</fg>");
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(3, 1),
            TestContext.Current.CancellationToken);
        var themed = surface.Cell(new Point(0, 0)).Style.Foreground;
        var marked = surface.Cell(new Point(2, 0)).Style.Foreground;
        marked.ShouldNotBe(themed);

        // Act
        await surface.UpdateAsync(
            () => text.Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(4)),
            "assign a local face");

        // Assert
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(4));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(4));
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(marked);
        await surface.UpdateAsync(text.ResetFace, "clear the local face");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(themed);
    }

    /// <summary>Verifies one-cell and zero-cell bounds render the first grapheme or nothing without
    /// faulting, and growing the bounds again restores the content.</summary>
    [Fact]
    public async Task Render_WhenBoundsAreOneOrZeroCells_DegradesGracefullyAsync()
    {
        // Arrange
        var text = new ControlText("界xyz") { Overflow = Overflow.Clip };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert one cell cannot host the wide lead
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");

        // Act shrink to zero, then widen
        await surface.UpdateAsync(() => text.Width = Length.Cells(0), "collapse the width to zero");
        text.Bounds.Width.ShouldBe(0);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        await surface.UpdateAsync(() => text.Width = Length.Cells(4), "widen to four cells");
        await surface.ResizeAsync(new Size(4, 1));

        // Assert
        surface.ShouldRender("界xy");
    }

    /// <summary>Verifies a surface resize merges wrapped lines back together and re-splits them.</summary>
    [Fact]
    public async Task ResizeAsync_WhenWidthChanges_ReflowsWrappedContentBothWaysAsync()
    {
        // Arrange
        var text = new ControlText("alpha beta") { Overflow = Overflow.Wrap };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("alpha \nbeta  ");

        // Act widen
        await surface.ResizeAsync(new Size(12, 2));

        // Assert
        surface.ShouldRender("alpha beta  \n            ");

        // Act narrow again
        await surface.ResizeAsync(new Size(5, 2));

        // Assert
        surface.ShouldRender("alpha\nbeta ");
    }

    /// <summary>Verifies TextChanged fires once per distinct content assignment, never for a repeat,
    /// and that clearing the content blanks the surface.</summary>
    [Fact]
    public async Task Content_WhenChangedAfterLayout_FiresOncePerChangeAndRepaintsAsync()
    {
        // Arrange
        var text = new ControlText("one");
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(5, 1),
            TestContext.Current.CancellationToken);
        var changes = 0;
        text.TextChanged += (_, _) => changes++;

        // Act
        await surface.UpdateAsync(() => text.Content = "two", "change the content");
        await surface.UpdateAsync(() => text.Content = "two", "assign the same content");
        surface.ShouldRender("two  ");
        await surface.UpdateAsync(() => text.Content = string.Empty, "clear the content");

        // Assert
        changes.ShouldBe(2);
        surface.ShouldRender("     ");
        _ = Should.Throw<ArgumentNullException>(() => text.Content = null!);
    }

    /// <summary>Verifies a label access key focuses its explicit target, and without a target the
    /// next tab stop after the label.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccessKey_WhenLabelMnemonicMatches_FocusesTargetOrNextTabStopAsync(bool explicitTarget)
    {
        // Arrange
        var before = new Button("Before");
        var label = new ControlText("&Name") { UseMnemonic = true };
        var next = new Button("Next");
        var far = new Button("Far");
        label.AccessKeyTarget = explicitTarget ? far : null;
        var stack = new Stack();
        stack.Children.Add(before);
        stack.Children.Add(label);
        stack.Children.Add(next);
        stack.Children.Add(far);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 12),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(before);
        surface.ShouldHaveFocus(before);
        surface.Cell(new Point(label.Bounds.X, label.Bounds.Y)).Text.ShouldBe("N");
        (surface.Cell(new Point(label.Bounds.X, label.Bounds.Y)).Style.Attributes & TerminalAttributes.Underline)
            .ShouldBe(TerminalAttributes.Underline);

        // Act
        await surface.SendAsync("\x1b[110;3:1u"u8.ToArray(), "Alt+N");

        // Assert
        surface.ShouldHaveFocus(explicitTarget ? far : next);
        label.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies turning UseMnemonic off after layout renders the ampersand literally and the
    /// key no longer matches.</summary>
    [Fact]
    public async Task UseMnemonic_WhenDisabledAfterLayout_RendersAmpersandAndIgnoresKeyAsync()
    {
        // Arrange
        var label = new ControlText("&Name") { UseMnemonic = true };
        var next = new Button("Next");
        var stack = new Stack();
        stack.Children.Add(label);
        stack.Children.Add(next);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 5),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("N");

        // Act
        await surface.UpdateAsync(() => label.UseMnemonic = false, "disable the mnemonic");
        await surface.SendAsync("\x1b[110;3:1u"u8.ToArray(), "Alt+N");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("&");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("N");
        next.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies whitespace at a soft-wrap break hangs off the line that broke instead of
    /// indenting the next line, both when a word fills the line exactly and when several spaces
    /// straddle the edge - while authored leading whitespace at a paragraph start is preserved.</summary>
    [Theory]
    [InlineData("one two", 3, "one|two")]
    [InlineData("one  two", 4, "one |two ")]
    [InlineData("one   two", 3, "one|two")]
    [InlineData("  in", 4, "  in|    ")]
    [InlineData("ab\n cd", 4, "ab  | cd ")]
    public async Task Render_WhenWhitespaceMeetsTheWrapEdge_HangsItOffTheBrokenLineAsync(string content, int width, string expected)
    {
        // Arrange
        var text = new ControlText(content)
        {
            Overflow = Overflow.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(width, 2),
            TestContext.Current.CancellationToken);

        // Assert
        var rows = new string[2];

        for (var y = 0; y < 2; y++)
        {
            var row = new StringBuilder();

            for (var x = 0; x < width; x++)
            {
                var cell = surface.Cell(new Point(x, y));
                _ = row.Append(cell.Text.Length == 0 ? " " : cell.Text);
            }

            rows[y] = row.ToString();
        }

        string.Join("|", rows).ShouldBe(expected);
    }

    private static TerminalAttributes Attributes(ComponentSurface surface, int x) =>
        surface.Cell(new Point(x, 0)).Style.Attributes;
}
