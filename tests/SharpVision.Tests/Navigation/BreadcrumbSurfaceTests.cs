// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies breadcrumb layout, rendering, focus, and routed input through a mounted application.</summary>
public sealed class BreadcrumbSurfaceTests
{
    /// <summary>Verifies clicking an ancestor after mount rearranges the retained items to the new projection.</summary>
    [Fact]
    public async Task Render_WhenAncestorIsClicked_RearrangesProjectedWindowAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(14, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("Root›Docs›Leaf");

        await surface.Pointer.ClickAsync(breadcrumb.Items[0]);

        surface.ShouldRender("…›Root        ");
    }

    /// <summary>Verifies an explicitly selected ancestor suppresses its later tail even at wide widths.</summary>
    [Fact]
    public async Task Render_WhenAncestorIsExplicitCurrent_ProjectsLaterTailInSourceOrderAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        breadcrumb.CurrentIndex = 0;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(14, 3),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("…›Root        ");
        breadcrumb.Layout.OverflowItems.Select(item => item.Text).ShouldBe(["Docs", "Leaf"]);
    }

    /// <summary>Verifies a wide path renders every complete caption with one-cell separators.</summary>
    [Fact]
    public async Task Render_WhenPathFits_DrawsEveryWholeItemAndSeparatorAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(14, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("Root›Docs›Leaf");
        breadcrumb.Layout.OverflowItems.ShouldBeEmpty();
    }

    /// <summary>Verifies current overflow retains a whole suffix and a leading menu trigger.</summary>
    [Fact]
    public async Task Render_WhenCurrentPathOverflows_DrawsContiguousSuffixAndTriggerAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("…›Leaf  ");
        breadcrumb.Layout.OverflowItems.Select(item => item.Text).ShouldBe(["Root", "Docs"]);
        breadcrumb.Items[0].Bounds.ShouldBe(default);
        breadcrumb.Items[2].Bounds.Width.ShouldBe(4);
    }

    /// <summary>Verifies a one-cell surface presents only the complete overflow affordance.</summary>
    [Fact]
    public async Task Render_WhenOnlyTriggerFits_DrawsNoPartialItemOrSeparatorAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("…");
        breadcrumb.Items.ShouldAllBe(item => item.Bounds == default);
    }

    /// <summary>Verifies deliberate no-current overflow retains the longest prefix before a trailing trigger.</summary>
    [Fact]
    public async Task Render_WhenNoCurrentPathOverflows_DrawsLongestPrefixThenTriggerAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        breadcrumb.CurrentIndex = -1;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("Root›…  ");
        breadcrumb.Layout.TriggerPrecedesPrimary.ShouldBeFalse();
        breadcrumb.Layout.OverflowItems.Select(item => item.Text).ShouldBe(["Docs", "Leaf"]);
    }

    /// <summary>Verifies hidden entries reserve their slot and adjacent gaps without painting or receiving input.</summary>
    [Fact]
    public async Task Visibility_WhenItemIsHidden_PreservesSlotWithoutRenderingOrProjectionAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        breadcrumb.Items[0].Visibility = Visibility.Hidden;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("     Leaf");
        breadcrumb.Items[0].Bounds.Width.ShouldBe(4);
        breadcrumb.Layout.OverflowItems.ShouldBeEmpty();
        breadcrumb.HitTest(default).ShouldBeSameAs(breadcrumb);
    }

    /// <summary>Verifies collapsed entries release their slot and both adjacent reserved gaps.</summary>
    [Fact]
    public async Task Visibility_WhenItemIsCollapsed_ReleasesItsSlotAndAdjacentGapsAsync()
    {
        var breadcrumb = Create("Root", "Middle", "Leaf");
        breadcrumb.Items[1].Visibility = Visibility.Collapsed;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("Root›Leaf");
        breadcrumb.Items[1].Bounds.ShouldBe(default);
    }

    /// <summary>Verifies wide Unicode captions keep their continuation cells intact.</summary>
    [Fact]
    public async Task Render_WhenCaptionContainsWideAndCombiningText_PreservesWholeClustersAsync()
    {
        var breadcrumb = Create("界", "e\u0301");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("界›é");
        surface.Cell(new Point(1, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies an ambiguous preferred separator repairs to its portable fallback.</summary>
    [Fact]
    public async Task Render_WhenSeparatorIsWideUnderPolicy_UsesFallbackAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        breadcrumb.Style = BreadcrumbStyle.Default with
        {
            SeparatorGlyph = new ControlGlyph(new Rune('─'), new Rune('-'))
        };
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TerminalOptions.Minimal with
            {
                Capabilities = TerminalCapabilities.Conservative with { AmbiguousWidth = Ambiguous.Wide }
            },
            TestContext.Current.CancellationToken);

        surface.ShouldRender("Root-Leaf");
    }

    /// <summary>Verifies disabled overflow omissions never acquire menu projections.</summary>
    [Fact]
    public async Task Overflow_WhenOmittedItemIsDisabled_DoesNotProjectItAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        breadcrumb.Items[0].IsEnabled = false;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        breadcrumb.Layout.OverflowItems.Select(item => item.Text).ShouldBe(["Docs"]);
    }

    /// <summary>Verifies the private menu keeps source order and invokes the original semantic item.</summary>
    [Fact]
    public async Task Overflow_WhenTriggerOpens_ProjectsAndActivatesOriginalItemAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        var invoked = 0;
        breadcrumb.Items[0].Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(8, 4),
            TestContext.Current.CancellationToken);
        var trigger = OwnedTree.Find<BreadcrumbOverflowButton>(breadcrumb).ShouldNotBeNull();

        await surface.Pointer.ClickAsync(trigger);

        var menu = OwnedTree.Find<Menu>(trigger).ShouldNotBeNull();
        menu.Items.Cast<MenuItem>().Select(item => item.Text).ShouldBe(["Root", "Docs"]);
        await surface.Pointer.ClickAsync((MenuItem) menu.Items[0]);

        breadcrumb.CurrentItem.ShouldBeSameAs(breadcrumb.Items[0]);
        invoked.ShouldBe(1);
    }

    /// <summary>Verifies the private breadcrumb overflow uses the shared Menu minimum.</summary>
    [Fact]
    public async Task Overflow_WhenShortProjectionOpens_UsesFifteenCellMenuWidthAsync()
    {
        // Arrange
        var breadcrumb = Create("Root", "Docs", "Leaf");
        breadcrumb.CurrentIndex = 0;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        var trigger = OwnedTree.Find<BreadcrumbOverflowButton>(breadcrumb).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(trigger);

        // Assert
        var menu = OwnedTree.Find<Menu>(trigger).ShouldNotBeNull();
        menu.MinWidth.ShouldBe(Length.Cells(15));
        menu.Bounds.Width.ShouldBe(15);
    }

    /// <summary>Verifies an overflow projection refreshes when equal-width source text changes.</summary>
    [Fact]
    public async Task Overflow_WhenSourceTextChangesAtEqualWidth_RefreshesProjectionAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => breadcrumb.Items[0].Text = "Home", "rename overflowed source");
        var trigger = OwnedTree.Find<BreadcrumbOverflowButton>(breadcrumb).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(trigger);

        var menu = OwnedTree.Find<Menu>(trigger).ShouldNotBeNull();
        ((MenuItem) menu.Items[0]).Text.ShouldBe("Home");
    }

    /// <summary>Verifies pointer activation focuses only the owner and commits through the canonical route.</summary>
    [Fact]
    public async Task Pointer_WhenItemIsClicked_FocusesOwnerAndInvokesItemAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        var invoked = 0;
        breadcrumb.Items[0].Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(breadcrumb.Items[0]);

        surface.ShouldHaveFocus(breadcrumb);
        breadcrumb.Items[0].IsFocused.ShouldBeFalse();
        breadcrumb.CurrentItem.ShouldBeSameAs(breadcrumb.Items[0]);
        invoked.ShouldBe(1);
    }

    /// <summary>Verifies roving keys do not alter semantic current until activation.</summary>
    [Fact]
    public async Task Keyboard_WhenHomeThenEnter_RovesIndependentlyBeforeActivationAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.Home);

        breadcrumb.CurrentItem.ShouldBeSameAs(breadcrumb.Items[1]);
        (breadcrumb.Items[0].GetAppearanceState() & VisualState.Current).ShouldBe(VisualState.Current);

        await surface.Keyboard.PressAsync(Code.Enter);

        breadcrumb.CurrentItem.ShouldBeSameAs(breadcrumb.Items[0]);
    }

    /// <summary>Verifies a visible mnemonic focuses the owner and invokes its original item.</summary>
    [Fact]
    public async Task AccessKey_WhenPrimaryItemMatches_UsesCanonicalActivationAsync()
    {
        var breadcrumb = Create("&Root", "&Leaf");
        var invoked = 0;
        breadcrumb.Items[0].Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TestContext.Current.CancellationToken);

        await surface.SendAsync("\x1b[114;3:1u"u8.ToArray(), "Alt+R");

        surface.ShouldHaveFocus(breadcrumb);
        breadcrumb.CurrentItem.ShouldBeSameAs(breadcrumb.Items[0]);
        invoked.ShouldBe(1);
    }

    /// <summary>Verifies an overflowed mnemonic becomes available only through the open menu.</summary>
    [Fact]
    public async Task AccessKey_WhenItemIsOverflowed_RequiresOpenMenuAsync()
    {
        var breadcrumb = Create("&Root", "&Docs", "&Leaf");
        var invoked = 0;
        breadcrumb.Items[0].Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        await surface.SendAsync("\x1b[114;3:1u"u8.ToArray(), "Alt+R while overflow is closed");
        invoked.ShouldBe(0);

        var trigger = OwnedTree.Find<BreadcrumbOverflowButton>(breadcrumb).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(trigger);
        await surface.SendAsync("\x1b[114;3:1u"u8.ToArray(), "Alt+R while overflow is open");

        invoked.ShouldBe(1);
        breadcrumb.CurrentItem.ShouldBeSameAs(breadcrumb.Items[0]);
    }

    /// <summary>Verifies removing a held semantic item immediately retires owner capture.</summary>
    [Fact]
    public async Task Pointer_WhenPressedItemIsRemoved_CancelsCaptureBeforeReleaseAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(breadcrumb.Items[0]);
        await surface.Pointer.PressAsync();
        var removed = breadcrumb.Items[0];

        await surface.UpdateAsync(() => breadcrumb.Items.RemoveAt(0), "remove pressed breadcrumb item");

        surface.ShouldHaveCapture(null);
        removed.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies focus transfer clears the owner-managed held-item state and capture.</summary>
    [Fact]
    public async Task Pointer_WhenOwnerLosesFocus_CancelsItemPressAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        var other = new Button { Text = "Other" };
        var root = new Stack { Children = { breadcrumb, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(9, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(breadcrumb.Items[0]);
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(() => other.Focus(), "transfer focus during breadcrumb press");

        surface.ShouldHaveFocus(other);
        surface.ShouldHaveCapture(null);
        breadcrumb.Items[0].IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies terminal pointer leave retires the held target without moving semantic current.</summary>
    [Fact]
    public async Task Pointer_WhenTerminalLeaveArrives_CancelsItemPressAndCaptureAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(9, 1),
            TestContext.Current.CancellationToken);
        var current = breadcrumb.CurrentItem;
        await surface.Pointer.MoveToAsync(breadcrumb.Items[0]);
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(
            () => _ = surface.Application.Capture.Dispatch(new Pointer(
                cells: null,
                pixels: null,
                Buttons.None,
                PointerAction.Leave,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: true,
                isCellPositionInferred: false)),
            "route terminal pointer Leave to captured Breadcrumb");

        surface.ShouldHaveCapture(null);
        breadcrumb.Items[0].IsPressed.ShouldBeFalse();
        breadcrumb.CurrentItem.ShouldBeSameAs(current);
    }

    /// <summary>Verifies owner disposal during a press cleans up without touching disposed item state.</summary>
    [Fact]
    public async Task Dispose_WhenOwnerHasPressedItem_ReleasesCaptureAndDisposesPathAsync()
    {
        var breadcrumb = Create("Root", "Leaf");
        var root = new Stack { Children = { breadcrumb } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(9, 1),
            TestContext.Current.CancellationToken);
        var pressed = breadcrumb.Items[0];
        await surface.Pointer.MoveToAsync(pressed);
        await surface.Pointer.PressAsync();

        await surface.UpdateAsync(breadcrumb.Dispose, "dispose pressed breadcrumb");

        breadcrumb.IsDisposed.ShouldBeTrue();
        pressed.IsDisposed.ShouldBeTrue();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a resize invalidates a held target before its old cell can activate.</summary>
    [Fact]
    public async Task Resize_WhenItemIsPressed_CancelsStaleLayoutActivationAsync()
    {
        var breadcrumb = Create("Root", "Docs", "Leaf");
        var invoked = 0;
        breadcrumb.Items[2].Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            breadcrumb,
            new Size(14, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(breadcrumb.Items[2]);
        await surface.Pointer.PressAsync();

        await surface.ResizeAsync(new Size(8, 1));
        await surface.Pointer.ReleaseAsync();

        invoked.ShouldBe(0);
        breadcrumb.Items[2].IsPressed.ShouldBeFalse();
    }

    private static Breadcrumb Create(params string[] captions)
    {
        var breadcrumb = new Breadcrumb();

        foreach (var caption in captions)
        {
            breadcrumb.Items.Add(new BreadcrumbItem { Text = caption });
        }

        return breadcrumb;
    }
}
