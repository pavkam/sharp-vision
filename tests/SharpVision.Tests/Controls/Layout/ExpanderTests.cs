// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Expander validation, direct header rendering, layout, events, and ownership.</summary>
public sealed class ExpanderTests
{
    /// <summary>Verifies an Expander starts as a borderless transparent section without caller styling.</summary>
    [ComponentUnitEvidence(typeof(Expander))]
    [Fact]
    public void Constructor_WhenCreated_UsesBorderlessTransparentDefaults()
    {
        // Arrange and act
        var expander = new Expander();

        // Assert
        expander.ActualBorder.Sides.ShouldBe(BorderSide.None);
        expander.Face.Background.ShouldBe(SemanticColor.Control);
        expander.ActualStyle.ContentIndent.ShouldBe(2);
    }

    /// <summary>Verifies defaults and invalid header assignments preserve committed state.</summary>
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidHeader_PreserveDefaults()
    {
        var expander = new Expander();

        expander.Header.ShouldBeNull();
        expander.Expanded.ShouldBeTrue();
        expander.Content.ShouldBeNull();

        _ = Should.Throw<ArgumentNullException>(() => expander.HeaderText = null!);

        expander.Header.ShouldBeNull();
    }

    /// <summary>Verifies collapse excludes content geometry without releasing caller ownership.</summary>
    [Fact]
    public void Layout_WhenExpansionChanges_ExcludesAndRestoresRetainedContent()
    {
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var engine = new LayoutEngine();

        engine.Layout(expander, new Size(20, 8));

        expander.DesiredSize.ShouldBe(new Size(9, 3));
        content.Bounds.ShouldBe(new Rect(2, 1, 7, 2));

        expander.Expanded = false;
        engine.Layout(expander, new Size(20, 8));

        expander.DesiredSize.ShouldBe(new Size(9, 1));
        content.Bounds.ShouldBe(default);
        content.Parent.ShouldBeSameAs(expander);
        using var frame = new Frame(new Size(9, 1));
        expander.Render(frame.Canvas);
        FrameOracle.Get(frame, default).ShouldBe("▶");
    }

    /// <summary>Verifies changed expansion publishes committed state once and identical assignment is a no-op.</summary>
    [Fact]
    public void IsExpanded_WhenValueChanges_RaisesOnePostCommitEvent()
    {
        var expander = new Expander();
        var states = new List<bool>();
        expander.ExpandedChanged += (_, _) => states.Add(expander.Expanded);

        expander.Expanded = false;
        expander.Expanded = false;
        expander.Expanded = true;

        states.ShouldBe([false, true]);
    }

    /// <summary>Verifies collapsing hides content from the Visibility chain, not only from arranged size,
    /// so a focusable control inside it stops being reachable by keyboard focus.</summary>
    [Fact]
    public void IsExpanded_WhenFalse_CollapsesContentVisibilityAndExcludesItFromFocus()
    {
        var content = new ProbeControl(new Size(4, 2)) { Focusable = true };
        var expander = new Expander { HeaderText = "Details", Content = content };
        content.CanFocus.ShouldBeTrue();

        expander.Expanded = false;

        content.Visibility.ShouldBe(Visibility.Collapsed);
        content.EffectiveIsVisible.ShouldBeFalse();
        content.CanFocus.ShouldBeFalse();

        expander.Expanded = true;

        content.Visibility.ShouldBe(Visibility.Visible);
        content.CanFocus.ShouldBeTrue();
    }

    /// <summary>Verifies Tab traversal skips a focusable control inside collapsed Expander content.</summary>
    [Fact]
    public async Task Focus_WhenExpanderIsCollapsed_TabTraversalSkipsHiddenContentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var before = new ProbeControl(new Size(1, 1)) { Focusable = true };
            var hidden = new ProbeControl(new Size(1, 1)) { Focusable = true };
            var expander = new Expander { HeaderText = "Details", Content = hidden, Expanded = false };
            var after = new ProbeControl(new Size(1, 1)) { Focusable = true };
            var panel = new Stack();
            panel.Children.Add(before);
            panel.Children.Add(expander);
            panel.Children.Add(after);
            panel.Attach(dispatcher);
            using FocusManager focus = new(panel);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(before);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(expander);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(after);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a caller-collapsed content control stays collapsed after re-expanding,
    /// rather than being forced back to Visible.</summary>
    [Fact]
    public void IsExpanded_WhenContentWasAuthoredCollapsed_StaysCollapsedAfterReExpanding()
    {
        var content = new ProbeControl(new Size(4, 2)) { Visibility = Visibility.Collapsed };
        var expander = new Expander { HeaderText = "Details", Content = content, Expanded = false };

        expander.Expanded = true;

        content.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies replacing collapsed content releases the previous child and retains only the replacement.</summary>
    [Fact]
    public void Content_WhenReplacedWhileCollapsed_TransfersOwnershipWithoutExpanding()
    {
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var expander = new Expander { Expanded = false, Content = first };

        expander.Content = second;

        expander.Expanded.ShouldBeFalse();
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(expander);
        expander.Content.ShouldBeSameAs(second);
    }

    /// <summary>Verifies a negative content indent is rejected before mutation, now by the style's
    /// own init accessor rather than a control-side setter.</summary>
    [Fact]
    public void ContentIndent_WhenNegative_ThrowsBeforeMutation()
    {
        // Arrange
        var expander = new Expander();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => expander.Style = ExpanderStyle.Default with { ContentIndent = -1 });
        expander.ActualStyle.ContentIndent.ShouldBe(2);
    }

