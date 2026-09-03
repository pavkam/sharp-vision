// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies mounted Pager rendering and input against committed target snapshots.</summary>
public sealed class PagerSurfaceTests
{
    /// <summary>Verifies the one-page ideal sequence is only its sole numbered page rather than
    /// four inert navigation glyphs that arranged layout never creates.</summary>
    [Fact]
    public void Measure_WhenOnlyOnePage_UsesSoleNumberWidth()
    {
        var pager = new Pager { PageCount = 1 };

        pager.Measure(new Constraint(width: null, height: 1));

        pager.DesiredSize.ShouldBe(new Size(1, 1));
    }

    /// <summary>Verifies unbounded measure computes complete ideal width without retaining one target per page.</summary>
    [Fact]
    public void Measure_WhenIdealWindowIsLarge_ComputesExactWidth()
    {
        const int pageCount = 10_000;
        var pager = new Pager
        {
            PageCount = pageCount,
            PageIndex = pageCount / 2,
            MaximumVisiblePages = pageCount - 2
        };
        var numericWidth = Enumerable.Range(1, pageCount)
            .Sum(static page => page.ToString(CultureInfo.InvariantCulture).Length);
        var expectedWidth = numericWidth + 4 + pageCount + 3;

        pager.Measure(new Constraint(width: null, height: 1));

        pager.DesiredSize.ShouldBe(new Size(expectedWidth, 1));
    }

    /// <summary>Verifies an unbounded middle-page layout emits navigation, endpoint numbers, gaps, and window pages in source order.</summary>
    [Fact]
    public void Render_WhenMiddlePageHasRoom_WritesCompleteIdealSequence()
    {
        var pager = new Pager
        {
            PageCount = 10,
            PageIndex = 4,
            MaximumVisiblePages = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(29, 1));
        using Frame frame = new(new Size(29, 1));

        pager.Render(frame.Canvas);

        Row(frame, 29).TrimEnd().ShouldBe("« ‹ 1 … 3 4 5 6 … 10 › »");
    }

