// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies modal-aware discovery and semantic dispatch of MenuItem shortcuts.</summary>
public sealed class ShortcutManagerTests
{
    /// <summary>Verifies a matching chord invokes the target item with a keyboard activation cause.</summary>
    [Fact]
    public async Task Process_WhenGestureMatches_InvokesTargetItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var menu = new Menu();
            var item = new MenuItem { Text = "Save", Shortcut = CtrlS };
            menu.Items.Add(item);
            root.Children.Add(menu);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            ActivationCause? cause = null;
            item.Invoked += (_, eventArgs) => cause = eventArgs.Cause;

            var handled = manager.Process(Ctrl('s'));

            handled.ShouldBeTrue();
            cause.ShouldBe(ActivationCause.Keyboard);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a gesture reaches an item inside a closed submenu without opening it.</summary>
    [Fact]
    public async Task Process_WhenItemIsInsideAClosedSubmenu_InvokesItWithoutOpeningTheSubmenuAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var menu = new Menu();
            var submenu = new Menu();
            var inner = new MenuItem { Text = "Save", Shortcut = CtrlS };
            submenu.Items.Add(inner);
            var outer = new MenuItem { Text = "File", Submenu = submenu };
            menu.Items.Add(outer);
            root.Children.Add(menu);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            var invocations = 0;
            inner.Invoked += (_, _) => invocations++;

            var handled = manager.Process(Ctrl('s'));

            handled.ShouldBeTrue();
            invocations.ShouldBe(1);
            OwnedTree.Find<Popup>(outer)!.IsOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a disabled item's shortcut declines and never invokes.</summary>
    [Fact]
    public async Task Process_WhenItemIsDisabled_DeclinesAndNeverInvokesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var menu = new Menu();
            var item = new MenuItem
            {
                Text = "Save",
                Shortcut = CtrlS,
                IsEnabled = false
            };
            menu.Items.Add(item);
            root.Children.Add(menu);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            var invocations = 0;
            item.Invoked += (_, _) => invocations++;

            var handled = manager.Process(Ctrl('s'));

            handled.ShouldBeFalse();
            invocations.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a key repeat never invokes, matching the access-key Press-only policy.</summary>
    [Fact]
    public async Task Process_WhenKeyActionIsRepeat_DeclinesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var menu = new Menu();
            var item = new MenuItem { Text = "Save", Shortcut = CtrlS };
            menu.Items.Add(item);
            root.Children.Add(menu);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            var invocations = 0;
            item.Invoked += (_, _) => invocations++;

            var handled = manager.Process(new Stroke(
                Code.Character,
                new Rune('s'),
                nativeCode: 0,
                Modifiers.Control,
                KeyAction.Repeat));

            handled.ShouldBeFalse();
            invocations.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies that with no relevant focus, a duplicate gesture deterministically invokes
    /// the same first collected match on every press - the common case, since a Menu suppresses its
    /// items' own focusability (Menu owns the single tab stop, matching ListView/NavigationView).</summary>
    [Fact]
    public async Task Process_WhenNothingIsFocused_RepeatedlyInvokesTheFirstMatchAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var menu = new Menu();
            var first = new MenuItem { Text = "First", Shortcut = CtrlS };
            var second = new MenuItem { Text = "Second", Shortcut = CtrlS };
            menu.Items.Add(first);
            menu.Items.Add(second);
            root.Children.Add(menu);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            var firstInvocations = 0;
            var secondInvocations = 0;
            first.Invoked += (_, _) => firstInvocations++;
            second.Invoked += (_, _) => secondInvocations++;

            manager.Process(Ctrl('s')).ShouldBeTrue();
            manager.Process(Ctrl('s')).ShouldBeTrue();

            firstInvocations.ShouldBe(2);
            secondInvocations.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a duplicate gesture cycles past a match that contains current focus,
    /// exactly like AccessKeyManager.FindAnchor - reachable when a match's own content hosts a
    /// separately focusable control.</summary>
    [Fact]
    public async Task Process_WhenFocusIsWithinAMatch_CyclesToTheNextMatchAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var menu = new Menu();
            var anchorContent = new Button();
            var first = new MenuItem { Text = anchorContent, Shortcut = CtrlS };
            var second = new MenuItem { Text = "Second", Shortcut = CtrlS };
            menu.Items.Add(first);
            menu.Items.Add(second);
            root.Children.Add(menu);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            var firstInvocations = 0;
            var secondInvocations = 0;
            first.Invoked += (_, _) => firstInvocations++;
            second.Invoked += (_, _) => secondInvocations++;
            focus.Focus(anchorContent).ShouldBeTrue();

            manager.Process(Ctrl('s')).ShouldBeTrue();

            firstInvocations.ShouldBe(0);
            secondInvocations.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an active modal plane excludes matching background item shortcuts.</summary>
    [Fact]
    public async Task Process_WhenModalPlaneIsActive_UsesOnlyPlaneMatchesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var backgroundMenu = new Menu();
            var background = new MenuItem { Text = "Background", Shortcut = CtrlS };
            backgroundMenu.Items.Add(background);
            var plane = new Stack();
            var planeMenu = new Menu();
            var inside = new MenuItem { Text = "Inside", Shortcut = CtrlS };
            planeMenu.Items.Add(inside);
            plane.Children.Add(planeMenu);
            root.Children.Add(backgroundMenu);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, initialFocus: planeMenu);
            var manager = new ShortcutManager(root, focus, modality);
            var backgroundInvocations = 0;
            var insideInvocations = 0;
            background.Invoked += (_, _) => backgroundInvocations++;
            inside.Invoked += (_, _) => insideInvocations++;

            manager.Process(Ctrl('s')).ShouldBeTrue();

            backgroundInvocations.ShouldBe(0);
            insideInvocations.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    private static KeyGesture CtrlS => new(Code.Character, Modifiers.Control, new Rune('s'));

    private static Stroke Ctrl(char value) =>
        new(Code.Character, new Rune(value), nativeCode: 0, Modifiers.Control, KeyAction.Press);
}
