// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves command-bar layout, rendering, focus, input, and popup behavior on a mounted surface.</summary>
public sealed class CommandBarSurfaceTests
{
    /// <summary>Verifies the ordinary row renders each semantic command once with an independently normalized separator.</summary>
    [Fact]
    public async Task Render_WhenEveryCommandFits_DrawsOnePrimaryRowWithoutTriggerAsync()
    {
        var bar = CreateBar(out var open, out var save, out var print);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(28, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender(" Open  │  Save   Print      ");
        open.IsOverflowed.ShouldBeFalse();
        save.IsOverflowed.ShouldBeFalse();
        print.IsOverflowed.ShouldBeFalse();
        OwnedTree.Find<CommandBarOverflowButton>(bar).ShouldNotBeNull().Bounds.Width.ShouldBe(0);
        OwnedTree.Find<Menu>(bar).ShouldNotBeNull().Items.ShouldBeEmpty();
    }

    /// <summary>Verifies shrinking keeps the longest fitting source prefix and projects the tail without reparenting it.</summary>
    [Fact]
    public async Task Resize_WhenTailOverflows_RendersTriggerAndPrivateMenuCommandsOnceAsync()
    {
        var bar = CreateBar(out var open, out var save, out var print);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(28, 6),
            TestContext.Current.CancellationToken);
        var semanticParent = save.Parent;

        await surface.ResizeAsync(new Size(12, 6));

        open.IsOverflowed.ShouldBeFalse();
        save.IsOverflowed.ShouldBeTrue();
        print.IsOverflowed.ShouldBeTrue();
        save.Parent.ShouldBeSameAs(semanticParent);
        print.Parent.ShouldBeSameAs(semanticParent);
        var trigger = OwnedTree.Find<CommandBarOverflowButton>(bar).ShouldNotBeNull();
        trigger.Bounds.ShouldBe(new Rect(7, 0, 1, 1));
        surface.Cell(new Point(trigger.Bounds.X, 0)).Text.ShouldBe("…");
        var menu = OwnedTree.Find<Menu>(bar).ShouldNotBeNull();
        menu.Items.Count.ShouldBe(2);
        menu.Items.OfType<MenuItem>().Select(static item => item.Text).ShouldBe(["&Save", "&Print"]);

        await surface.Pointer.ClickAsync(trigger);

        bar.IsOverflowOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(menu);
        ReadRow(surface, menu.Bounds.Y, 12).ShouldContain("Save");
        ReadRow(surface, menu.Bounds.Y + 1, 12).ShouldContain("Print");
    }

    /// <summary>Verifies disabled and hidden rows follow their distinct sizing, projection, and selection contracts.</summary>
    [Fact]
    public async Task Layout_WhenAvailabilityVaries_PreservesHiddenSlotAndProjectsDisabledTailAsync()
    {
        var first = new CommandBarItem { Text = "A" };
        var hidden = new CommandBarItem { Text = "Hidden", Visibility = Visibility.Hidden };
        var disabled = new CommandBarItem { Text = "Disabled", IsEnabled = false };
        var collapsed = new CommandBarItem { Text = "Collapsed", Visibility = Visibility.Collapsed };
        var bar = new CommandBar();
        bar.Items.Add(first);
        bar.Items.Add(hidden);
        bar.Items.Add(new CommandBarSeparator());
        bar.Items.Add(disabled);
        bar.Items.Add(collapsed);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(14, 5),
            TestContext.Current.CancellationToken);