    /// <summary>Verifies the one-page layout paints only its sole current page and exposes no
    /// interactive navigation target.</summary>
    [Fact]
    public void Render_WhenOnlyOnePage_PaintsOnlyCurrentNumber()
    {
        var pager = new Pager { PageCount = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
        new LayoutEngine().Layout(pager, new Size(9, 1));
        using Frame frame = new(new Size(9, 1));

        pager.Render(frame.Canvas);

        Row(frame, 9).ShouldBe("1        ");
        var target = pager.LayoutSnapshot.Targets.ShouldHaveSingleItem();
        target.Kind.ShouldBe(PagerTargetKind.Number);
        target.IsCurrent.ShouldBeTrue();
        target.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies an empty range emits no target, text, or imaginary page zero.</summary>
    [Fact]
    public void Render_WhenPageCountIsZero_PaintsNoTargets()
    {
        var pager = new Pager { HorizontalAlignment = HorizontalAlignment.Stretch };
        new LayoutEngine().Layout(pager, new Size(9, 1));
        using Frame frame = new(new Size(9, 1));

        pager.Render(frame.Canvas);

        Row(frame, 9).ShouldBe("         ");
        pager.LayoutSnapshot.Targets.ShouldBeEmpty();
    }

    /// <summary>Verifies MaximumVisiblePages budgets only neighboring interior numbers while the
    /// current and endpoint numbers remain independently required.</summary>
    [Fact]
    public void Render_WhenMaximumVisiblePagesIsOne_RetainsOneLeftNeighborAndEndpoints()
    {
        var pager = new Pager
        {
            PageCount = 10,
            PageIndex = 4,
            MaximumVisiblePages = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(25, 1));
        using Frame frame = new(new Size(25, 1));

        pager.Render(frame.Canvas);

        Row(frame, 25).TrimEnd().ShouldBe("« ‹ 1 … 4 5 … 10 › »");
    }

    /// <summary>Verifies a rejected wide final-page number leaves its cheaper omission eligible
    /// before lower-priority navigation glyphs consume the remaining cells.</summary>
    [Fact]
    public void Render_WhenFinalPageNumberDoesNotFit_RetainsTrailingOmissionBeforeNavigation()
    {
        var pager = new Pager
        {
            PageCount = 1_000,
            PageIndex = 1,
            MaximumVisiblePages = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(7, 1));
        using Frame frame = new(new Size(7, 1));

        pager.Render(frame.Canvas);

        Row(frame, 7).ShouldBe("1 2 3 …");
    }

    /// <summary>Verifies page numbers always use invariant ASCII formatting even when process
    /// culture uses a different native numbering system.</summary>
    [Fact]
    public void Render_WhenCurrentCultureChanges_RemainsInvariant()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            CultureInfo.CurrentUICulture = new CultureInfo("fa-IR");
            var pager = new Pager
            {
                PageCount = 20,
                PageIndex = 11,
                MaximumVisiblePages = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            new LayoutEngine().Layout(pager, new Size(30, 1));
            using Frame frame = new(new Size(30, 1));

            pager.Render(frame.Canvas);

            Row(frame, 30).TrimEnd().ShouldBe("« ‹ 1 … 11 12 … 20 › »");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>Verifies a preferred ambiguous-width glyph degrades to its portable fallback
    /// under the live wide-cell policy.</summary>
    [Fact]
    public void Render_WhenPreferredGlyphBecomesWide_UsesFallback()
    {
        var pager = new Pager
        {
            PageCount = 3,
            PageIndex = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Style = PagerStyle.Default with
            {
                FirstPageGlyph = new ControlGlyph(new Rune('•'), new Rune('F'))
            }
        };
        pager.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(pager, new Size(20, 1));
        using Frame frame = new(new Size(20, 1), ambiguousWidth: Ambiguous.Wide);

        pager.Render(frame.Canvas);

        pager.LayoutSnapshot.Targets[0].Text.ShouldBe("F");
        FrameOracle.Get(frame, default).ShouldBe("F");
    }

    /// <summary>Verifies a glyph target disappears completely when both preferred and fallback
    /// scalars become wide under the live policy.</summary>
    [Fact]
    public void Arrange_WhenPreferredAndFallbackGlyphsBecomeWide_OmitsWholeTarget()
    {
        var pager = new Pager
        {
            PageCount = 3,
            PageIndex = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Style = PagerStyle.Default with
            {
                FirstPageGlyph = new ControlGlyph(new Rune('•'), new Rune('·'))
            }
        };
        pager.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));

        new LayoutEngine().Layout(pager, new Size(20, 1));

        pager.LayoutSnapshot.Targets.ShouldNotContain(static target => target.Kind == PagerTargetKind.First);
    }

    /// <summary>Verifies ordinary, current, omitted, and disabled endpoint targets resolve their
    /// documented semantic roles rather than sharing one fallback foreground.</summary>
    [Fact]
    public void Render_WhenTargetKindsDiffer_UsesCodeOwnedSemanticColors()
    {
        var json = ThemeJson.Create(foreground: "#112233", accent: "#445566")
            .Replace("\"__disabledText\":\"#707070\"", "\"__disabledText\":\"#773311\"", StringComparison.Ordinal)
            .Replace("\"__muted\":\"#707070\"", "\"__muted\":\"#117733\"", StringComparison.Ordinal);
        var theme = ThemeCatalog.Parse(json);
        var endpointPager = new Pager
        {
            PageCount = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        endpointPager.SetTheme(theme);
        new LayoutEngine().Layout(endpointPager, new Size(20, 1));
        using Frame endpointFrame = new(new Size(20, 1));
        endpointPager.Render(endpointFrame.Canvas);
        var first = endpointPager.LayoutSnapshot.Targets.Single(static target => target.Kind == PagerTargetKind.First);
        var current = endpointPager.LayoutSnapshot.Targets.Single(static target => target.IsCurrent);
        var ordinary = endpointPager.LayoutSnapshot.Targets.Single(target =>
            target.Kind == PagerTargetKind.Number && target.PageIndex == 1);

        endpointFrame.GetCell(Origin(first.Bounds)).Style.Foreground.ShouldBe(Color.FromHex("#773311"));
        endpointFrame.GetCell(Origin(current.Bounds)).Style.Foreground.ShouldBe(Color.FromHex("#445566"));
        endpointFrame.GetCell(Origin(ordinary.Bounds)).Style.Foreground.ShouldBe(Color.FromHex("#112233"));

        var omissionPager = new Pager
        {
            PageCount = 10,
            PageIndex = 4,
            MaximumVisiblePages = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        omissionPager.SetTheme(theme);
        new LayoutEngine().Layout(omissionPager, new Size(30, 1));
        using Frame omissionFrame = new(new Size(30, 1));
        omissionPager.Render(omissionFrame.Canvas);
        var omission = omissionPager.LayoutSnapshot.Targets.First(static target => target.Kind == PagerTargetKind.Omitted);

        omissionFrame.GetCell(Origin(omission.Bounds)).Style.Foreground.ShouldBe(Color.FromHex("#117733"));
    }

    /// <summary>Verifies focus keeps each Pager target's semantic foreground visible instead of
    /// reversing the whole borderless owner into target-colored background blocks.</summary>
    [Fact]
    public async Task Render_WhenFocusedAtFinalPage_PreservesSemanticTargetColorsAsync()
    {
        // Arrange
        var pager = new Pager
        {
            PageCount = 18,
            PageIndex = 17,
            MaximumVisiblePages = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            pager,
            new Size(40, 1),
            ThemeCatalog.Dark,
            TestContext.Current.CancellationToken);
        var restingBackground = surface.Cell(new Point(
            pager.ContentBounds.Right - 1,
            pager.ContentBounds.Y)).Style.Background;

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(pager);
        var ordinary = pager.LayoutSnapshot.Targets.Single(target =>
            target.Kind == PagerTargetKind.Number && target.PageIndex == 16);
        var current = pager.LayoutSnapshot.Targets.Single(static target => target.IsCurrent);
        var omission = pager.LayoutSnapshot.Targets.First(static target => target.Kind == PagerTargetKind.Omitted);
        var disabled = pager.LayoutSnapshot.Targets.Single(static target => target.Kind == PagerTargetKind.Next);
        var ordinaryCell = surface.Cell(Origin(ordinary.Bounds));
        var currentCell = surface.Cell(Origin(current.Bounds));
        var omissionCell = surface.Cell(Origin(omission.Bounds));
        var disabledCell = surface.Cell(Origin(disabled.Bounds));
        var unusedCell = surface.Cell(new Point(pager.ContentBounds.Right - 1, pager.ContentBounds.Y));

        foreach (var cell in new[] { ordinaryCell, currentCell, omissionCell, disabledCell, unusedCell })
        {
            cell.Style.Background.ShouldBe(restingBackground);
            (cell.Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);
        }

        omissionCell.Text.ShouldBe(omission.Text);
        disabledCell.Text.ShouldBe(disabled.Text);
        ordinaryCell.Style.Foreground.ShouldBe(Color.FromHex("#ffffff"));
        currentCell.Style.Foreground.ShouldBe(Color.FromHex("#00ffff"));
        omissionCell.Style.Foreground.ShouldBe(Color.FromHex("#7f7f7f"));
        disabledCell.Style.Foreground.ShouldBe(TerminalPalette.Project(
            ThemeCatalog.Dark.ResolveColor(SemanticColor.DisabledText),
            ColorDepth.Basic16));
    }

    /// <summary>Verifies finite retention keeps the current number before endpoint and nearest-window candidates.</summary>
    [Fact]
    public void Render_WhenWidthIsNarrow_RetainsWholeTargetsByPriority()
    {
        var pager = new Pager
        {
            PageCount = 10,
            PageIndex = 4,
            MaximumVisiblePages = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        pager.Render(frame.Canvas);

        Row(frame, 5).ShouldBe("1 4 5");
        pager.LayoutSnapshot.Targets.Select(static target => target.PageIndex).ShouldBe([0, 3, 4]);
    }

    /// <summary>Verifies an unfittable current number produces no partial cells or pointer target.</summary>
    [Fact]
    public void Render_WhenCurrentNumberDoesNotFit_WritesNothing()
    {
        var pager = new Pager
        {
            PageCount = 100,
            PageIndex = 99,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));

        pager.Render(frame.Canvas);

        Row(frame, 2).ShouldBe("  ");
        pager.LayoutSnapshot.Targets.ShouldBeEmpty();
    }

    /// <summary>Verifies primary release activates the captured numbered-target identity.</summary>
    [Fact]
    public async Task Pointer_WhenNumberIsPressedAndReleased_ChangesPageAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var pager = new Pager { PageCount = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
            pager.Attach(dispatcher);
            new LayoutEngine().Layout(pager, new Size(24, 1));
            using FocusManager focus = new(pager);
            using PointerManager pointer = new(pager);
            PageChangedEventArgs? change = null;
            pager.PageChanged += (_, eventArgs) => change = eventArgs;
            var target = pager.LayoutSnapshot.Targets.Single(item =>
                item.Kind == PagerTargetKind.Number && item.PageIndex == 3);

            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Press));

            pointer.Captured.ShouldBeSameAs(pager);
            pager.IsPressed.ShouldBeTrue();

            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Release));

