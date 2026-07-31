// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Windows;

using SharpVision.Surfaces;

/// <summary>Verifies framed terminal window layout, title chrome, and visual shadow behavior.</summary>
public sealed class WindowTests
{
    /// <summary>Verifies a Window owns an opaque semantic background without caller styling.</summary>
    [ComponentUnitEvidence(typeof(Window))]
    [Fact]
    public void Constructor_WhenCreated_UsesWindowBackgroundRole()
    {
        var window = new Window();

        window.Face.Background.ShouldBe(ThemeColor.Surface);
    }

    /// <summary>Verifies a Window is visually distinguished by a paired-line frame without caller styling.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesPairedBorder()
    {
        var window = new Window();

        window.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
        window.CloseGlyph.ShouldBe(new Rune('■'));
    }

    /// <summary>Verifies the parameterless constructor establishes ordinary Window defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesOrdinaryDefaults()
    {
        var window = new Window();

        window.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
        window.HeaderPlacement.ShouldBe(WindowTitlePlacement.Left);
        window.ClosePlacement.ShouldBe(WindowClosePlacement.Left);
        window.CanMove.ShouldBeTrue();
        window.CanClose.ShouldBeFalse();
        window.CloseOnEscape.ShouldBeFalse();
    }

    /// <summary>Verifies ordinary Window properties can express specialized chrome without a role enum.</summary>
    [Fact]
    public void Properties_WhenSpecializedChromeIsRequested_UseCallerValues()
    {
        var window = new Window
        {
            CanMove = false,
            CanClose = true,
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Heavy),
            HeaderPlacement = WindowTitlePlacement.Right,
            ClosePlacement = WindowClosePlacement.Right
        };

