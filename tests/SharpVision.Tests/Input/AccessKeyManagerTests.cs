// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies modal-aware discovery and semantic dispatch of application access keys.</summary>
public sealed class AccessKeyManagerTests
{
    /// <summary>Verifies an Alt character focuses and keyboard-activates a captioned pressable.</summary>
    [Fact]
    public async Task Process_WhenButtonCaptionMatches_FocusesAndActivatesWithKeyboardCauseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var button = new Button { Content = new ControlText("&Save") };
            root.Children.Add(button);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new AccessKeyManager(root, focus, modality);
            ActivationCause? cause = null;
            button.Click += (_, eventArgs) => cause = eventArgs.Cause;

            var handled = manager.Process(Alt('s'));

            handled.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(button);
            cause.ShouldBe(ActivationCause.Keyboard);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies repeated duplicate access keys cycle from the currently focused match.</summary>
    [Fact]
    public async Task Process_WhenCaptionsShareKey_CyclesAfterFocusedMatchAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var first = new Button { Content = new ControlText("&Apply") };
            var second = new Button { Content = new ControlText("&Again") };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new AccessKeyManager(root, focus, modality);
            var firstClicks = 0;
            var secondClicks = 0;
            first.Click += (_, _) => firstClicks++;
            second.Click += (_, _) => secondClicks++;

            manager.Process(Alt('a')).ShouldBeTrue();
            manager.Process(Alt('A', Modifiers.Alt | Modifiers.Shift)).ShouldBeTrue();

            firstClicks.ShouldBe(1);
            secondClicks.ShouldBe(1);
            focus.Focused.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a group caption focuses its first eligible descendant by tab order.</summary>
    [Fact]
    public async Task Process_WhenGroupCaptionMatches_FocusesFirstDescendantInTabOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var later = new TextInput { TabIndex = 2 };
            var first = new TextInput { TabIndex = 1 };
            var group = new GroupBox
            {
                Header = "&Profile",
                Content = new Stack { Children = { later, first } }
            };
            root.Children.Add(group);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new AccessKeyManager(root, focus, modality);

            manager.Process(Alt('p')).ShouldBeTrue();

            focus.Focused.ShouldBeSameAs(first);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an active modal plane excludes matching background captions.</summary>
    [Fact]
    public async Task Process_WhenModalPlaneIsActive_UsesOnlyPlaneMatchesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var background = new Button { Content = new ControlText("&Run") };
            var plane = new Stack();
            var inside = new Button { Content = new ControlText("&Run") };
            plane.Children.Add(inside);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, initialFocus: inside);
            var manager = new AccessKeyManager(root, focus, modality);
            var backgroundClicks = 0;
            var insideClicks = 0;
            background.Click += (_, _) => backgroundClicks++;
            inside.Click += (_, _) => insideClicks++;

            manager.Process(Alt('r')).ShouldBeTrue();

            backgroundClicks.ShouldBe(0);
            insideClicks.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies non-Alt and ControlBase-modified Alt characters remain available to routed behavior.</summary>
    [Fact]
    public async Task Process_WhenModifiersAreNotAccessKeyShape_DeclinesStrokeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack { Children = { new Button { Content = new ControlText("&Save") } } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new AccessKeyManager(root, focus, modality);

            manager.Process(Alt('s', Modifiers.None)).ShouldBeFalse();
            manager.Process(Alt('s', Modifiers.Alt | Modifiers.Control)).ShouldBeFalse();
            focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies built-in captions reuse toggle, selection, navigation, and invocation state machines.</summary>
    [Fact]
    public async Task Process_WhenBuiltInCaptionsMatch_UsesEachSemanticKeyboardActionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var checkBox = new CheckBox { Content = new ControlText("&Check") };
            var radioButton = new RadioButton { Content = new ControlText("&Radio") };
            var expander = new Expander { Header = "&Expand", IsExpanded = true };
            var menu = new Menu();
            var menuItem = new MenuItem { Content = new ControlText("&Menu") };
            menu.Items.Add(menuItem);
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "&General" });
            tabs.Items.Add(new TabItem { Header = "&Advanced" });
            var navigation = new NavigationView();
            var navigationItem = new NavigationViewItem { Header = "&Home" };
            navigation.Items.Add(navigationItem);
            root.Children.Add(checkBox);
            root.Children.Add(radioButton);
            root.Children.Add(expander);
            root.Children.Add(menu);
            root.Children.Add(tabs);
            root.Children.Add(navigation);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new AccessKeyManager(root, focus, modality);
            var menuInvocations = 0;
            var navigationInvocations = 0;
            menuItem.Invoked += (_, eventArgs) =>
            {
                eventArgs.Cause.ShouldBe(ActivationCause.Keyboard);
                menuInvocations++;
            };
            navigationItem.Invoked += (_, eventArgs) =>
            {
                eventArgs.Cause.ShouldBe(ActivationCause.Keyboard);
                navigationInvocations++;
            };

            manager.Process(Alt('c')).ShouldBeTrue();
            manager.Process(Alt('r')).ShouldBeTrue();
            manager.Process(Alt('e')).ShouldBeTrue();
            manager.Process(Alt('m')).ShouldBeTrue();
            manager.Process(Alt('a')).ShouldBeTrue();
            manager.Process(Alt('h')).ShouldBeTrue();

            checkBox.IsChecked.ShouldBe(true);
            radioButton.IsChecked.ShouldBeTrue();
            expander.IsExpanded.ShouldBeFalse();
            menu.SelectedIndex.ShouldBe(0);
            menuInvocations.ShouldBe(1);
            tabs.SelectedIndex.ShouldBe(1);
            navigation.SelectedItem.ShouldBeSameAs(navigationItem);
            navigationInvocations.ShouldBe(1);
            focus.Focused.ShouldBeSameAs(navigation);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies live caption and availability changes are authoritative without registration updates.</summary>
    [Fact]
    public async Task Process_WhenCaptionAndAvailabilityMutate_UsesCurrentTreeStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var unavailable = new Button
            {
                Content = new ControlText("&Save"),
                IsEnabled = false
            };
            var caption = new ControlText("&Open");
            var current = new Button { Content = caption };
            root.Children.Add(unavailable);
            root.Children.Add(current);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new AccessKeyManager(root, focus, modality);
            var clicks = 0;
            current.Click += (_, _) => clicks++;

            manager.Process(Alt('s')).ShouldBeFalse();
            manager.Process(Alt('o')).ShouldBeTrue();
            caption.Content = "&Save";
            manager.Process(Alt('o')).ShouldBeFalse();
            manager.Process(Alt('s')).ShouldBeTrue();

            clicks.ShouldBe(2);
            focus.Focused.ShouldBeSameAs(current);
        }, TestContext.Current.CancellationToken);
    }

    private static Stroke Alt(char value, Modifiers modifiers = Modifiers.Alt) =>
        new(Code.Character, new Rune(value), nativeCode: 0, modifiers, KeyAction.Press);
}