            pointer.Captured.ShouldBeNull();
            pager.IsPressed.ShouldBeFalse();
            pager.PageIndex.ShouldBe(3);
            change.ShouldNotBeNull().Cause.ShouldBe(ActivationCause.Pointer);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a newer layout cancels capture so one physical cell cannot be reinterpreted.</summary>
    [Fact]
    public async Task Pointer_WhenLayoutChangesBeforeRelease_DoesNotActivateStaleTargetAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var pager = new Pager { PageCount = 20, PageIndex = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
            pager.Attach(dispatcher);
            new LayoutEngine().Layout(pager, new Size(30, 1));
            using FocusManager focus = new(pager);
            using PointerManager pointer = new(pager);
            var target = pager.LayoutSnapshot.Targets.First(item =>
                item.Kind == PagerTargetKind.Number && item.PageIndex != pager.PageIndex);
            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Press));

            new LayoutEngine().Layout(pager, new Size(5, 1));
            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Release));

            pager.PageIndex.ShouldBe(10);
            pointer.Captured.ShouldBeNull();
            pager.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a layout-affecting mutation makes the previously arranged snapshot
    /// non-interactive before its property callbacks can route fresh pointer input.</summary>
    [Theory]
    [InlineData(nameof(Pager.PageCount))]
    [InlineData(nameof(Pager.PageIndex))]
    [InlineData(nameof(Pager.MaximumVisiblePages))]
    [InlineData(nameof(Pager.Style))]
    public async Task Pointer_WhenLayoutMutationObserverPressesBeforeArrange_RejectsStaleTargetAsync(
        string mutation)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var pager = new Pager
            {
                PageCount = 10,
                PageIndex = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            pager.Attach(dispatcher);
            new LayoutEngine().Layout(pager, new Size(30, 1));
            using FocusManager focus = new(pager);
            using PointerManager pointer = new(pager);
            var staleTarget = pager.LayoutSnapshot.Targets.Single(static target =>
                target.Kind == PagerTargetKind.Number && target.PageIndex == 5);
            pager.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != mutation)
                {
                    return;
                }

                _ = pointer.Dispatch(PointerAt(staleTarget.Bounds, PointerAction.Press));
                _ = pointer.Dispatch(PointerAt(staleTarget.Bounds, PointerAction.Release));
            };

            switch (mutation)
            {
                case nameof(Pager.PageCount):
                    pager.PageCount = 2;
                    break;
                case nameof(Pager.PageIndex):
                    pager.PageIndex = 2;
                    break;
                case nameof(Pager.MaximumVisiblePages):
                    pager.MaximumVisiblePages = 1;
                    break;
                case nameof(Pager.Style):
                    pager.Style = PagerStyle.Default with
                    {
                        FirstPageGlyph = new ControlGlyph(new Rune('F'), new Rune('F'))
                    };
                    break;
                default:
                    throw new UnreachableException();
            }

            pager.PageIndex.ShouldBe(mutation switch
            {
                nameof(Pager.PageCount) => 1,
                nameof(Pager.PageIndex) => 2,
                _ => 4
            });
            pointer.Captured.ShouldBeNull();
            pager.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies mounted Tab and repeated-key input flow through the real application,
    /// decoder, focus manager, and routed-event path.</summary>
    [Fact]
    public async Task Keyboard_WhenMountedPagerHasManyPages_TabsAndRepeatsNavigationAsync()
    {
        var pager = new Pager
        {
            PageCount = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            pager,
            new Size(24, 1),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(pager);

        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.RepeatAsync(Code.Right);

        pager.PageIndex.ShouldBe(2);
    }

    /// <summary>Verifies leaving Tab eligibility preserves existing keyboard focus exactly as the
    /// scalar focus contract requires.</summary>
    [Fact]
    public async Task Focus_WhenPageCountBecomesOne_PreservesFocusButLeavesTabTraversalAsync()
    {
        var pager = new Pager
        {
            PageCount = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            pager,
            new Size(20, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(pager);

        await surface.UpdateAsync(() => pager.PageCount = 1, "reduce Pager to one page");

        surface.ShouldHaveFocus(pager);
        pager.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies pointer activation can focus a one-page Pager without committing the
    /// impossible transition or capturing the pointer.</summary>
    [Fact]
    public async Task Pointer_WhenPagerHasOnePage_FocusesWithoutChangingPageAsync()
    {
        var pager = new Pager
        {
            PageCount = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        var changes = 0;
        pager.PageChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            pager,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(pager, default);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        surface.ShouldHaveFocus(pager);
        surface.ShouldHaveCapture(null);
        pager.PageIndex.ShouldBe(0);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies a captured press dragged away from its original target cleans up without
    /// activating that page.</summary>
    [Fact]
    public async Task Pointer_WhenPressedTargetIsDraggedAway_CancelsActivationAsync()
    {
        var pager = new Pager
        {
            PageCount = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            pager,
            new Size(24, 1),
            TestContext.Current.CancellationToken);
        var target = pager.LayoutSnapshot.Targets.Single(static item =>
            item.Kind == PagerTargetKind.Number && item.PageIndex == 3);
        var start = RelativePoint(pager, Origin(target.Bounds));

        await surface.Pointer.MoveToAsync(pager, start);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(pager, new Point(23, 0));
        await surface.Pointer.ReleaseAsync();

        pager.PageIndex.ShouldBe(0);
        pager.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a page-count mutation during a captured gesture releases capture and
    /// prevents the physical release from activating a reinterpreted target.</summary>
    [Fact]
    public async Task Pointer_WhenPageCountChangesWhilePressed_CancelsCapturedIdentityAsync()
    {
        var pager = new Pager
        {
            PageCount = 8,
            PageIndex = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            pager,
            new Size(28, 1),
            TestContext.Current.CancellationToken);
        var target = pager.LayoutSnapshot.Targets.Single(static item =>
            item.Kind == PagerTargetKind.Number && item.PageIndex == 5);
        var point = RelativePoint(pager, Origin(target.Bounds));
        await surface.Pointer.MoveToAsync(pager, point);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pager);

        await surface.UpdateAsync(() => pager.PageCount = 4, "shrink Pager while pressed");
        await surface.Pointer.ReleaseAsync();

        surface.ShouldHaveCapture(null);
        pager.IsPressed.ShouldBeFalse();
        pager.PageIndex.ShouldBe(3);
    }

    /// <summary>Verifies every availability loss during a mounted captured press releases the
    /// original identity without committing a page transition.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pointer_WhenAvailabilityChangesWhilePressed_CancelsCapturedIdentityAsync(bool hide)
    {
        var pager = new Pager
        {
            PageCount = 5,
            PageIndex = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            pager,
            new Size(24, 1),
            TestContext.Current.CancellationToken);
        var target = pager.LayoutSnapshot.Targets.Single(static item =>
            item.Kind == PagerTargetKind.Number && item.PageIndex == 4);
        var point = RelativePoint(pager, Origin(target.Bounds));
        await surface.Pointer.MoveToAsync(pager, point);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pager);

        await surface.UpdateAsync(
            () =>
            {
                if (hide)
                {
                    pager.Visibility = Visibility.Hidden;
                }
                else
                {
                    pager.IsEnabled = false;
                }
            },
            hide ? "hide Pager while pressed" : "disable Pager while pressed");
        await surface.Pointer.ReleaseAsync();

        surface.ShouldHaveCapture(null);
        pager.IsPressed.ShouldBeFalse();
        pager.PageIndex.ShouldBe(2);
    }

    private static Pointer PointerAt(Rect bounds, PointerAction action) => new(
        new Point(bounds.X, bounds.Y),
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    private static Point RelativePoint(Pager pager, Point absolute) => new(
        absolute.X - pager.Bounds.X,
        absolute.Y - pager.Bounds.Y);

    private static Point Origin(Rect bounds) => new(bounds.X, bounds.Y);

    private static string Row(Frame frame, int width)
    {
        var text = new StringBuilder(width);

        for (var x = 0; x < width; x++)
        {
            var cell = FrameOracle.Get(frame, new Point(x, 0));
            _ = text.Append(cell.Length == 0 ? " " : cell);
        }

        return text.ToString();
    }
}
