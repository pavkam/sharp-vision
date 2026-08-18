// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Windows;

using SharpVision.Surfaces;

/// <summary>Verifies framed terminal window layout, title chrome, and visual shadow behavior.</summary>
public sealed class WindowTests
{
    /// <summary>Verifies a Window owns an opaque semantic background distinct from ordinary
    /// Control/Container content, without caller styling.</summary>
    [ComponentUnitEvidence(typeof(Window))]
    [Fact]
    public void Constructor_WhenCreated_UsesWindowBackgroundColor()
    {
        var window = new Window();

        window.Face.Background.ShouldBe(SemanticColor.WindowSurface);
    }

    /// <summary>Verifies Window proves direct and ancestor-inherited disabled state at the
    /// detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled - the
    /// same disabled contract exercised on a live mounted terminal surface.</summary>
    [ComponentUnitEvidence(typeof(Window), ComponentBehavior.Disabled)]
    [Fact]
    public void EffectiveIsEnabled_WhenWindowIsDisabledDirectlyOrByAncestor_ReportsDisabledAndRecovers()
    {
        var window = new Window();
        var host = new Overlay();
        host.Children.Add(window);

        window.IsEnabled = false;
        window.EffectiveIsEnabled.ShouldBeFalse();

        window.IsEnabled = true;
        window.EffectiveIsEnabled.ShouldBeTrue();

        host.IsEnabled = false;
        window.IsEnabled.ShouldBeTrue();
        window.EffectiveIsEnabled.ShouldBeFalse();

        host.IsEnabled = true;
        window.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies a Window is visually distinguished by a paired-line frame without caller styling.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesPairedBorder()
    {
        var window = new Window();

        window.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
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
        window.CanClose.ShouldBeTrue();
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
            new LayoutEngine().Layout(root, new Size(30, 12));

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

        type.BaseType.ShouldBe(typeof(FloatingSurfaceBase));
        typeof(FloatingSurfaceBase).IsAssignableFrom(type).ShouldBeTrue();
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
            new LayoutEngine().Layout(root, new Size(30, 12));
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
            new LayoutEngine().Layout(root, new Size(30, 12));
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
        var observations = new List<(ControlBase? Content, ControlBase? Parent)>();
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
        var observations = new List<(ControlBase? Content, ControlBase? Parent, bool IsDisposed, int DisposingCalls)>();
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
        var window = new Window { Content = content, CanClose = false };
        var engine = new LayoutEngine();

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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CanClose = false
        };
        var size = new Size(10, 4);
        new LayoutEngine().Layout(window, size);
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
            Height = Length.Cells(3),
            CanClose = false
        };
        var size = new Size(12, 3);
        new LayoutEngine().Layout(window, size);
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
        new LayoutEngine().Layout(window, new Size(5, 4));
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
        new LayoutEngine().Layout(window, size);
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
            Border = AppearanceTestValues.Border(BorderSide.All, glyphs)
        };
        using Frame frame = new(new Size(16, 3));

        window.Render(frame.Canvas);

        ReadRow(frame, 1, 7).ShouldBe(expected);
    }

    /// <summary>Provides exact close chrome for every standard border family.</summary>
    public static TheoryData<BorderGlyphStyle, string> CloseChromeCases => new()
    {
        { BorderGlyphStyle.Light, "──[■]──" },
        { BorderGlyphStyle.Rounded, "──[■]──" },
        { BorderGlyphStyle.Heavy, "━━[■]━━" },
        { BorderGlyphStyle.Paired, "══[■]══" },
        { BorderGlyphStyle.Ascii, "--[■]--" }
    };

    /// <summary>Verifies the close control can occupy the trailing title-bar edge.</summary>
    [Fact]
    public void Render_WhenClosePlacementIsRight_DrawsChromeBeforeTopRightCorner()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 16, 3),
            CanClose = true,
            ClosePlacement = WindowClosePlacement.Right
        };
        using Frame frame = new(new Size(16, 3));

        window.Render(frame.Canvas);

        ReadRow(frame, 8, 7).ShouldBe("══[■]══");
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
            Border = AppearanceTestValues.Border(BorderSide.All, glyphs)
        };
        using Frame frame = new(new Size(16, 3));

        window.Render(frame.Canvas);

        ReadRow(frame, 1, 7).ShouldBe("hh[■]hh");
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
            Header = "Hi",
            HeaderPlacement = placement
        };
        using Frame frame = new(new Size(20, 3));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(expectedTitleColumn, 0)).ShouldBe("H");
        ReadRow(frame, 1, 7).ShouldBe("══[■]══");
    }

    /// <summary>Verifies automatic width reserves complete close chrome and a Unicode-measured title lane.</summary>
    [Fact]
    public void Measure_WhenClosableWindowHasWideTitle_ReservesChromeWithoutCollision()
    {
        var window = new Window { CanClose = true, Header = "界" };

        new LayoutEngine().Layout(window, new Size(40, 8));

        window.DesiredSize.Width.ShouldBe(13);
    }

    /// <summary>Verifies a narrow closable Window keeps a visible close glyph at the selected leading edge.</summary>
    [Fact]
    public void Render_WhenCloseChromeDoesNotFit_UsesLeadingSingleCellFallback()
    {
        var window = new Window
        {
            Bounds = new Rect(0, 0, 8, 3),
            CanClose = true
        };
        using Frame frame = new(new Size(8, 3));

        window.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("■");
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
            CanClose = true
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
                eventArgs.IsHandled = true;
            }
        });

        _ = Router.Route(window, Events.Key, handled);
        _ = Router.Route(window, Events.Key, Key(Code.Escape, KeyAction.Release));

        handled.IsHandled.ShouldBeTrue();
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
        key.IsHandled.ShouldBeTrue();
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
        key.IsHandled.ShouldBeFalse();
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
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);

            _ = Router.Route(window, Events.Key, Key(Code.Escape));

            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a CloseRequested handler can veto Escape-driven closure, leaving the window
    /// presented and raising neither Closing nor Closed.</summary>
    [Fact]
    public async Task CloseRequested_WhenHandlerCancels_LeavesWindowPresentedAndRaisesNeitherClosingNorClosedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanMove = false,
                CanClose = true,
                CloseOnEscape = true,
                HeaderPlacement = WindowTitlePlacement.Center
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            var closingCalls = 0;
            var closedCalls = 0;
            window.CloseRequested += (_, eventArgs) => eventArgs.Cancel = true;
            window.Closing += (_, _) => closingCalls++;
            window.Closed += (_, _) => closedCalls++;

            var key = Key(Code.Escape);
            _ = Router.Route(window, Events.Key, key);

            window.Visibility.ShouldBe(Visibility.Visible);
            closingCalls.ShouldBe(0);
            closedCalls.ShouldBe(0);
            key.IsHandled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an uncancelled CloseRequested still closes normally, publishing the request
    /// once before Closing and Closed each fire exactly once.</summary>
    [Fact]
    public async Task CloseRequested_WhenNotCancelled_PublishesRequestThenClosingThenClosedExactlyOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanMove = false,
                CanClose = true,
                CloseOnEscape = true,
                HeaderPlacement = WindowTitlePlacement.Center
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            var order = new List<string>();
            window.CloseRequested += (_, eventArgs) =>
            {
                order.Add("requested");
                eventArgs.Cancel.ShouldBeFalse();
                window.Visibility.ShouldBe(Visibility.Visible);
            };
            window.Closing += (_, _) => order.Add("closing");
            window.Closed += (_, _) => order.Add("closed");

            _ = Router.Route(window, Events.Key, Key(Code.Escape));

            order.ShouldBe(["requested", "closing", "closed"]);
            window.Visibility.ShouldBe(Visibility.Collapsed);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a CloseRequested handler that calls Close() reentrantly (synchronously, from
    /// inside the event) does not re-enter RequestClose and re-raise CloseRequested a second
    /// time. The reentrancy guard must already be armed before CloseRequested is raised, the same
    /// way it is already armed before Closing is raised - otherwise the reentrant call passes both
    /// guard checks and invokes the same handler again, with no bound on the recursion.
    /// </summary>
    [Fact]
    public async Task CloseRequested_WhenHandlerCallsCloseReentrantly_DoesNotReenterAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window { CanMove = false, CanClose = true, HeaderPlacement = WindowTitlePlacement.Center };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            var requestedCalls = 0;
            window.CloseRequested += (_, _) =>
            {
                requestedCalls++;

                if (requestedCalls == 1)
                {
                    window.Close();
                }
            };

            window.Close();

            requestedCalls.ShouldBe(1);
            window.Visibility.ShouldBe(Visibility.Collapsed);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the public Close() method runs the same veto, Closing, and default-collapse
    /// sequence as Escape and the close affordance.</summary>
    [Fact]
    public async Task Close_WhenCalledProgrammatically_PublishesTheFullCloseSequenceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window { CanMove = false, CanClose = true, HeaderPlacement = WindowTitlePlacement.Center };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            var order = new List<string>();
            window.CloseRequested += (_, _) => order.Add("requested");
            window.Closing += (_, _) => order.Add("closing");
            window.Closed += (_, _) => order.Add("closed");

            window.Close();

            order.ShouldBe(["requested", "closing", "closed"]);
            window.Visibility.ShouldBe(Visibility.Collapsed);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies calling Close() again after committed cleanup raises nothing, matching
    /// FloatingSurfaceBase's own idempotency contract instead of re-raising CloseRequested and
    /// Closing against an already-closed Window.</summary>
    [Fact]
    public async Task Close_WhenAlreadyClosed_RaisesNothingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window { CanMove = false, CanClose = true, HeaderPlacement = WindowTitlePlacement.Center };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            window.Close();
            var order = new List<string>();
            window.CloseRequested += (_, _) => order.Add("requested");
            window.Closing += (_, _) => order.Add("closing");
            window.Closed += (_, _) => order.Add("closed");

            window.Close();

            order.ShouldBeEmpty();
            window.Visibility.ShouldBe(Visibility.Collapsed);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies closing a Window that was never presented (Collapsed from construction,
    /// or detached) raises nothing, rather than publishing a phantom close sequence for a Window
    /// that was never open.</summary>
    [Fact]
    public async Task Close_WhenNeverPresented_RaisesNothingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanMove = false,
                CanClose = true,
                Visibility = Visibility.Collapsed
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            var order = new List<string>();
            window.CloseRequested += (_, _) => order.Add("requested");
            window.Closing += (_, _) => order.Add("closing");
            window.Closed += (_, _) => order.Add("closed");

            window.Close();

            order.ShouldBeEmpty();
            window.Visibility.ShouldBe(Visibility.Collapsed);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a never-attached, still-IsVisible Window - the only shape the ordinary
    /// two-clause guard cannot distinguish from "legitimately still open", since it has no
    /// IsSurfacePresented transition to collapse or detect a repeat from - raises CloseRequested
    /// and Closing exactly once no matter how many times Close() is called, instead of repeating
    /// forever.</summary>
    [Fact]
    public void Close_WhenNeverAttachedAndStillVisible_RaisesEventsOnlyOnce()
    {
        var window = new Window();
        var order = new List<string>();
        window.CloseRequested += (_, _) => order.Add("requested");
        window.Closing += (_, _) => order.Add("closing");
        window.Closed += (_, _) => order.Add("closed");

        window.Close();
        window.Close();
        window.Close();

        order.ShouldBe(["requested", "closing"]);
        window.Visibility.ShouldBe(Visibility.Visible);
    }

    /// <summary>Verifies Visibility returning to IsVisible after a never-attached close reopens the
    /// substitute open/closed bit, so a subsequent Close() raises its events again instead of
    /// being permanently rejected as a repeat.</summary>
    [Fact]
    public void Close_WhenNeverAttachedWindowIsShownAgainAfterClosing_RaisesEventsAgain()
    {
        var window = new Window();
        var order = new List<string>();
        window.CloseRequested += (_, _) => order.Add("requested");
        window.Closing += (_, _) => order.Add("closing");

        window.Close();
        window.Visibility = Visibility.Collapsed;
        window.Visibility = Visibility.Visible;
        window.Close();

        order.ShouldBe(["requested", "closing", "requested", "closing"]);
    }

    /// <summary>Verifies Close() on a disposed Window throws ObjectDisposedException, matching
    /// every other public mutator, instead of silently succeeding - the sole outlier found by a
    /// systematic comparison across the whole public surface.</summary>
    [Fact]
    public void Close_WhenDisposed_ThrowsObjectDisposedException()
    {
        var window = new Window();
        window.Dispose();

        _ = Should.Throw<ObjectDisposedException>(window.Close);
    }

    /// <summary>Verifies Close() off the owning dispatcher throws InvalidOperationException
    /// before running any CloseRequested or Closing handler, instead of running framework-invoked
    /// callbacks on the wrong thread ahead of a dispatcher-affinity fault.</summary>
    [Fact]
    public async Task Close_WhenCalledOffDispatcher_ThrowsBeforeRunningHandlersAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var window = new Window();
        var root = new Overlay { Children = { window } };
        var handlerRan = false;

        await dispatcher.InvokeAsync(() =>
        {
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            window.CloseRequested += (_, _) => handlerRan = true;
            window.Closing += (_, _) => handlerRan = true;
        }, TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(window.Close);
        handlerRan.ShouldBeFalse();
    }

    /// <summary>Verifies Close() closes regardless of CanClose, matching modal outside-dismissal's own
    /// behavior - CanClose only gates the close affordance and CloseOnEscape.</summary>
    [Fact]
    public async Task Close_WhenCanCloseIsFalse_StillClosesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window { CanMove = false, CanClose = false, HeaderPlacement = WindowTitlePlacement.Center };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);

            window.Close();

            window.Visibility.ShouldBe(Visibility.Collapsed);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Escape actually collapses a closable, presented window by default, instead of only raising Closing.</summary>
    [Fact]
    public async Task Dispatch_WhenDialogEscapeHasNoCancelButton_CollapsesWindowByDefaultAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanMove = false,
                CanClose = true,
                CloseOnEscape = true,
                HeaderPlacement = WindowTitlePlacement.Center
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);

            _ = Router.Route(window, Events.Key, Key(Code.Escape));

            window.Visibility.ShouldBe(Visibility.Collapsed);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Closed fires once the window's default close behavior actually collapses it.</summary>
    [Fact]
    public async Task Dispatch_WhenDialogEscapeHasNoCancelButton_RaisesClosedAfterClosingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var closing = 0;
            var closed = 0;
            var window = new Window
            {
                CanMove = false,
                CanClose = true,
                CloseOnEscape = true,
                HeaderPlacement = WindowTitlePlacement.Center
            };
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);

            _ = Router.Route(window, Events.Key, Key(Code.Escape));

            closing.ShouldBe(1);
            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Closing handler that already hides the window is not double-collapsed.</summary>
    [Fact]
    public async Task Dispatch_WhenClosingHandlerAlreadyHidesWindow_DoesNotDoubleCollapseAsync()
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
            window.Closing += (_, _) => window.Visibility = Visibility.Collapsed;
            window.Closed += (_, _) => closed++;
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);

            _ = Router.Route(window, Events.Key, Key(Code.Escape));

            window.Visibility.ShouldBe(Visibility.Collapsed);
            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Closing handler that re-shows the window keeps it open instead of being force-collapsed.</summary>
    [Fact]
    public async Task Dispatch_WhenClosingHandlerReopensTheWindow_LeavesItVisibleAsync()
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
            window.Closing += (_, _) =>
            {
                window.Visibility = Visibility.Collapsed;
                window.Visibility = Visibility.Visible;
            };
            window.Closed += (_, _) => closed++;
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);

            _ = Router.Route(window, Events.Key, Key(Code.Escape));

            window.Visibility.ShouldBe(Visibility.Visible);
            closed.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the close-affordance press collapses the window by default, matching Escape and Dismiss.</summary>
    [Fact]
    public async Task Close_WhenPrimaryPressReleasesOnTarget_CollapsesWindowByDefaultAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = ClosableWindow();
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            using PointerManager pointer = new(root);

            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Press));
            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Release));

            window.Visibility.ShouldBe(Visibility.Collapsed);
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
            new LayoutEngine().Layout(root, new Size(20, 8));
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
            new LayoutEngine().Layout(root, new Size(20, 8));
            root.Attach(dispatcher);
            using PointerManager pointer = new(root);

            _ = pointer.Dispatch(Pointer(new Point(4, 0), PointerAction.Press));
            _ = pointer.Dispatch(Pointer(new Point(12, 0), PointerAction.Move));
            _ = pointer.Dispatch(Pointer(new Point(12, 0), PointerAction.Release));

            closing.ShouldBe(0);
            Overlay.GetLeft(window).ShouldBe(Length.Cells(0));
            Overlay.GetTop(window).ShouldBe(Length.Cells(0));
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
            new LayoutEngine().Layout(root, new Size(20, 8));
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
            new LayoutEngine().Layout(root, new Size(20, 8));
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
                CanClose = false
            };
            Overlay.SetLeft(window, Length.Cells(2));
            Overlay.SetTop(window, Length.Cells(1));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(30, 15));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(5, 1), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);
            _ = capture.Dispatch(Pointer(new Point(8, 3), PointerAction.Move));

            Overlay.GetLeft(window).ShouldBe(Length.Cells(5));
            Overlay.GetTop(window).ShouldBe(Length.Cells(3));
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
                CanClose = false
            };
            Overlay.SetLeft(window, Length.Cells(2));
            Overlay.SetTop(window, Length.Cells(1));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(30, 15));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(5, 1), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(6, 2), PointerAction.Move));
            _ = capture.Dispatch(Pointer(new Point(8, 4), PointerAction.Move));
            _ = capture.Dispatch(Pointer(new Point(11, 5), PointerAction.Move));

            Overlay.GetLeft(window).ShouldBe(Length.Cells(8));
            Overlay.GetTop(window).ShouldBe(Length.Cells(5));
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
                Height = Length.Cells(4)
            };
            Overlay.SetLeft(window, Length.Cells(2));
            Overlay.SetTop(window, Length.Cells(1));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(
                Pointer(new Point(window.Bounds.X + 2, window.Bounds.Y), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(100, 100), PointerAction.Move));

            Overlay.GetLeft(window).ShouldBe(Length.Cells(6));
            Overlay.GetTop(window).ShouldBe(Length.Cells(4));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies dragging the title bar of a Right/Bottom-anchored, Auto-sized Window moves
    /// it instead of stretching it. A still-live trailing anchor left uncleared alongside drag's
    /// freshly written Left/Top would otherwise make Overlay.Outer treat both offsets as a hard
    /// stretch inset for an Auto-sized child, ballooning the window's width as it is dragged.</summary>
    [Fact]
    public async Task Drag_WhenWindowIsAnchoredTrailing_MovesWithoutStretchingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var canvas = new Overlay();
            var window = new Window
            {
                Header = "Anchored",
                CanClose = false
            };
            Overlay.SetRight(window, Length.Cells(2));
            Overlay.SetTop(window, Length.Cells(1));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(30, 15));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            var originalWidth = window.Bounds.Width;
            var originalHeight = window.Bounds.Height;
            var originalX = window.Bounds.X;

            _ = capture.Dispatch(Pointer(new Point(window.Bounds.X + 1, window.Bounds.Y), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);
            _ = capture.Dispatch(
                Pointer(new Point(Math.Max(0, window.Bounds.X - 5), window.Bounds.Y + 1), PointerAction.Move));

            new LayoutEngine().Layout(canvas, new Size(30, 15));

            window.Bounds.Width.ShouldBe(originalWidth);
            window.Bounds.Height.ShouldBe(originalHeight);
            window.Bounds.X.ShouldBeLessThan(originalX);
            Overlay.GetRight(window).ShouldBeNull();
            Overlay.GetBottom(window).ShouldBeNull();
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
            Height = Length.Cells(6)
        };
        Overlay.SetLeft(window, Length.Cells(5));
        Overlay.SetTop(window, Length.Cells(5));
        canvas.Children.Add(window);

        // Act
        new LayoutEngine().Layout(canvas, new Size(8, 5));

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
        new LayoutEngine().Layout(canvas, new Size(30, 12));

        // Assert
        window.Bounds.ShouldBe(new Rect(10, 4, 10, 4));
        Overlay.GetLeft(window).ShouldBeNull();
        Overlay.GetTop(window).ShouldBeNull();
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
                Height = Length.Cells(4)
            };
            Overlay.SetLeft(window, Length.Cells(0));
            Overlay.SetTop(window, Length.Cells(0));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(outer, new Size(40, 20));
            outer.Attach(dispatcher);
            using PointerManager capture = new(outer);

            var titleY = window.Bounds.Y;
            var titleX = window.Bounds.X + 2;
            _ = capture.Dispatch(Pointer(new Point(titleX, titleY), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);
            _ = capture.Dispatch(Pointer(new Point(titleX + 3, titleY + 2), PointerAction.Move));

            Overlay.GetLeft(window).ShouldBe(Length.Cells(3));
            Overlay.GetTop(window).ShouldBe(Length.Cells(2));
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
                CanMove = false
            };
            Overlay.SetLeft(window, Length.Cells(1));
            Overlay.SetTop(window, Length.Cells(1));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(3, 1), PointerAction.Press));
            capture.Captured.ShouldNotBeSameAs(window);

            Overlay.GetLeft(window).ShouldBe(Length.Cells(1));
            Overlay.GetTop(window).ShouldBe(Length.Cells(1));
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
                CanClose = false
            };
            Overlay.SetLeft(window, Length.Cells(0));
            Overlay.SetTop(window, Length.Cells(0));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(3, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(window);
            _ = capture.Dispatch(Pointer(new Point(5, 2), PointerAction.Move));
            _ = capture.Dispatch(Pointer(new Point(5, 2), PointerAction.Release));

            capture.Captured.ShouldBeNull();
            Overlay.GetLeft(window).ShouldBe(Length.Cells(2));
            Overlay.GetTop(window).ShouldBe(Length.Cells(2));
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
                Height = Length.Cells(4)
            };
            Overlay.SetLeft(window, Length.Cells(0));
            Overlay.SetTop(window, Length.Cells(0));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(20, 10));
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
                Height = Length.Cells(4)
            };
            Overlay.SetLeft(window, Length.Cells(0));
            Overlay.SetTop(window, Length.Cells(0));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(20, 10));
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
                CanClose = false
            };
            Overlay.SetLeft(window, Length.Cells(2));
            Overlay.SetTop(window, Length.Cells(2));
            canvas.Children.Add(window);
            new LayoutEngine().Layout(canvas, new Size(20, 10));
            canvas.Attach(dispatcher);
            using PointerManager capture = new(canvas);

            _ = capture.Dispatch(Pointer(new Point(5, 2), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(0, 0), PointerAction.Move));

            Overlay.GetLeft(window).ShouldBe(Length.Cells(0));
            Overlay.GetTop(window).ShouldBe(Length.Cells(0));
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

    /// <summary>Verifies a header carrying a terminal control character is rejected instead of
    /// silently disagreeing with the measured/painted width later.</summary>
    [Theory]
    [InlineData("Save\nAs")]
    [InlineData("Save\rAs")]
    [InlineData("Save\tAs")]
    public void Header_WhenContainingControlCharacter_Throws(string header)
    {
        var window = new Window();

        _ = Should.Throw<ArgumentException>(() => window.Header = header);
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

    private static Window ClosableWindow()
    {
        var window = new Window
        {
            Width = Length.Cells(14),
            Height = Length.Cells(4),
            CanClose = true
        };
        Overlay.SetLeft(window, Length.Cells(0));
        Overlay.SetTop(window, Length.Cells(0));
        return window;
    }

    private static string ReadRow(Frame frame, int start, int length) =>
        string.Concat(Enumerable.Range(start, length).Select(x => FrameOracle.Get(frame, new Point(x, 0))));

    private static KeyEventArgs Key(Code code, KeyAction action = KeyAction.Press) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        action));

    #region Presentation lifetime

    /// <summary>Verifies the default policy ignores outside input and one Window cannot own two live presentations.</summary>
    [Fact]
    public async Task ShowModal_WhenPresentationIsAlreadyLive_DefaultsToIgnoreAndRejectsDuplicateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = window.ShowModal();

            var exception = Should.Throw<InvalidOperationException>(() => window.ShowModal());

            exception.Message.ShouldBe("The Window already has an active modal presentation.");
            scope.OutsideInteraction.ShouldBe(OutsideInteraction.Ignore);
            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            window.Visibility.ShouldBe(Visibility.Visible);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a duplicate attempted from modal-entry callbacks cannot disturb the entering presentation.</summary>
    [Fact]
    public async Task ShowModal_WhenFocusCallbackReenters_RejectsNestedCallAndKeepsOuterPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Hidden };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            InvalidOperationException? nested = null;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    nested = Should.Throw<InvalidOperationException>(() => window.ShowModal());
                }
            };

            using var scope = window.ShowModal();

            nested.ShouldNotBeNull().Message.ShouldBe("Window modal presentations cannot be reentered.");
            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            window.Visibility.ShouldBe(Visibility.Visible);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies external disposal ends only modality and permits another presentation of the visible Window.</summary>
    [Fact]
    public async Task ShowModal_WhenScopeIsDisposedExternally_LeavesWindowVisibleAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            var first = window.ShowModal(initialFocus: action);
            first.Dispose();

            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Visible);

            using var second = window.ShowModal(initialFocus: action);

            second.ShouldNotBeSameAs(first);
            second.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
            window.Visibility.ShouldBe(Visibility.Visible);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the "attach, then immediately ShowModal" idiom used throughout this class
    /// still lets ModalityManager.Enter's background-focus snapshot capture the true pre-attach
    /// background control. A Window attached while already Visible schedules a deferred
    /// post-attach focus fallback for a later dispatcher tick; if that fallback instead ran
    /// synchronously inside OnAttached, it would fire unconditionally (ShowModal cannot make
    /// ModalityOwner non-null until after OnAttached returns, so _isShowingModal is always false
    /// throughout it) and steal focus onto the dialog's own content before ShowModal ever takes
    /// its snapshot - corrupting it with a descendant of the dialog Window itself instead of the
    /// separate sibling Window's control that was genuinely focused first.</summary>
    [Fact]
    public async Task ShowModal_WhenWindowAttachesAndShowsInTheSameTick_RestoresTruePreAttachBackgroundFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var backgroundWindow = new Window { Content = background };
            var root = new Overlay { Children = { backgroundWindow } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            // The dialog Window does not exist yet - "background" is the true pre-attach focus,
            // owned by an entirely separate sibling Window.
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action };
            root.Children.Add(window);
            var scope = window.ShowModal(initialFocus: action);

            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            focus.Focused.ShouldBeSameAs(action);

            scope.Dispose();

            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an exit callback may reopen without old-scope cleanup erasing the replacement.</summary>
    [Fact]
    public async Task ShowModal_WhenExternalExitCallbackReopens_TracksReplacementByIdentityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var first = window.ShowModal();
            ModalScope? replacement = null;
            first.Exited += (_, _) => replacement = window.ShowModal();

            first.Dispose();

            first.IsActive.ShouldBeFalse();
            replacement.ShouldNotBeNull().IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(replacement);
            window.Visibility.ShouldBe(Visibility.Visible);
            replacement.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope disposed from entry callbacks is returned inactive without stale Window tracking.</summary>
    [Fact]
    public async Task ShowModal_WhenEntryCallbackDisposesScope_ReturnsInactiveAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var disposeOnEntry = true;
            focus.Gained += (_, eventArgs) =>
            {
                if (disposeOnEntry && ReferenceEquals(eventArgs.Current, action))
                {
                    modality.Active.ShouldNotBeNull().Dispose();
                }
            };

            var first = window.ShowModal();

            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Visible);
            disposeOnEntry = false;

            using var second = window.ShowModal();

            second.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Window hidden from entry callbacks returns an inactive, untracked presentation.</summary>
    [Fact]
    public async Task ShowModal_WhenEntryCallbackHidesWindow_ReturnsInactiveWithoutRestoringVisibilityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    window.Visibility = Visibility.Hidden;
                }
            };

            var scope = window.ShowModal();

            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Hidden);
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Visibility, focus, and failure recovery

    /// <summary>Verifies hiding or collapsing a modal Window exits before its visibility notification.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public async Task Visibility_WhenModalWindowBecomesUnavailable_ExitsAndRestoresBeforeNotificationAsync(
        Visibility visibility)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { background, window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var scope = window.ShowModal(initialFocus: action);
            var observations = 0;
            var closing = 0;
            var closed = 0;
            var presentedBounds = window.SurfaceBounds;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility) &&
                    window.Visibility != Visibility.Visible)
                {
                    observations++;
                    scope.IsActive.ShouldBeFalse();
                    modality.Active.ShouldBeNull();
                    focus.Focused.ShouldBeSameAs(background);
                }
            };

            window.Visibility = visibility;

            observations.ShouldBe(1);
            scope.IsActive.ShouldBeFalse();
            window.Visibility.ShouldBe(visibility);
            focus.Focused.ShouldBeSameAs(background);
            window.SurfaceBounds.ShouldBe(default);
            closing.ShouldBe(0);
            closed.ShouldBe(0);

            window.Visibility = Visibility.Visible;

            window.SurfaceBounds.ShouldBe(presentedBounds);
            modality.Active.ShouldBeNull();
            closing.ShouldBe(0);
            closed.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies forced detachment silently clears Window presentation and modality.</summary>
    [Fact]
    public async Task Detach_WhenModalWindowIsRemoved_DoesNotPublishCloseLifecycleAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var window = new Window { Width = Length.Cells(12), Height = Length.Cells(5) };
            using var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();

            root.Children.Remove(window).ShouldBeTrue();

            closing.ShouldBe(0);
            closed.ShouldBe(0);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Dispatcher.ShouldBeNull();
            window.SurfaceBounds.ShouldBe(default);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies forced disposal silently clears Window presentation and modality.</summary>
    [Fact]
    public async Task Dispose_WhenModalWindowIsDisposed_DoesNotPublishCloseLifecycleAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window { Width = Length.Cells(12), Height = Length.Cells(5) };
            using var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();

            window.Dispose();

            closing.ShouldBe(0);
            closed.ShouldBe(0);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.IsDisposed.ShouldBeTrue();
            window.SurfaceBounds.ShouldBe(default);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failing modal exit cannot suppress visibility publication or replace its first failure.</summary>
    [Fact]
    public async Task Visibility_WhenModalExitCallbackFails_CompletesTransitionAndPreservesFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal exit callback failed.");
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = window.ShowModal();
            scope.Exited += (_, _) => throw expected;
            var published = 0;
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
                {
                    published++;
                    scope.IsActive.ShouldBeFalse();
                    modality.Active.ShouldBeNull();
                }
            };

            var exception = Should.Throw<InvalidOperationException>(() =>
                window.Visibility = Visibility.Collapsed);

            exception.ShouldBeSameAs(expected);
            published.ShouldBe(1);
            window.Visibility.ShouldBe(Visibility.Collapsed);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies explicit modal focus bypasses the legacy first-descendant visibility autofocus.</summary>
    [Fact]
    public async Task ShowModal_WhenInitialFocusIsProvided_FocusesOnlyThatDescendantAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var first = new ProbeControl { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            var content = new Overlay { Children = { first, second } };
            var window = new Window { Content = content, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var gained = new List<ControlBase?>();
            focus.Gained += (_, eventArgs) => gained.Add(eventArgs.Current);

            using var scope = window.ShowModal(OutsideInteraction.Ignore, second);

            gained.ShouldBe([second]);
            first.IsFocused.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(second);
            scope.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies invalid focus restores the exact pre-call Window visibility and background focus.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public async Task ShowModal_WhenInitialFocusIsOutsideWindow_RestoresPriorVisibilityAsync(
        Visibility visibility)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = visibility };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var exception = Should.Throw<ArgumentException>(() =>
                window.ShowModal(initialFocus: background));

            exception.ParamName.ShouldBe("initialFocus");
            window.Visibility.ShouldBe(visibility);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an exposure callback failure rolls the Window back to its exact prior visibility.</summary>
    [Fact]
    public async Task ShowModal_WhenVisibilityCallbackFails_RestoresPriorVisibilityAndFailureIdentityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The visibility callback failed.");
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Collapsed };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility) &&
                    window.Visibility == Visibility.Visible)
                {
                    throw expected;
                }
            };

            var exception = Should.Throw<InvalidOperationException>(() => window.ShowModal());

            exception.ShouldBeSameAs(expected);
            window.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal-entry failure wins over rollback callback failure and restores prior visibility.</summary>
    [Fact]
    public async Task ShowModal_WhenEntryAndRollbackCallbacksFail_PreservesEntryFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal focus callback failed.");
            var background = new ProbeControl { IsFocusable = true };
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Hidden };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    throw expected;
                }
            };
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility) &&
                    window.Visibility == Visibility.Hidden)
                {
                    throw new InvalidOperationException("The rollback callback failed.");
                }
            };

            var exception = Should.Throw<InvalidOperationException>(() => window.ShowModal());

            exception.ShouldBeSameAs(expected);
            window.Visibility.ShouldBe(Visibility.Hidden);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies policy validation occurs before a hidden Window is exposed.</summary>
    [Fact]
    public async Task ShowModal_WhenOutsideInteractionIsUndefined_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window { Content = action, Visibility = Visibility.Hidden };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var visibilityChanges = 0;
            window.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
                {
                    visibilityChanges++;
                }
            };

            var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
                window.ShowModal((OutsideInteraction) int.MaxValue));

            exception.ParamName.ShouldBe("outsideInteraction");
            visibilityChanges.ShouldBe(0);
            window.Visibility.ShouldBe(Visibility.Hidden);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Modal interaction

    /// <summary>Verifies default and cancel Button clicks alone decide whether the modal Window remains presented.</summary>
    [Fact]
    public async Task ShowModal_WhenDefaultAndCancelButtonsRun_ClickHandlersOwnVisibilityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var editor = new ProbeControl { IsFocusable = true };
            var accept = new Button { IsDefault = true };
            var cancel = new Button { IsCancel = true };
            var accepted = 0;
            var cancelled = 0;
            accept.Click += (_, _) => accepted++;
            var window = new Window
            {
                Content = new Stack { Children = { editor, accept, cancel } },
                Visibility = Visibility.Collapsed,
            };
            cancel.Click += (_, _) =>
            {
                cancelled++;
                window.Visibility = Visibility.Hidden;
            };
            var root = new Overlay { Children = { window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = window.ShowModal(initialFocus: editor);

            _ = Router.Route(editor, Events.Key, Key(Code.Enter));

            accepted.ShouldBe(1);
            scope.IsActive.ShouldBeTrue();
            window.Visibility.ShouldBe(Visibility.Visible);

            _ = Router.Route(editor, Events.Key, Key(Code.Escape));

            cancelled.ShouldBe(1);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Hidden);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the close glyph collapses the Window and ends its modal scope on a single press by default.</summary>
    [Fact]
    public async Task ShowModal_WhenCloseGlyphRequestsClosing_ClosesOnOnePressByDefaultAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { IsFocusable = true };
            var window = new Window
            {
                CanClose = true,
                Content = action,
                Width = Length.Cells(12),
                Height = Length.Cells(5),
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) => closing++;
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();
            var close = new Point(window.Bounds.X + 4, window.Bounds.Y);

            _ = pointer.Dispatch(Pointer(close, PointerAction.Press));
            _ = pointer.Dispatch(Pointer(close, PointerAction.Release));

            closing.ShouldBe(1);
            closed.ShouldBe(1);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Collapsed);
            window.SurfaceBounds.ShouldBe(default);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Closing handler that explicitly hides the Window on its own is not double-collapsed.</summary>
    [Fact]
    public async Task ShowModal_WhenClosingHandlerHidesWindowItself_DoesNotDoubleCloseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanClose = true,
                Width = Length.Cells(12),
                Height = Length.Cells(5),
            };
            var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) =>
            {
                closing++;
                window.Visibility = Visibility.Hidden;
            };
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();
            var close = new Point(window.Bounds.X + 4, window.Bounds.Y);

            _ = pointer.Dispatch(Pointer(close, PointerAction.Press));
            _ = pointer.Dispatch(Pointer(close, PointerAction.Release));

            closing.ShouldBe(1);
            closed.ShouldBe(1);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Hidden);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a close owner may hide then restore the Window without leaving presentation state stale.</summary>
    [Fact]
    public async Task ShowModal_WhenClosingHandlerRestoresVisibility_ReopensPresentedAndModelessAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var window = new Window
            {
                CanClose = true,
                Width = Length.Cells(12),
                Height = Length.Cells(5)
            };
            using var root = new Overlay { Children = { window } };
            new LayoutEngine().Layout(root, new Size(24, 10));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var closing = 0;
            var closed = 0;
            window.Closing += (_, _) =>
            {
                closing++;
                window.Visibility = Visibility.Hidden;
                window.Visibility = Visibility.Visible;
            };
            window.Closed += (_, _) => closed++;
            var scope = window.ShowModal();
            var close = new Point(window.Bounds.X + 4, window.Bounds.Y);

            _ = pointer.Dispatch(Pointer(close, PointerAction.Press));
            _ = pointer.Dispatch(Pointer(close, PointerAction.Release));

            closing.ShouldBe(1);
            closed.ShouldBe(0);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            window.Visibility.ShouldBe(Visibility.Visible);
            window.SurfaceBounds.ShouldBe(window.Bounds);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Modeless compatibility

    /// <summary>Verifies ordinary modeless visibility continues to focus the first eligible descendant.</summary>
    [Fact]
    public async Task Visibility_WhenWindowIsShownModelessly_FocusesFirstDescendantAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { IsFocusable = true };
            var first = new ProbeControl { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            var window = new Window
            {
                Content = new Overlay { Children = { first, second } },
                Visibility = Visibility.Hidden,
            };
            var root = new Overlay { Children = { background, window } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            window.Visibility = Visibility.Visible;

            focus.Focused.ShouldBeSameAs(first);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region MessageBox scope stacking

    /// <summary>Verifies MessageBox.ShowAsync inside a modal window stacks modal scopes correctly.</summary>
    [Fact]
    public async Task ShowAsync_WhenCalledInsideModalWindow_StacksScopesAndRestoresWindowFocusAsync()
    {
        // Arrange
        var trigger = new Button { Text = "Trigger" };
        var parentWindow = new Window
        {
            Content = trigger,
            Visibility = Visibility.Collapsed,
        };
        var host = new Overlay { Children = { parentWindow } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        ModalScope? windowScope = null;

        // Act — show the window modally
        await surface.UpdateAsync(
            () => windowScope = parentWindow.ShowModal(),
            "show modal window");

        // Assert — window scope is active
        windowScope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(windowScope);

        // Act — show a MessageBox from the trigger button inside the modal window
        Task<MessageBoxResult>? messagePending = null;
        await surface.UpdateAsync(
            () => messagePending = MessageBox.ShowAsync(trigger, "Continue?", "Confirm"),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var messageBoxWindow = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();

        // Assert — both scopes are active; MessageBox scope is youngest
        var messageBoxScope = surface.Application.Modality.Active.ShouldNotBeNull();
        messageBoxScope.ShouldNotBeSameAs(windowScope);
        messageBoxScope.Root.ShouldBeSameAs(messageBoxWindow);
        windowScope.IsActive.ShouldBeTrue();

        // Act — press Escape to dismiss the MessageBox
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert — MessageBox dismissed, window scope restored
        (await messagePending!).ShouldBe(MessageBoxResult.Cancel);
        windowScope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(windowScope);

        // Clean up
        await surface.UpdateAsync(windowScope.Dispose, "end window modal");
    }

    #endregion
}
