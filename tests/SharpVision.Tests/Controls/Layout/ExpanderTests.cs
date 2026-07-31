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
        expander.Face.Background.ShouldBe(ThemeColor.Control);
        expander.ContentIndent.ShouldBe(2);
    }

    /// <summary>Verifies defaults and invalid header assignments preserve committed state.</summary>
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidHeader_PreserveDefaults()
    {
        var expander = new Expander();

        expander.Header.ShouldBeEmpty();
        expander.IsExpanded.ShouldBeTrue();
        expander.Content.ShouldBeNull();

        _ = Should.Throw<ArgumentNullException>(() => expander.Header = null!);
        _ = Should.Throw<ArgumentException>(() => expander.Header = "bad\nheader");

        expander.Header.ShouldBeEmpty();
    }

    /// <summary>Verifies collapse excludes content geometry without releasing caller ownership.</summary>
    [Fact]
    public void Layout_WhenExpansionChanges_ExcludesAndRestoresRetainedContent()
    {
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            Header = "Details",
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

    /// <summary>Verifies collapsing hides content from the Visibility chain, not only from arranged size,
    /// so a focusable control inside it stops being reachable by keyboard focus.</summary>
    [Fact]
    public void IsExpanded_WhenFalse_CollapsesContentVisibilityAndExcludesItFromFocus()
    {
        var content = new ProbeControl(new Size(4, 2)) { Focusable = true };
        var expander = new Expander { Header = "Details", Content = content };
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
            var before = new ProbeControl(new Size(1, 1)) { Focusable = true };
            var hidden = new ProbeControl(new Size(1, 1)) { Focusable = true };
            var expander = new Expander { Header = "Details", Content = hidden, IsExpanded = false };
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
        var expander = new Expander { Header = "Details", Content = content, IsExpanded = false };

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

    /// <summary>Verifies negative ContentIndent is rejected before mutation.</summary>
    [Fact]
    public void ContentIndent_WhenNegative_ThrowsBeforeMutation()
    {
        // Arrange
        var expander = new Expander();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => expander.ContentIndent = -1);
        expander.ContentIndent.ShouldBe(2);
    }

    /// <summary>Verifies changing ContentIndent shifts content bounds.</summary>
    [Fact]
    public void ContentIndent_WhenChanged_ShiftsContentBounds()
    {
        // Arrange
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            Header = "Details",
            Content = content,
            ContentIndent = 4,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var engine = new LayoutEngine();

        // Act
        engine.Layout(expander, new Size(20, 8));

        // Assert
        content.Bounds.X.ShouldBe(4);
    }

    /// <summary>Verifies custom glyphs override defaults and ResetGlyphs restores them.</summary>
    [Fact]
    public void Glyphs_WhenCustomized_OverrideDefaultsAndResetRestores()
    {
        // Arrange
        var expander = new Expander();
        var defaultCollapsed = expander.CollapsedGlyph;
        var defaultExpanded = expander.ExpandedGlyph;

        // Act
        expander.CollapsedGlyph = new Rune('+');
        expander.ExpandedGlyph = new Rune('-');

        // Assert custom
        expander.CollapsedGlyph.ShouldBe(new Rune('+'));
        expander.ExpandedGlyph.ShouldBe(new Rune('-'));

        // Act reset
        expander.ResetGlyphs();

        // Assert restored
        expander.CollapsedGlyph.ShouldBe(defaultCollapsed);
        expander.ExpandedGlyph.ShouldBe(defaultExpanded);
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
        _ = Should.Throw<ObjectDisposedException>(() => expander.Header = "Test");
    }

    /// <summary>Verifies zero ContentIndent places content at the leading edge.</summary>
    [Fact]
    public void ContentIndent_WhenZero_PlacesContentAtLeadingEdge()
    {
        // Arrange
        var content = new ProbeControl(new Size(4, 2));
        var expander = new Expander
        {
            Header = "Details",
            Content = content,
            ContentIndent = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        new LayoutEngine().Layout(expander, new Size(20, 8));

        // Assert
        content.Bounds.X.ShouldBe(0);
        content.Bounds.Y.ShouldBe(1);
    }
}
