// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies routed input respects stable modal-plane boundaries.</summary>
public sealed partial class ModalityManagerTests
{
    /// <summary>Verifies preview, bubble, and defaults remain inside the matching primary plane root.</summary>
    [Fact]
    public async Task Route_WhenTargetIsInsideModalPlane_StopsAtMatchingRootAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            Record(appRoot, "app");
            Record(plane, "plane");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    Events.Key,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies blocked direct callers fail before arguments or handlers observe a route.</summary>
    [Fact]
    public async Task Route_WhenTargetIsOutsideActivePlane_ThrowsBeforeBeginningArgumentsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var background = new RecordingControl("background", order);
            var plane = new RecordingControl("plane", order);
            appRoot.Children.Add(background);
            appRoot.Children.Add(plane);
            _ = appRoot.AddHandler(Events.Key, (_, _) => order.Add("app-handler"));
            _ = background.AddHandler(Events.Key, (_, _) => order.Add("background-handler"));
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);
            var eventArgs = new KeyEventArgs(CreateStroke()) { Handled = true };

            _ = Should.Throw<InvalidOperationException>(() =>
                Router.Route(background, Events.Key, eventArgs));

            eventArgs.OriginalSource.ShouldBeNull();
            eventArgs.Source.ShouldBeNull();
            eventArgs.Handled.ShouldBeTrue();
            order.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a target in a secondary plane routes only through that included root.</summary>
    [Fact]
    public async Task Route_WhenTargetUsesIncludedRoot_StopsAtIncludedRootAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var primary = new RecordingControl("primary", order);
            var included = new RecordingControl("included", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(primary);
            appRoot.Children.Add(included);
            included.Children.Add(leaf);
            Record(appRoot, "app");
            Record(primary, "primary");
            Record(included, "included");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(primary);
            scope.Include(included);

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "included-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "included-Bubble",
                "included-default",
            ]);
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    Events.Key,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies entering a scope during preview changes only later routes.</summary>
    [Fact]
    public async Task Route_WhenHandlerEntersScope_KeepsCapturedAncestryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            ModalScope? scope = null;
            Record(appRoot, "app", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && scope is null)
                {
                    scope = modality.Enter(plane);
                }
            });
            Record(plane, "plane");
            Record(leaf, "leaf");

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
                "app-Bubble",
                "app-default",
            ]);
            order.Clear();

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            _ = scope.ShouldNotBeNull();
            scope.Dispose();
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies including a root during preview affects eligibility only after the current route.</summary>
    [Fact]
    public async Task Route_WhenHandlerIncludesRoot_KeepsCapturedBoundaryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var primary = new RecordingControl("primary", order);
            var primaryLeaf = new RecordingControl("primary-leaf", order);
            var included = new RecordingControl("included", order);
            var includedLeaf = new RecordingControl("included-leaf", order);
            appRoot.Children.Add(primary);
            appRoot.Children.Add(included);
            primary.Children.Add(primaryLeaf);
            included.Children.Add(includedLeaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(primary);
            var includedDuringRoute = false;
            Record(primary, "primary", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && !includedDuringRoute)
                {
                    includedDuringRoute = true;
                    scope.Include(included);
                }
            });
            Record(primaryLeaf, "primary-leaf");
            Record(included, "included");
            Record(includedLeaf, "included-leaf");

            _ = Router.Route(primaryLeaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "primary-Preview",
                "primary-leaf-Preview",
                "primary-leaf-Bubble",
                "primary-leaf-default",
                "primary-Bubble",
                "primary-default",
            ]);
            order.Clear();

            _ = Router.Route(includedLeaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "included-Preview",
                "included-leaf-Preview",
                "included-leaf-Bubble",
                "included-leaf-default",
                "included-Bubble",
                "included-default",
            ]);
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies exiting a scope during preview does not extend the current route.</summary>
    [Fact]
    public async Task Route_WhenHandlerExitsScope_KeepsCapturedBoundaryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            var scope = modality.Enter(plane);
            var exitedDuringRoute = false;
            Record(appRoot, "app");
            Record(plane, "plane", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && !exitedDuringRoute)
                {
                    exitedDuringRoute = true;
                    scope.Dispose();
                }
            });
            Record(leaf, "leaf");

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            order.Clear();

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
                "app-Bubble",
                "app-default",
            ]);
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies manager disposal during preview does not extend the captured route.</summary>
    [Fact]
    public async Task Route_WhenHandlerDisposesManager_KeepsCapturedBoundaryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);
            var disposedDuringRoute = false;
            Record(appRoot, "app");
            Record(plane, "plane", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && !disposedDuringRoute)
                {
                    disposedDuringRoute = true;
                    modality.Dispose();
                }
            });
            Record(leaf, "leaf");

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            order.Clear();

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
                "app-Bubble",
                "app-default",
            ]);
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct events remain target-only while enforcing modal eligibility.</summary>
    [Fact]
    public async Task Route_WhenStrategyIsDirect_UsesOnlyEligibleTargetAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var background = new RecordingControl("background", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(background);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            var direct = new Event<KeyEventArgs>("Direct", RoutingStrategy.Direct);
            Record(appRoot, "app");
            Record(background, "background");
            Record(plane, "plane");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);

            _ = Router.Route(leaf, direct, new KeyEventArgs(CreateStroke()));

            order.ShouldBe(["leaf-Bubble", "leaf-default"]);
            var blockedArgs = new KeyEventArgs(CreateStroke());
            _ = Should.Throw<InvalidOperationException>(() =>
                Router.Route(background, direct, blockedArgs));
            blockedArgs.OriginalSource.ShouldBeNull();
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    direct,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an attached manager without an active scope preserves the established route order.</summary>
    [Fact]
    public async Task Route_WhenNoScopeIsActive_PreservesExistingOrderingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var middle = new RecordingControl("middle", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(middle);
            middle.Children.Add(leaf);
            Record(appRoot, "app");
            Record(middle, "middle");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "middle-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "middle-Bubble",
                "middle-default",
                "app-Bubble",
                "app-default",
            ]);
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    Events.Key,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    private static Stroke CreateStroke() => new(
        Code.Enter,
        character: null,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press);
}
