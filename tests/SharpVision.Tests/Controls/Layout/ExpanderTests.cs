// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Expander validation, direct header rendering, layout, events, and ownership.</summary>
public sealed class ExpanderTests
{
    /// <summary>Verifies an Expander starts as a borderless transparent section without caller styling.</summary>
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

    /// <summary>Verifies a disabled Expander refuses Space activation and leaves IsExpanded unchanged.</summary>
    [Fact]
    public void Dispatch_WhenDisabled_RefusesSpaceActivation()
    {
        // Arrange
        var expander = new Expander { IsEnabled = false };
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        // Act
        _ = Router.Route(expander, Events.Key, eventArgs);

        // Assert
        eventArgs.IsHandled.ShouldBeFalse();
        expander.IsExpanded.ShouldBeTrue();
    }

    /// <summary>Verifies defaults and invalid header assignments preserve committed state.</summary>
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidHeader_PreserveDefaults()
    {
        var expander = new Expander();

        expander.Header.ShouldBeNull();
        expander.IsExpanded.ShouldBeTrue();
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

        expander.IsExpanded = false;
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
        expander.ExpandedChanged += (_, _) => states.Add(expander.IsExpanded);

        expander.IsExpanded = false;
        expander.IsExpanded = false;
        expander.IsExpanded = true;

        states.ShouldBe([false, true]);
    }