        first.IsOverflowed.ShouldBeFalse();
        hidden.IsOverflowed.ShouldBeFalse();
        disabled.IsOverflowed.ShouldBeTrue();
        collapsed.IsOverflowed.ShouldBeFalse();
        hidden.Bounds.Width.ShouldBeGreaterThan(0);
        disabled.Bounds.Width.ShouldBe(0);
        collapsed.Bounds.Width.ShouldBe(0);
        var menuItem = OwnedTree.FindAll<MenuItem>(bar).Single();
        menuItem.Text.ShouldBe("Disabled");
        menuItem.IsEnabled.ShouldBeFalse();
        _ = Should.Throw<InvalidOperationException>(() => bar.SelectedItem = disabled);
        _ = Should.Throw<InvalidOperationException>(() => bar.SelectedItem = hidden);
    }

    /// <summary>Verifies arrow, Home, Enter, and Space route through one focused bar and canonical semantic actions.</summary>
    [Fact]
    public async Task Keyboard_WhenBarIsFocused_RovesAndActivatesWithoutFocusingFacesAsync()
    {
        var bar = CreateBar(out var open, out var save, out _);
        var invoked = new List<CommandBarItem>();
        bar.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Item);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(28, 2),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Enter);

        surface.ShouldHaveFocus(bar);
        bar.SelectedItem.ShouldBeSameAs(save);
        save.IsFocused.ShouldBeFalse();
        invoked.ShouldBe([save]);

        await surface.UpdateAsync(
            () => bar.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        open.IsPressed.ShouldBeTrue();
        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        open.IsPressed.ShouldBeFalse();
        invoked.ShouldBe([save, open]);
    }

    /// <summary>Verifies an overflowed mnemonic opens the menu and selects its matching private face instead of invoking invisibly.</summary>
    [Fact]
    public async Task AccessKey_WhenItemIsOverflowed_OpensAndSelectsProjectionAsync()
    {
        var bar = CreateBar(out _, out _, out var print);
        var invoked = 0;
        print.Invoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        var menu = OwnedTree.Find<Menu>(bar).ShouldNotBeNull();

        await surface.SendAsync(
            "\x1b[112;3:1u"u8.ToArray(),
            "press Alt+P access key");

        bar.IsOverflowOpen.ShouldBeTrue();
        bar.SelectedItem.ShouldBeSameAs(print);
        menu.SelectedItem.ShouldNotBeNull().Text.ShouldBe("&Print");
        invoked.ShouldBe(0);
        surface.ShouldHaveFocus(menu);
    }

    /// <summary>Verifies Escape dismisses the coordinator-owned modal plane and returns focus to the single bar stop.</summary>
    [Fact]
    public async Task Overflow_WhenEscapeDismisses_RestoresFocusToCommandBarAsync()
    {
        var bar = CreateBar(out _, out _, out _);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        var trigger = OwnedTree.Find<CommandBarOverflowButton>(bar).ShouldNotBeNull();

        await surface.Pointer.ClickAsync(trigger);
        bar.IsOverflowOpen.ShouldBeTrue();

        await surface.Keyboard.PressAsync(Code.Escape);

        bar.IsOverflowOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(bar);
    }

    /// <summary>Verifies Tab exits the bar because semantic items and the trigger are never independent stops.</summary>
    [Fact]
    public async Task Tab_WhenCommandBarFocused_LeavesTheSingleTabStopAsync()
    {
        var bar = CreateBar(out _, out _, out _);
        var after = new Button("After");
        var root = new Stack { Children = { bar, after } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(28, 4),
            TestContext.Current.CancellationToken);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(bar);

        await surface.Keyboard.PressAsync(Code.Tab);

        surface.ShouldHaveFocus(after);
    }

    /// <summary>Verifies primary pointer press selects through the bar while the semantic face owns capture and activation.</summary>
    [Fact]
    public async Task Pointer_WhenPrimaryItemIsClicked_SelectsAndInvokesThroughOneBarFocusTargetAsync()
    {
        var bar = CreateBar(out _, out var save, out _);
        var invoked = 0;
        save.Invoked += (_, eventArgs) =>
        {
            eventArgs.Cause.ShouldBe(ActivationCause.Pointer);
            invoked++;
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(28, 2),
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(save);
        await surface.Pointer.PressAsync();

        bar.SelectedItem.ShouldBeSameAs(save);
        save.IsPressed.ShouldBeTrue();
        surface.ShouldHaveFocus(bar);
        surface.ShouldHaveCapture(save);

        await surface.Pointer.ReleaseAsync();

        invoked.ShouldBe(1);
        save.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies overflow separator normalization removes edge and adjacent separators independently of the primary plane.</summary>
    [Fact]
    public async Task Overflow_WhenSeparatorsAreRedundant_ProjectsOnlyOneInteriorSeparatorAsync()
    {
        var bar = new CommandBar();
        bar.Items.Add(new CommandBarSeparator());
        bar.Items.Add(new CommandBarItem { Text = "Alpha" });
        bar.Items.Add(new CommandBarSeparator());
        bar.Items.Add(new CommandBarSeparator());
        bar.Items.Add(new CommandBarItem { Text = "Beta" });
        bar.Items.Add(new CommandBarSeparator());
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(1, 6),
            TestContext.Current.CancellationToken);
        var menu = OwnedTree.Find<Menu>(bar).ShouldNotBeNull();

        menu.Items.Count.ShouldBe(3);
        menu.Items[0].ShouldBeOfType<MenuItem>().Text.ShouldBe("Alpha");
        _ = menu.Items[1].ShouldBeOfType<MenuSeparator>();
        menu.Items[2].ShouldBeOfType<MenuItem>().Text.ShouldBe("Beta");
        OwnedTree.Find<CommandBarOverflowButton>(bar).ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 0, 1, 1));
    }

    /// <summary>Verifies live caption, affix, enabled, and local style state reaches an existing private projection.</summary>
    [Fact]
    public async Task Projection_WhenSourcePresentationChanges_SynchronizesWithoutCopyingCommandAsync()
    {
        var bar = CreateBar(out _, out _, out var print);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        var menuItem = OwnedTree.FindAll<MenuItem>(bar).Last();
        var style = CommandBarItemStyle.Default with
        {
            Face = CommandBarItemStyle.Default.Face with { Foreground = SemanticColor.Warning },
            AffixGap = 2
        };

        await surface.UpdateAsync(
            () =>
            {
                print.Text = "Publish";
                print.StartAffix = new Affix(">");
                print.EndAffix = new Affix("<");
                print.Style = style;
                print.IsEnabled = false;
            },
            "change overflowed semantic presentation");

        menuItem.Text.ShouldBe("Publish");
        menuItem.StartAffix.ShouldBe(new Affix(">"));
        menuItem.EndAffix.ShouldBe(new Affix("<"));
        menuItem.IsEnabled.ShouldBeFalse();
        menuItem.Style.ShouldNotBeNull().AffixGap.ShouldBe(2);
        menuItem.Command.ShouldBeNull();
        menuItem.CommandParameter.ShouldBeNull();
    }

    /// <summary>Verifies invoking a private face raises the semantic sequence, executes once, closes, and restores focus.</summary>
    [Fact]
    public async Task Overflow_WhenProjectionInvokes_UsesCanonicalSourceAndClosesSessionAsync()
    {
        var bar = CreateBar(out _, out _, out var print);
        var command = new ProbeCommand();
        var order = new List<string>();
        print.Command = command;
        print.Invoked += (_, eventArgs) =>
        {
            eventArgs.Cause.ShouldBe(ActivationCause.Pointer);
            order.Add("item");
        };
        bar.ItemInvoked += (_, eventArgs) =>
        {
            eventArgs.Item.ShouldBeSameAs(print);
            order.Add("bar");
        };
        command.Executing = _ => order.Add("command");
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        var trigger = OwnedTree.Find<CommandBarOverflowButton>(bar).ShouldNotBeNull();
        var projection = OwnedTree.FindAll<MenuItem>(bar).Last();
        await surface.Pointer.ClickAsync(trigger);

        await surface.Pointer.ClickAsync(projection);

        order.ShouldBe(["item", "bar", "command"]);
        command.Executions.ShouldBe([null]);
        bar.IsOverflowOpen.ShouldBeFalse();
        bar.SelectedItem.ShouldBeSameAs(print);
        surface.ShouldHaveFocus(bar);
    }

    /// <summary>Verifies zero- and one-cell layouts remain bounded and recover wide Unicode content without splitting clusters.</summary>
    [Fact]
    public async Task Resize_WhenBoundsAreTinyAndContentIsUnicode_StaysBoundedAndRecoversAsync()
    {
        var unicode = new CommandBarItem
        {
            Text = "e\u0301界",
            StartAffix = new Affix("👩🏽‍💻"),
            EndAffix = new Affix("✓")
        };
        var bar = new CommandBar();
        bar.Items.Add(unicode);
        new LayoutEngine().Layout(bar, new Size(0, 1));

        unicode.Bounds.Width.ShouldBe(0);
        unicode.IsOverflowed.ShouldBeTrue();

        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(1, 2),
            TestContext.Current.CancellationToken);
        OwnedTree.Find<CommandBarOverflowButton>(bar).ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 0, 1, 1));

        await surface.ResizeAsync(new Size(20, 2));

        unicode.IsOverflowed.ShouldBeFalse();
        unicode.Bounds.Right.ShouldBeLessThanOrEqualTo(20);
        ReadRow(surface, 0, 20).ShouldContain("é界");
    }

    /// <summary>Verifies an outside pointer target dismisses overflow through the shared coordinator.</summary>
    [Fact]
    public async Task Overflow_WhenOutsideTargetIsClicked_DismissesBeforeRoutingTargetAsync()
    {
        var bar = CreateBar(out _, out _, out _);
        var outside = new Button("Outside");
        var root = new Stack { Spacing = 1, Children = { bar, outside } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        var trigger = OwnedTree.Find<CommandBarOverflowButton>(bar).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(trigger);
        bar.IsOverflowOpen.ShouldBeTrue();

        await surface.Pointer.ClickAsync(outside);

        bar.IsOverflowOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(bar);
    }

    private static CommandBar CreateBar(
        out CommandBarItem open,
        out CommandBarItem save,
        out CommandBarItem print)
    {
        open = new CommandBarItem { Text = "&Open" };
        save = new CommandBarItem { Text = "&Save" };
        print = new CommandBarItem { Text = "&Print" };
        var bar = new CommandBar();
        bar.Items.Add(open);
        bar.Items.Add(new CommandBarSeparator());
        bar.Items.Add(save);
        bar.Items.Add(print);
        return bar;
    }

    private static string ReadRow(ComponentSurface surface, int y, int width) =>
        string.Concat(Enumerable.Range(0, width).Select(x => surface.Cell(new Point(x, y)).Text));
}