    /// <summary>Verifies changing ContentIndent shifts content bounds.</summary>
    [Fact]
    public void ContentIndent_WhenChanged_ShiftsContentBounds()
    {
        // Arrange
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = content,
            Style = ExpanderStyle.Default with { ContentIndent = 4 },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var engine = new LayoutEngine();

        // Act
        engine.Layout(expander, new Size(20, 8));

        // Assert
        content.Bounds.X.ShouldBe(4);
    }

    /// <summary>Verifies a local style overrides the code-owned glyphs and assigning null returns the
    /// expander to theme ownership - the escape hatch ResetGlyphs() never actually provided, since it
    /// restored code-owned values rather than theme-owned ones.</summary>
    [Fact]
    public void Glyphs_WhenCustomized_OverrideDefaultsAndResetRestores()
    {
        // Arrange
        var expander = new Expander();
        var defaultCollapsed = expander.ActualStyle.CollapsedGlyph;
        var defaultExpanded = expander.ActualStyle.ExpandedGlyph;

        // Act
        expander.Style = ExpanderStyle.Default with
        {
            CollapsedGlyph = new Rune('+'),
            ExpandedGlyph = new Rune('-')
        };

        // Assert custom
        expander.ActualStyle.CollapsedGlyph.ShouldBe(new Rune('+'));
        expander.ActualStyle.ExpandedGlyph.ShouldBe(new Rune('-'));

        // Act reset
        expander.Style = null;

        // Assert restored
        expander.ActualStyle.CollapsedGlyph.ShouldBe(defaultCollapsed);
        expander.ActualStyle.ExpandedGlyph.ShouldBe(defaultExpanded);
    }

    /// <summary>Verifies ExpandedChanged event args carry the committed state.</summary>
    [Fact]
    public void ExpandedChanged_WhenFired_EventArgsCarryCommittedState()
    {
        // Arrange
        var expander = new Expander();
        var captured = new List<bool>();
        expander.ExpandedChanged += (_, eventArgs) => captured.Add(eventArgs.Expanded);

        // Act
        expander.Expanded = false;
        expander.Expanded = true;

        // Assert
        captured.ShouldBe([false, true]);
    }

    /// <summary>Verifies disposing the expander prevents mutation and clears event handlers.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var expander = new Expander();

        // Act
        expander.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => expander.Expanded = false);
        _ = Should.Throw<ObjectDisposedException>(() => expander.HeaderText = "Test");
    }

    /// <summary>Verifies zero ContentIndent places content at the leading edge.</summary>
    [Fact]
    public void ContentIndent_WhenZero_PlacesContentAtLeadingEdge()
    {
        // Arrange
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = content,
            Style = ExpanderStyle.Default with { ContentIndent = 0 },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        new LayoutEngine().Layout(expander, new Size(20, 8));

        // Assert
        content.Bounds.X.ShouldBe(0);
        content.Bounds.Y.ShouldBe(1);
    }

    /// <summary>Verifies an Expander's single content child always fills its slot regardless of
    /// Width or alignment, matching Grid's own documented ResolvedAxes.Both contract - the same
    /// gap that applies to Expander/GroupBox, which share ResolvedAxes.Both.</summary>
    [Fact]
    public void Expander_WhenContentSetsWidthAndAlignment_StillFillsTheContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Expander itself defaults to Left/Top alignment, so as an unstretched root it would
        // shrink-wrap to its own measured content width - which is itself influenced by
        // content's explicit Width during measure, even though arrange's resolved axes ignore
        // it - creating a circular illusion that Width "worked". Stretching Expander itself to
        // the full given size removes that confound and isolates the arrange-time behavior.
        var expander = new Expander
        {
            Expanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(expander, new Size(10, 3));

        content.Bounds.Width.ShouldBe(10 - expander.ActualStyle.ContentIndent);
    }

    /// <summary>Verifies MaxWidth on an Expander's content still caps the otherwise-filled slot,
    /// mirroring Grid's own Width-ignored-but-MaxWidth-honored asymmetry exactly.</summary>
    [Fact]
    public void Expander_WhenContentSetsMaxWidth_CapsTheFilledContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1)) { MaxWidth = 3 };
        var expander = new Expander
        {
            Expanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(expander, new Size(10, 3));

        content.Bounds.Width.ShouldBe(3);
    }
}