    /// <summary>Verifies a reentrant property observer owns the final expanded state, content
    /// visibility, and typed event stream.</summary>
    [Fact]
    public void IsExpanded_WhenPropertyObserverRestoresValue_SuppressesStaleExpandedEvent()
    {
        var content = new ProbeControl(new Size(4, 1));
        var expander = new Expander { Content = content };
        var observations = new List<(bool EventValue, bool LiveValue, Visibility ContentVisibility)>();
        expander.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Expander.IsExpanded) && !expander.IsExpanded)
            {
                expander.IsExpanded = true;
            }
        };
        expander.ExpandedChanged += (_, eventArgs) =>
            observations.Add((eventArgs.IsExpanded, expander.IsExpanded, content.Visibility));

        expander.IsExpanded = false;

        expander.IsExpanded.ShouldBeTrue();
        content.Visibility.ShouldBe(Visibility.Visible);
        observations.ShouldBe([(true, true, Visibility.Visible)]);
    }

    /// <summary>Verifies collapsing hides content from the Visibility chain, not only from arranged size,
    /// so a focusable control inside it stops being reachable by keyboard focus.</summary>
    [Fact]
    public void IsExpanded_WhenFalse_CollapsesContentVisibilityAndExcludesItFromFocus()
    {
        var content = new ProbeControl(new Size(4, 2)) { IsFocusable = true };
        var expander = new Expander { HeaderText = "Details", Content = content };
        content.CanFocus.ShouldBeTrue();

        expander.IsExpanded = false;

        content.Visibility.ShouldBe(Visibility.Collapsed);
        content.EffectiveIsVisible.ShouldBeFalse();
        content.CanFocus.ShouldBeFalse();

        expander.IsExpanded = true;

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
            var before = new ProbeControl(new Size(1, 1)) { IsFocusable = true };
            var hidden = new ProbeControl(new Size(1, 1)) { IsFocusable = true };
            var expander = new Expander { HeaderText = "Details", Content = hidden, IsExpanded = false };
            var after = new ProbeControl(new Size(1, 1)) { IsFocusable = true };
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
    /// rather than being forced back to IsVisible.</summary>
    [Fact]
    public void IsExpanded_WhenContentWasAuthoredCollapsed_StaysCollapsedAfterReExpanding()
    {
        var content = new ProbeControl(new Size(4, 2)) { Visibility = Visibility.Collapsed };
        var expander = new Expander { HeaderText = "Details", Content = content, IsExpanded = false };

        expander.IsExpanded = true;

        content.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies replacing collapsed content releases the previous child and retains only the replacement.</summary>
    [Fact]
    public void Content_WhenReplacedWhileCollapsed_TransfersOwnershipWithoutExpanding()
    {
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var expander = new Expander { IsExpanded = false, Content = first };

        expander.Content = second;

        expander.IsExpanded.ShouldBeFalse();
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
        expander.ExpandedChanged += (_, eventArgs) => captured.Add(eventArgs.IsExpanded);

        // Act
        expander.IsExpanded = false;
        expander.IsExpanded = true;

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
        _ = Should.Throw<ObjectDisposedException>(() => expander.IsExpanded = false);
        _ = Should.Throw<ObjectDisposedException>(() => expander.HeaderText = "Test");
        _ = Should.Throw<ObjectDisposedException>(() => expander.Style = ExpanderStyle.Default);
    }

    /// <summary>Verifies Style mutation requires dispatcher affinity once attached.</summary>
    [Fact]
    public async Task Style_WhenAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var expander = new Expander();
        await dispatcher.InvokeAsync(
            () => expander.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => expander.Style = ExpanderStyle.Default);

        expander.Style.ShouldBeNull();
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

    /// <summary>Verifies the header label's arranged X position stays in lockstep with the
    /// content's arranged X position for a non-default ContentIndent, since both now derive from
    /// the same style value - previously the header used a fixed two-cell chrome regardless of
    /// ContentIndent, desyncing the two whenever ContentIndent departed from its
    /// coincidentally-matching default.</summary>
    [Fact]
    public void HeaderChromeWidth_WhenContentIndentIsNonDefault_MatchesContentIndentation()
    {
        // Arrange
        var header = new ProbeControl(new Size(4, 1));
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            Header = header,
            Content = content,
            Style = ExpanderStyle.Default with { ContentIndent = 4 },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        new LayoutEngine().Layout(expander, new Size(20, 8));

        // Assert both the header label and the content sit the same four cells from the
        // expander's own left edge
        (header.Bounds.X - expander.Bounds.X).ShouldBe(4);
        (content.Bounds.X - expander.Bounds.X).ShouldBe(4);
        header.Bounds.X.ShouldBe(content.Bounds.X);
    }

    /// <summary>Verifies a zero ContentIndent still floors the header slot at one cell, so the
    /// disclosure glyph keeps a paintable column instead of being squeezed to zero width
    /// alongside the label.</summary>
    [Fact]
    public void HeaderChromeWidth_WhenContentIndentIsZero_FloorsHeaderSlotAtOneCell()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ProbeControl(new Size(4, 2)),
            Style = ExpanderStyle.Default with { ContentIndent = 0 },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        new LayoutEngine().Layout(expander, new Size(20, 8));

        // Assert the header slot still starts one cell after the expander's own left edge,
        // leaving the disclosure glyph a paintable column instead of collapsing chrome to zero
        var header = expander.Header.ShouldNotBeNull();
        (header.Bounds.X - expander.Bounds.X).ShouldBe(1);

        using var frame = new Frame(expander.DesiredSize);
        expander.Render(frame.Canvas);
        FrameOracle.Get(frame, default).ShouldBe("▼");
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
            IsExpanded = true,
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
            IsExpanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(expander, new Size(10, 3));

        content.Bounds.Width.ShouldBe(3);
    }

    /// <summary>Verifies a collapsed header contributes nothing to the header-content width, leaving
    /// only the fixed disclosure-glyph chrome - distinct from the expand-state↔Visibility restoration
    /// the rest of this file already covers.</summary>
    [Fact]
    public void Layout_WhenHeaderIsCollapsed_ExcludesItFromHeaderWidth()
    {
        var header = new ProbeControl(new Size(20, 1)) { Visibility = Visibility.Collapsed };
        var expander = new Expander
        {
            Header = header,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        new LayoutEngine().Layout(expander, new Size(30, 8));

        expander.DesiredSize.ShouldBe(new Size(2, 1));
        header.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies content collapsed directly through Visibility - while IsExpanded stays true,
    /// never touching the expand/collapse toggle - measures header-only geometry identical to the
    /// IsExpanded=false baseline established above, proving the exclusion at Expander.cs's own
    /// content-width oracle rather than the already-covered IsExpanded↔Visibility restoration.</summary>
    [Fact]
    public void Layout_WhenContentIsCollapsedWhileExpanded_MeasuresHeaderOnlyGeometry()
    {
        var content = new ProbeControl(new Size(20, 6)) { Visibility = Visibility.Collapsed };
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = content,
            IsExpanded = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        new LayoutEngine().Layout(expander, new Size(30, 8));

        expander.IsExpanded.ShouldBeTrue("collapsing content directly must not itself change Expanded");
        // Matches the IsExpanded=false DesiredSize this file's own baseline test establishes for the
        // same header text, proving collapsed content is excluded from both dimensions.
        expander.DesiredSize.ShouldBe(new Size(9, 1));
        content.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies hidden content while expanded keeps the exact measured and arranged
    /// geometry visible content of the same intrinsic size receives, and only excludes rendering.</summary>
    [Fact]
    public void Layout_WhenContentIsHiddenWhileExpanded_MatchesVisibleGeometryButExcludesRendering()
    {
        var content = new ProbeControl(new Size(4, 2)) { Visibility = Visibility.Hidden };
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = content,
            IsExpanded = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        new LayoutEngine().Layout(expander, new Size(30, 8));

        // Same geometry Layout_WhenExpansionChanges_ExcludesAndRestoresRetainedContent proves for
        // visible content of the same intrinsic size.
        expander.DesiredSize.ShouldBe(new Size(9, 3));
        content.Bounds.ShouldBe(new Rect(2, 1, 7, 2));
        using var frame = new Frame(new Size(9, 3));

        expander.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBeEmpty();
    }
}