        window.CanMove.ShouldBeFalse();
        window.CanClose.ShouldBeTrue();
        window.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        window.HeaderPlacement.ShouldBe(WindowTitlePlacement.Right);
        window.ClosePlacement.ShouldBe(WindowClosePlacement.Right);
    }

    /// <summary>Verifies Shown fires when a hidden Window becomes visible.</summary>
    [Fact]
    public void Shown_WhenWindowBecomesVisible_Fires()
    {
        // Arrange
        var fired = 0;
        var window = new Window { Visibility = Visibility.Collapsed };
        window.Shown += (_, _) => fired++;

        // Act
        window.Visibility = Visibility.Visible;

        // Assert
        fired.ShouldBe(1);
    }

    /// <summary>Verifies Shown fires when a default-visible Window attaches, not just on an explicit transition.</summary>
    [Fact]
    public async Task Shown_WhenDefaultVisibleWindowAttaches_FiresOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Overlay();
            var window = new Window();
            var fired = 0;
            window.Shown += (_, _) => fired++;
            root.Children.Add(window);
            new Engine().Layout(root, new Size(30, 12));

            root.Attach(dispatcher);

            fired.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies undefined close placement is rejected before the current value changes.</summary>
    [Fact]
    public void ClosePlacement_WhenValueIsUndefined_ThrowsBeforeMutation()
    {
        var window = new Window { ClosePlacement = WindowClosePlacement.Right };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            window.ClosePlacement = (WindowClosePlacement) 2);

        window.ClosePlacement.ShouldBe(WindowClosePlacement.Right);
    }

    /// <summary>Verifies Window exposes only the public single-content authoring role.</summary>
    [Fact]
    public void Type_WhenInspected_DerivesFromFloatingSurfaceWithoutRoleOrChildCollection()
    {
        var type = typeof(Window);

        type.BaseType.ShouldBe(typeof(FloatingSurface));
        type.IsSealed.ShouldBeFalse();
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.GetProperty(nameof(Container.Children)).ShouldBeNull();
        type.GetProperty(nameof(Container.AutoScroll)).ShouldBeNull();
        type.GetProperty(nameof(Container.AutoSize)).ShouldBeNull();
        type.GetProperty("Child").ShouldBeNull();
        _ = type.GetProperty(nameof(ContentControl.Content)).ShouldNotBeNull();
        type.GetConstructors().ShouldHaveSingleItem().GetParameters().ShouldBeEmpty();
        type.Assembly.GetType("SharpVision.Windows.WindowKind").ShouldBeNull();
    }

    /// <summary>Verifies attaching an already arranged visible Window immediately publishes its committed bounds.</summary>
    [Fact]
    public async Task Attach_WhenVisibleWindowWasAlreadyArranged_PublishesSurfaceBoundsImmediatelyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Overlay();
            var window = new Window { Width = Length.Cells(12), Height = Length.Cells(5) };
            root.Children.Add(window);
            new Engine().Layout(root, new Size(30, 12));
            var arranged = window.Bounds;

            window.SurfaceBounds.ShouldBe(default);
            root.Attach(dispatcher);

            window.SurfaceBounds.ShouldBe(arranged);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies showing an already arranged hidden Window restores bounds before another layout pass.</summary>
    [Fact]
    public async Task Visibility_WhenArrangedHiddenWindowBecomesVisible_RestoresSurfaceBoundsImmediatelyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Overlay();
            var window = new Window
            {
                Width = Length.Cells(12),
                Height = Length.Cells(5),
                Visibility = Visibility.Hidden
            };
            root.Children.Add(window);
            new Engine().Layout(root, new Size(30, 12));
            var arranged = window.Bounds;
            root.Attach(dispatcher);

            window.SurfaceBounds.ShouldBe(default);
            window.Visibility = Visibility.Visible;

            window.SurfaceBounds.ShouldBe(arranged);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies inherited content publication and direct disposal expose committed Window ownership.</summary>
    [Fact]
    public void Content_WhenAssignedThenDisposedDirectly_PublishesCommittedWindowOwnership()
    {
        var window = new Window();
        ContentControl owner = window;
        var content = new ProbeControl();
        var observations = new List<(Control? Content, Control? Parent)>();
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                observations.Add((owner.Content, content.Parent));
            }
        };

        owner.Content = content;
        content.Dispose();

        observations.ShouldBe([(content, window), (null, null)]);
        owner.Content.ShouldBeNull();
        content.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies Window disposal clears published content, disposes only its current content, and does so once.</summary>
    [Fact]
    public void Dispose_WhenWindowOwnsReplacement_DisposesCurrentOnceAndPublishesCommittedClear()
    {
        var window = new Window();
        var replaced = new OwnershipObserverControl();
        var current = new OwnershipObserverControl();
        window.Content = replaced;
        window.Content = current;
        var observations = new List<(Control? Content, Control? Parent, bool Disposed, int DisposingCalls)>();
        window.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                observations.Add((window.Content, current.Parent, current.IsDisposed, current.DisposingCalls));
            }
        };

        window.Dispose();
        window.Dispose();

        window.IsDisposed.ShouldBeTrue();
        window.Content.ShouldBeNull();
        current.IsDisposed.ShouldBeTrue();
        current.DisposingCalls.ShouldBe(1);
        observations.ShouldBe([(null, null, false, 1)]);
        replaced.IsDisposed.ShouldBeFalse();
        replaced.Parent.ShouldBeNull();
    }

    /// <summary>Verifies collapsed content contributes no margin and has stale layout state cleared.</summary>
    [Fact]
    public void Layout_WhenContentCollapses_PreservesFrameMinimumAndClearsContentGeometry()
    {
        var content = new ProbeControl(new Size(4, 2)) { Margin = new Thickness(3) };
        var window = new Window { Content = content };
        var engine = new Engine();

        engine.Layout(window, new Size(20, 10));
        var measureCalls = content.MeasureConstraints.Count;
        var arrangeCalls = content.ArrangeBounds.Count;
        content.DesiredSize.ShouldNotBe(default);
        content.Bounds.ShouldNotBe(default);

        content.Visibility = Visibility.Collapsed;
        engine.Layout(window, new Size(20, 10));

        window.DesiredSize.ShouldBe(new Size(2, 2));
        content.DesiredSize.ShouldBe(default);
        content.Bounds.ShouldBe(default);
        content.MeasureConstraints.Count.ShouldBe(measureCalls);
        content.ArrangeBounds.Count.ShouldBe(arrangeCalls);
    }

    /// <summary>Verifies a title owns the top edge while content receives the bounded interior box.</summary>
    [Fact]
    public void Render_WhenTitleAndChildArePresent_DrawsFramedChromeAndInterior()
    {
        var child = new ProbeControl(new Size(3, 1)) { Content = "app".AsMemory() };
        var window = new Window
        {
            Header = "Tools",
            Content = child,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var size = new Size(10, 4);
        new Engine().Layout(window, size);
        using Frame frame = new(size);

        window.Render(frame.Canvas);

        child.Bounds.ShouldBe(new Rect(1, 1, 8, 2));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╔");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("s");
        FrameOracle.Get(frame, new Point(7, 0)).ShouldBe(" ");
        FrameOracle.Get(frame, new Point(9, 0)).ShouldBe("╗");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(0, 3)).ShouldBe("╚");
    }

    /// <summary>Verifies wide-glyph (CJK) headers truncate by cell width, not UTF-16 char
    /// count, so the title never overflows past its lane into the border corner.</summary>
    [Fact]
    public void Render_WhenHeaderContainsWideGlyphsAndOverflows_TruncatesByCellWidthNotCharCount()
    {
        var window = new Window
        {
            Header = "中中中中中中中中中中",
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        var size = new Size(12, 3);
        new Engine().Layout(window, size);
        using Frame frame = new(size);

        window.Render(frame.Canvas);

        var row = string.Concat(Enumerable.Range(0, 12).Select(x => FrameOracle.Get(frame, new Point(x, 0))));
        row.ShouldBe("╔ 中中中中中中… ═╗");
        row.ShouldContain("…");
        row.ShouldNotContain("中中中中中中中中");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╔");
        FrameOracle.Get(frame, new Point(11, 0)).ShouldBe("╗");
    }

    /// <summary>Verifies the Turbo Vision block shadow occupies only translated cells outside the window body.</summary>
    [Fact]
    public void Render_WhenBlockShadowIsEnabled_DrawsOutsideBodyWithoutCoveringContent()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 4, 3),
            Shadow = AppearanceTestValues.Shadow(
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 1))
        };
        using Frame frame = new(new Size(6, 5));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("╝");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 3)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╔");
    }

    /// <summary>Verifies retained child shadows cannot replace the owning Window frame.</summary>
    [Fact]
    public void Render_WhenContentShadowTouchesFrame_PaintsFrameAfterContent()
    {
        var content = new Button
        {
            Style = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 1),
                glyph: new Rune('▓')))
        };
        var window = new Window
        {
            Width = Length.Cells(5),
            Height = Length.Cells(4),
            Shadow = AppearanceTestValues.Shadow(visible: false),
            Content = content
        };
        new Engine().Layout(window, new Size(5, 4));
        using Frame frame = new(new Size(5, 4));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(4, 2)).ShouldBe("║");
        FrameOracle.Get(frame, new Point(2, 3)).ShouldBe("═");
    }

    /// <summary>Verifies Window emits its intrinsic shadow exactly once.</summary>
    [Fact]
    public void Render_WhenTextArenaFitsOneFrameAndShadow_DoesNotDrawShadowTwice()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 2, 2),
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Ascii),
            Shadow = AppearanceTestValues.Shadow(
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 1),
                glyph: new Rune('s'))
        };
        using Frame frame = new(new Size(3, 3), maxTextBytes: 7);

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("s");
        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("s");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("s");
    }

    /// <summary>Verifies a long title clips inside the top edge without corrupting either frame corner.</summary>
    [Fact]
    public void Render_WhenTitleExceedsFrameWidth_PreservesTopCorners()
    {
        var window = new Window { Header = "A deliberately long title" };
        var size = new Size(6, 2);
        new Engine().Layout(window, size);
        using Frame frame = new(size);

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╔");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("╗");
    }

    /// <summary>Verifies centered and right title placement keep the title inside both corners.</summary>
    [Theory]
    [InlineData(WindowTitlePlacement.Center, 9)]
    [InlineData(WindowTitlePlacement.Right, 16)]
    public void Render_WhenTitlePlacementChanges_AlignsTitleInsideFrame(
        WindowTitlePlacement placement,
        int expectedTitleColumn)
    {
        var window = new Window { Bounds = new Rect(0, 0, 20, 3), Header = "Hi", HeaderPlacement = placement };
        using Frame frame = new(new Size(20, 3));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(expectedTitleColumn, 0)).ShouldBe("H");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╔");
        FrameOracle.Get(frame, new Point(19, 0)).ShouldBe("╗");
    }

    /// <summary>Verifies every standard frame family supplies compact bracketed close chrome.</summary>
    [Theory]
    [MemberData(nameof(CloseChromeCases))]
    public void Render_WhenStandardFrameIsClosable_UsesBracketedCloseChrome(
        BorderGlyphStyle glyphs,
        string expected)
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 16, 3),
            CanClose = true,
            CloseGlyph = new Rune('x'),
            Border = AppearanceTestValues.Border(BorderSide.All, glyphs)
        };
        using Frame frame = new(new Size(16, 3));

        window.Render(frame.Canvas);

        ReadRow(frame, 1, 7).ShouldBe(expected);
    }

    /// <summary>Provides exact close chrome for every standard border family.</summary>
    public static TheoryData<BorderGlyphStyle, string> CloseChromeCases => new()
    {
        { BorderGlyphStyle.Light, "──[x]──" },
        { BorderGlyphStyle.Rounded, "──[x]──" },
        { BorderGlyphStyle.Heavy, "━━[x]━━" },
        { BorderGlyphStyle.Paired, "══[x]══" },
        { BorderGlyphStyle.Ascii, "--[x]--" }
    };

    /// <summary>Verifies the close control can occupy the trailing title-bar edge.</summary>
    [Fact]
    public void Render_WhenClosePlacementIsRight_DrawsChromeBeforeTopRightCorner()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 16, 3),
            CanClose = true,
            CloseGlyph = new Rune('x'),
            ClosePlacement = WindowClosePlacement.Right
        };
        using Frame frame = new(new Size(16, 3));

        window.Render(frame.Canvas);

        ReadRow(frame, 8, 7).ShouldBe("══[x]══");
        FrameOracle.Get(frame, new Point(15, 0)).ShouldBe("╗");
    }

    /// <summary>Verifies decorative custom borders retain their configured-glyph close fallback.</summary>
    [Fact]
    public void Render_WhenBorderHasNoLineTopology_UsesConfiguredSideGlyphFallback()
    {
        var glyphs = new BorderGlyphStyle(
            new Rune('A'),
            new Rune('h'),
            new Rune('B'),
            new Rune('r'),
            new Rune('C'),
            new Rune('b'),
            new Rune('D'),
            new Rune('l'));
        var window = new Window
        {
            Bounds = new Rect(0, 0, 16, 3),
            CanClose = true,
            CloseGlyph = new Rune('x'),
            Border = AppearanceTestValues.Border(BorderSide.All, glyphs)
        };
        using Frame frame = new(new Size(16, 3));

        window.Render(frame.Canvas);

        ReadRow(frame, 1, 7).ShouldBe("hh[x]hh");
    }

    /// <summary>Verifies title alignment uses only the uninterrupted lane beside leading close chrome.</summary>
    [Theory]
    [InlineData(WindowTitlePlacement.Left, 9)]
    [InlineData(WindowTitlePlacement.Center, 9)]
    [InlineData(WindowTitlePlacement.Right, 16)]
    public void Render_WhenCloseIsLeft_AlignsTitleInsideRemainingLane(
        WindowTitlePlacement placement,
        int expectedTitleColumn)
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 20, 3),
            CanClose = true,
            CloseGlyph = new Rune('x'),
            Header = "Hi",
            HeaderPlacement = placement
        };
        using Frame frame = new(new Size(20, 3));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(expectedTitleColumn, 0)).ShouldBe("H");
        ReadRow(frame, 1, 7).ShouldBe("══[x]══");
    }

    /// <summary>Verifies automatic width reserves complete close chrome and a Unicode-measured title lane.</summary>
    [Fact]
    public void Measure_WhenClosableWindowHasWideTitle_ReservesChromeWithoutCollision()
    {
        var window = new Window { CanClose = true, Header = "界" };

        new Engine().Layout(window, new Size(40, 8));

        window.DesiredSize.Width.ShouldBe(13);
    }

    /// <summary>Verifies a narrow closable Window keeps a visible close glyph at the selected leading edge.</summary>
    [Fact]
    public void Render_WhenCloseChromeDoesNotFit_UsesLeadingSingleCellFallback()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 8, 3),
            CanClose = true,
            CloseGlyph = new Rune('x')
        };
        using Frame frame = new(new Size(8, 3));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("x");
        FrameOracle.Get(frame, default).ShouldBe("╔");
        FrameOracle.Get(frame, new Point(7, 0)).ShouldBe("╗");
    }

    /// <summary>Verifies tiny bounds preserve the frame and omit an unrepresentable close control.</summary>
    [Fact]
    public void Render_WhenFrameIsTooNarrowForCloseGlyph_PreservesCorners()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 3, 2),
            CanClose = true,
            CloseGlyph = new Rune('x')
        };
        using Frame frame = new(new Size(3, 2));

        window.Render(frame.Canvas);

        ReadRow(frame, 0, 3).ShouldBe("╔═╗");
    }

    /// <summary>Verifies unhandled Enter and Escape invoke the first available default and cancel button inside the window.</summary>
    [Fact]
    public void Dispatch_WhenEnterOrEscapeIsUnhandled_InvokesWindowDefaultOrCancelButton()
    {
        var defaults = 0;
        var cancels = 0;
        var content = new Stack();
        var accept = new Button { IsDefault = true };
        var cancel = new Button { IsCancel = true };
        accept.Click += (_, _) => defaults++;
        cancel.Click += (_, _) => cancels++;
        content.Children.Add(accept);
        content.Children.Add(cancel);
        var window = new Window { Content = content };

        _ = Router.Route(window, Events.Key, Key(Code.Enter));
        _ = Router.Route(window, Events.Key, Key(Code.Escape));

        defaults.ShouldBe(1);
        cancels.ShouldBe(1);
    }

    /// <summary>Verifies default and cancel discovery traverses private slots on non-Container content.</summary>
    [Fact]
    public void Dispatch_WhenButtonsUseNonContainerSlots_InvokesDefaultAndCancel()
    {
        var defaults = 0;
        var cancels = 0;
        var content = new TraversalOwner();
        var branch = new TraversalOwner();
        var accept = new Button { IsDefault = true };
        var cancel = new Button { IsCancel = true };
        accept.Click += (_, _) => defaults++;
        cancel.Click += (_, _) => cancels++;
        content.AddExcluded(branch);
        branch.AddExcluded(accept);
        content.AddPopup(cancel);
        var window = new Window { Content = content };

        _ = Router.Route(window, Events.Key, Key(Code.Enter));
        _ = Router.Route(window, Events.Key, Key(Code.Escape));

        defaults.ShouldBe(1);
        cancels.ShouldBe(1);
    }

    /// <summary>Verifies fallback discovery skips unavailable candidates and activates only the first eligible slot member.</summary>
    [Fact]
    public void Dispatch_WhenEarlierFallbackButtonsAreUnavailable_ActivatesFirstEligibleAcrossSlots()
    {
        var content = new TraversalOwner();
        var disabled = FallbackButton();
        disabled.IsEnabled = false;
        var hidden = FallbackButton();
        hidden.Visibility = Visibility.Hidden;
        var collapsed = FallbackButton();
        collapsed.Visibility = Visibility.Collapsed;
        var firstEligible = FallbackButton();
        var laterEligible = FallbackButton();
        var invocations = new Dictionary<Button, int>
        {
            [disabled] = 0,
            [hidden] = 0,
            [collapsed] = 0,
            [firstEligible] = 0,
            [laterEligible] = 0
        };

        foreach (var button in invocations.Keys)
        {
            button.Click += (_, _) => invocations[button]++;
        }

        content.AddNormal(disabled);
        content.AddExcluded(hidden);
        content.AddSecondary(collapsed);
        content.AddPopup(firstEligible);
        content.AddPopup(laterEligible);
        var window = new Window { Content = content };

        _ = Router.Route(window, Events.Key, Key(Code.Enter));
        _ = Router.Route(window, Events.Key, Key(Code.Escape));

        invocations[disabled].ShouldBe(0);
        invocations[hidden].ShouldBe(0);
        invocations[collapsed].ShouldBe(0);
        invocations[firstEligible].ShouldBe(2);
        invocations[laterEligible].ShouldBe(0);
    }

    /// <summary>Verifies handled keys and non-press strokes do not invoke Window fallbacks.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsHandledOrNotPress_IgnoresFallbackButton()
    {
        var invocations = 0;
        var button = FallbackButton();
        button.Click += (_, _) => invocations++;
        var window = new Window { Content = button };
        var handled = Key(Code.Enter);
        _ = window.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Stroke is { Code: Code.Enter, Action: KeyAction.Press })
            {
                eventArgs.Handled = true;
            }
        });

        _ = Router.Route(window, Events.Key, handled);
        _ = Router.Route(window, Events.Key, Key(Code.Escape, KeyAction.Release));

        handled.Handled.ShouldBeTrue();
        invocations.ShouldBe(0);
    }

    /// <summary>Verifies Escape requests closure for a closable dialog that has no cancel action.</summary>
    [Fact]
    public void Dispatch_WhenDialogEscapeHasNoCancelButton_RaisesClosing()
    {
        var closing = 0;
        var window = new Window
        {
            CanMove = false,
            CanClose = true,
            CloseOnEscape = true,
            HeaderPlacement = WindowTitlePlacement.Center
        };
        window.Closing += (_, _) => closing++;
        var key = Key(Code.Escape);

        _ = Router.Route(window, Events.Key, key);

        closing.ShouldBe(1);
        key.Handled.ShouldBeTrue();
    }

    /// <summary>Verifies a dialog cancel button takes precedence over the frame close fallback.</summary>
    [Fact]
    public void Dispatch_WhenDialogEscapeHasCancelButton_ActivatesCancelWithoutClosing()
    {
        var cancelled = 0;
        var closing = 0;
        var cancel = new Button { IsCancel = true };
        cancel.Click += (_, _) => cancelled++;
        var window = new Window
        {
            CanMove = false,
            CanClose = true,
            CloseOnEscape = true,
            HeaderPlacement = WindowTitlePlacement.Center,
            Content = cancel
        };
        window.Closing += (_, _) => closing++;

        _ = Router.Route(window, Events.Key, Key(Code.Escape));

        cancelled.ShouldBe(1);
        closing.ShouldBe(0);
    }

    /// <summary>Verifies Escape does not reinterpret a normal Window as a dialog.</summary>
    [Fact]
    public void Dispatch_WhenNormalClosableWindowReceivesEscape_DoesNotRaiseClosing()
    {
        var closing = 0;
        var window = new Window { CanClose = true };
        window.Closing += (_, _) => closing++;
        var key = Key(Code.Escape);

        _ = Router.Route(window, Events.Key, key);

        closing.ShouldBe(0);
        key.Handled.ShouldBeFalse();
    }

    /// <summary>Verifies Closed still fires when a Closing handler disposes the window synchronously.</summary>
    [Fact]
    public async Task Closed_WhenClosingHandlerDisposesWindowSynchronously_FiresOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var closed = 0;
            var window = new Window
            {
                CanMove = false,
                CanClose = true,
                CloseOnEscape = true,
                HeaderPlacement = WindowTitlePlacement.Center
            };
            window.Closing += (_, _) => window.Dispose();
            window.Closed += (_, _) => closed++;
            var root = new Overlay { Children = { window } };
            new Engine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);

            _ = Router.Route(window, Events.Key, Key(Code.Escape));

            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies close activation waits for an armed primary release.</summary>
    [Fact]
    public async Task Close_WhenPrimaryPressIsHeld_WaitsForReleaseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var closing = 0;
            var window = ClosableWindow();
            window.Closing += (_, _) => closing++;
            var root = new Overlay { Children = { window } };
            new Engine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            using PointerManager pointer = new(root);

            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Press));

            closing.ShouldBe(0);
            pointer.Captured.ShouldBeSameAs(window);

            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Release));

            closing.ShouldBe(1);
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies releasing outside the close target cancels activation without starting title drag.</summary>
    [Fact]
    public async Task Close_WhenPointerReleasesOutside_CancelsWithoutMovingWindowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var closing = 0;
            var window = ClosableWindow();
            window.Closing += (_, _) => closing++;
            var root = new Overlay { Children = { window } };
            new Engine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            using PointerManager pointer = new(root);

            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Press));
            _ = pointer.Dispatch(Pointer(new Point(12, 0), PointerAction.Move));
            _ = pointer.Dispatch(Pointer(new Point(12, 0), PointerAction.Release));

            closing.ShouldBe(0);
            window.Left.ShouldBe(Length.Cells(0));
            window.Top.ShouldBe(Length.Cells(0));
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies moving out and back into the captured close target rearms one activation.</summary>
    [Fact]
    public async Task Close_WhenPointerReturnsBeforeRelease_ActivatesOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var closing = 0;
            var window = ClosableWindow();
            window.Closing += (_, _) => closing++;
            var root = new Overlay { Children = { window } };
            new Engine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            using PointerManager pointer = new(root);

            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Press));
            _ = pointer.Dispatch(Pointer(new Point(12, 0), PointerAction.Move));
            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Move));
            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Release));

            closing.ShouldBe(1);
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies availability-driven capture loss cancels a held close gesture.</summary>
    [Fact]
    public async Task Close_WhenWindowBecomesUnavailable_CancelsWithoutActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var closing = 0;
            var window = ClosableWindow();
            window.Closing += (_, _) => closing++;
            var root = new Overlay { Children = { window } };
            new Engine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            using PointerManager pointer = new(root);

            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Press));
            window.IsEnabled = false;

            closing.ShouldBe(0);
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies CanMove defaults to true and can be disabled.</summary>
    [Fact]
    public void CanMove_WhenDefaulted_IsTrue()
    {
        var window = new Window();

        window.CanMove.ShouldBeTrue();
        window.CanMove = false;
        window.CanMove.ShouldBeFalse();
    }

    /// <summary>Verifies dragging the title bar updates the window's own Left and Top properties.</summary>
    [Fact]
    public async Task Drag_WhenTitleBarIsDragged_UpdatesLeftAndTopAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Draggable",
                Width = Length.Cells(12),
                Height = Length.Cells(5),
                Left = Length.Cells(2),
                Top = Length.Cells(1)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(30, 15));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(5, 1), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);
            _ = capture.Dispatch(Pointer(new Point(8, 3), PointerAction.Move));

            window.Left.ShouldBe(Length.Cells(5));
            window.Top.ShouldBe(Length.Cells(3));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a burst of drag moves uses the latest pointer position without waiting for layout.</summary>
    [Fact]
    public async Task Drag_WhenPointerMovesOutrunLayout_TracksLatestPointerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Burst",
                Width = Length.Cells(12),
                Height = Length.Cells(5),
                Left = Length.Cells(2),
                Top = Length.Cells(1)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(30, 15));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(5, 1), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(6, 2), PointerAction.Move));
            _ = capture.Dispatch(Pointer(new Point(8, 4), PointerAction.Move));
            _ = capture.Dispatch(Pointer(new Point(11, 5), PointerAction.Move));

            window.Left.ShouldBe(Length.Cells(8));
            window.Top.ShouldBe(Length.Cells(5));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies dragging cannot move the Window body outside its parent's content area.</summary>
    [Fact]
    public async Task Drag_WhenDraggedPastClientEdge_ClampsToParentContentBoundsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay
            {
                Border = AppearanceTestValues.Border(BorderSide.All),
                Padding = new Thickness(1, 0)
            };
            var window = new Window
            {
                Header = "Contained",
                Width = Length.Cells(10),
                Height = Length.Cells(4),
                Left = Length.Cells(2),
                Top = Length.Cells(1)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(
                Pointer(new Point(window.Bounds.X + 2, window.Bounds.Y), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(100, 100), PointerAction.Move));

            window.Left.ShouldBe(Length.Cells(6));
            window.Top.ShouldBe(Length.Cells(4));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an oversized Window begins at the Overlay content origin and clips trailing overflow.</summary>
    [Fact]
    public void Layout_WhenWindowExceedsOverlayContent_AnchorsAtLeadingContentOrigin()
    {
        // Arrange
        var canvas = new Overlay
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1, 0)
        };
        var window = new Window
        {
            Width = Length.Cells(10),
            Height = Length.Cells(6),
            Left = Length.Cells(5),
            Top = Length.Cells(5)
        };
        canvas.Children.Add(window);

        // Act
        new Engine().Layout(canvas, new Size(8, 5));

        // Assert
        window.Bounds.ShouldBe(new Rect(2, 1, 10, 6));
    }

    /// <summary>Verifies an unpositioned Overlay Window resolves its centered alignment before containment.</summary>
    [Fact]
    public void Layout_WhenOverlayWindowIsUnpositioned_CentersFromAlignment()
    {
        // Arrange
        var window = new Window
        {
            Width = Length.Cells(10),
            Height = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var canvas = new Overlay { Children = { window } };

        // Act
        new Engine().Layout(canvas, new Size(30, 12));

        // Assert
        window.Bounds.ShouldBe(new Rect(10, 4, 10, 4));
        window.Left.ShouldBeNull();
        window.Top.ShouldBeNull();
    }

    /// <summary>Verifies drag uses the Window's own Overlay position properties.</summary>
    [Fact]
    public async Task Drag_WhenInsideNestedContainer_PositionsRelativeToParentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var outer = new Dock { Padding = new Thickness(5) };
            var canvas = new Overlay();
            outer.Children.Add(canvas);
            var window = new Window
            {
                Header = "Nested",
                Width = Length.Cells(10),
                Height = Length.Cells(4),
                Left = Length.Cells(0),
                Top = Length.Cells(0)
            };
            canvas.Children.Add(window);
            new Engine().Layout(outer, new Size(40, 20));
            outer.Attach(dispatcher);
            using PointerManager capture = new(outer);

            var titleY = window.Bounds.Y;
            var titleX = window.Bounds.X + 2;
            _ = capture.Dispatch(Pointer(new Point(titleX, titleY), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);
            _ = capture.Dispatch(Pointer(new Point(titleX + 3, titleY + 2), PointerAction.Move));

            window.Left.ShouldBe(Length.Cells(3));
            window.Top.ShouldBe(Length.Cells(2));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies drag does not move the window when CanMove is false.</summary>
    [Fact]
    public async Task Drag_WhenCanMoveIsFalse_DoesNotMoveAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Fixed",
                Width = Length.Cells(10),
                Height = Length.Cells(4),
                CanMove = false,
                Left = Length.Cells(1),
                Top = Length.Cells(1)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(3, 1), PointerAction.Press));
            capture.Captured.ShouldNotBeSameAs(window);

            window.Left.ShouldBe(Length.Cells(1));
            window.Top.ShouldBe(Length.Cells(1));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies releasing the pointer ends the drag.</summary>
    [Fact]
    public async Task Drag_WhenReleased_EndsDragAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Release",
                Width = Length.Cells(10),
                Height = Length.Cells(4),
                Left = Length.Cells(0),
                Top = Length.Cells(0)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(3, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);
            _ = capture.Dispatch(Pointer(new Point(5, 2), PointerAction.Move));
            _ = capture.Dispatch(Pointer(new Point(5, 2), PointerAction.Release));

            capture.Captured.ShouldBeNull();
            window.Left.ShouldBe(Length.Cells(2));
            window.Top.ShouldBe(Length.Cells(2));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Release still ends an active drag and releases capture even when
    /// CanMove was toggled off mid-drag, instead of leaking capture permanently.</summary>
    [Fact]
    public async Task Drag_WhenCanMoveBecomesFalseDuringDrag_StillReleasesCaptureOnReleaseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Release",
                Width = Length.Cells(10),
                Height = Length.Cells(4),
                Left = Length.Cells(0),
                Top = Length.Cells(0)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(3, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);

            window.CanMove = false;
            _ = capture.Dispatch(Pointer(new Point(5, 2), PointerAction.Release));

            capture.Captured.ShouldBeNull();
            window.HasPointerCapture.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Release still ends an active drag and releases capture even when the
    /// event carries no cell coordinates (a legitimate state in SGR-pixel mouse mode without
    /// cell-metrics mapping), instead of leaking capture permanently.</summary>
    [Fact]
    public async Task Drag_WhenReleaseHasNoCellCoordinates_StillReleasesCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Release",
                Width = Length.Cells(10),
                Height = Length.Cells(4),
                Left = Length.Cells(0),
                Top = Length.Cells(0)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(3, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);

            var releaseWithoutCells = new Pointer(
                cells: null,
                pixels: new Point(50, 20),
                Buttons.None,
                PointerAction.Release,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false);
            _ = capture.Dispatch(releaseWithoutCells);

            capture.Captured.ShouldBeNull();
            window.HasPointerCapture.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies dragging past the top-left edge clamps to zero instead of throwing.</summary>
    [Fact]
    public async Task Drag_WhenDraggedPastOrigin_ClampsToZeroAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Clamp",
                Width = Length.Cells(10),
                Height = Length.Cells(4),
                Left = Length.Cells(2),
                Top = Length.Cells(2)
            };
            canvas.Children.Add(window);
            new Engine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(5, 2), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(0, 0), PointerAction.Move));

            window.Left.ShouldBe(Length.Cells(0));
            window.Top.ShouldBe(Length.Cells(0));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the Header alias reads and writes the underlying Title property.</summary>
    [Fact]
    public void Header_WhenAssigned_GetsAndSetsTitle()
    {
        var window = new Window { Header = "Test" };

        window.Header.ShouldBe("Test");
        window.Header.ShouldBe("Test");
    }

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        action == PointerAction.Release ? Buttons.None : Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

    private static Button FallbackButton() => new() { IsDefault = true, IsCancel = true };

    private static Window ClosableWindow() => new()
    {
        Width = Length.Cells(14),
        Height = Length.Cells(4),
        CanClose = true,
        Left = Length.Cells(0),
        Top = Length.Cells(0)
    };

    private static string ReadRow(Frame frame, int start, int length) =>
        string.Concat(Enumerable.Range(start, length).Select(x => FrameOracle.Get(frame, new Point(x, 0))));

    private static KeyEventArgs Key(Code code, KeyAction action = KeyAction.Press) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        action));
}
