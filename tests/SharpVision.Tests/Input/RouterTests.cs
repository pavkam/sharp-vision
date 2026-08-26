// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;


/// <summary>Verifies typed preview/bubble dispatch over stable route snapshots.</summary>
public sealed class RouterTests
{
    /// <summary>Verifies preview, bubble, source state, payload, and default ordering.</summary>
    [Fact]
    public void Route_WhenTreeIsNested_InvokesPreviewThenBubbleAndDefault()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var middle = new RecordingControl("middle", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(middle);
        middle.Children.Add(target);
        var stroke = CreateStroke();

        AddRecorder(root, target, stroke, order);
        AddRecorder(middle, target, stroke, order);
        AddRecorder(target, target, stroke, order);

        var eventArgs = new KeyEventArgs(stroke);
        _ = Router.Route(target, Events.Key, eventArgs);

        order.ShouldBe([
            "root-Preview",
            "middle-Preview",
            "target-Preview",
            "target-Bubble",
            "target-default",
            "middle-Bubble",
            "middle-default",
            "root-Bubble",
            "root-default"
        ]);
        eventArgs.OriginalSource.ShouldBeSameAs(target);
        eventArgs.Source.ShouldBeSameAs(target);
    }

    /// <summary>Verifies routing follows ownership through private non-interactive slots on non-Container owners.</summary>
    [Fact]
    public void Route_WhenAncestryUsesNonContainerSlots_PreviewsAndBubblesCompletePath()
    {
        var order = new List<string>();
        var root = new TraversalOwner();
        var middle = new TraversalOwner();
        var target = new ProbeControl();
        root.AddExcluded(middle);
        middle.AddPopup(target);
        Record(root, "root");
        Record(middle, "middle");
        Record(target, "target");

        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        order.ShouldBe([
            "root-Preview",
            "middle-Preview",
            "target-Preview",
            "target-Bubble",
            "middle-Bubble",
            "root-Bubble"
        ]);
        return;

        void Record(ControlBase control, string name) =>
            _ = control.AddHandler(Events.Key, (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
    }

    /// <summary>Verifies Shift+Tab uses the shared default to move focus backward.</summary>
    [Fact]
    public async Task Route_WhenShiftTabIsPressed_MovesFocusToPreviousTabStopAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(second).ShouldBeTrue();
            var eventArgs = new KeyEventArgs(new Stroke(
                Code.Tab,
                character: null,
                nativeCode: 0,
                Modifiers.Shift,
                KeyAction.Press));

            var result = Router.Route(second, Events.Key, eventArgs);
            focus.MoveNext(result.Anchor, reverse: true).ShouldBeTrue();

            focus.Focused.ShouldBeSameAs(first);
            eventArgs.IsHandled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies handled state suppresses ordinary handlers and default behavior while opted-in
    /// handlers still run at every remaining node, in preview and in bubble.
    /// </summary>
    [Fact]
    public void Route_WhenHandledDuringPreview_InvokesOptedInHandlersInEveryLaterPhase()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
            order.Add($"root-ordinary-{eventArgs.Phase}"));
        _ = root.AddHandler(
            Events.Key,
            (_, eventArgs) => order.Add($"root-always-{eventArgs.Phase}"),
            handledEventsToo: true);
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
        {
            order.Add($"target-handle-{eventArgs.Phase}");
            eventArgs.IsHandled = true;
        });
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
            order.Add($"target-skipped-{eventArgs.Phase}"));
        _ = target.AddHandler(
            Events.Key,
            (_, eventArgs) => order.Add($"target-always-{eventArgs.Phase}"),
            handledEventsToo: true);

        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        order.ShouldBe([
            "root-ordinary-Preview",
            "root-always-Preview",
            "target-handle-Preview",
            "target-always-Preview",
            "target-always-Bubble",
            "root-always-Bubble"
        ]);
    }

    /// <summary>
    /// Verifies a target that handles during bubble still lets opted-in ancestor handlers run.
    /// IsHandled ends ordinary handling, not ancestry traversal.
    /// </summary>
    [Fact]
    public void Route_WhenTargetHandlesDuringBubble_StillInvokesOptedInAncestorHandlers()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var middle = new RecordingControl("middle", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(middle);
        middle.Children.Add(target);
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble)
            {
                order.Add("target-handle-Bubble");
                eventArgs.IsHandled = true;
            }
        });
        _ = middle.AddHandler(
            Events.Key,
            (_, eventArgs) => order.Add($"middle-always-{eventArgs.Phase}"),
            handledEventsToo: true);
        _ = middle.AddHandler(Events.Key, (_, eventArgs) =>
            order.Add($"middle-ordinary-{eventArgs.Phase}"));
        _ = root.AddHandler(
            Events.Key,
            (_, eventArgs) => order.Add($"root-always-{eventArgs.Phase}"),
            handledEventsToo: true);

        var result = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        result.IsHandled.ShouldBeTrue();
        order.ShouldBe([
            "root-always-Preview",
            "middle-always-Preview",
            "middle-ordinary-Preview",
            "target-handle-Bubble",
            "middle-always-Bubble",
            "root-always-Bubble"
        ]);
    }

    /// <summary>
    /// Verifies handled state still suppresses ancestor default behavior even though ancestry is
    /// now always walked, so an ancestor default cannot steal an already-handled event.
    /// </summary>
    [Fact]
    public void Route_WhenHandledDuringBubble_SkipsAncestorDefaultBehavior()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble)
            {
                eventArgs.IsHandled = true;
            }
        });

        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        order.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies an unhandled route still runs every ancestor default in bubble order, so always
    /// walking the ancestry did not change unhandled behavior.
    /// </summary>
    [Fact]
    public void Route_WhenNeverHandled_InvokesEveryDefaultInBubbleOrder()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);

        var result = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        result.IsHandled.ShouldBeFalse();
        order.ShouldBe(["target-default", "root-default"]);
    }

    /// <summary>Verifies duplicate registration fails and disposal is idempotent.</summary>
    [Fact]
    public void AddHandler_WhenDuplicateOrDisposed_RejectsDuplicateAndStopsLaterRoutes()
    {
        List<string> order = [];
        var target = new RecordingControl("target", order);
        var calls = 0;
        var registration = target.AddHandler(Events.Key, Handle);

        _ = Should.Throw<ArgumentException>(() => target.AddHandler(Events.Key, Handle));
        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        registration.Dispose();
        registration.Dispose();
        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        calls.ShouldBe(2);
        return;

        void Handle(object? sender, KeyEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            calls++;
        }
    }

    /// <summary>Verifies ancestry mutation changes only routes created afterward.</summary>
    [Fact]
    public void Route_WhenPreviewReparentsTarget_KeepsCurrentSnapshot()
    {
        List<string> order = [];
        var root = new RecordingControl("old", order);
        var replacement = new RecordingControl("new", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        var moved = false;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            order.Add($"old-{eventArgs.Phase}");

            if (!moved && eventArgs.Phase == RoutingPhase.Preview)
            {
                moved = true;
                _ = root.Children.Remove(target);
                replacement.Children.Add(target);
            }
        });
        _ = replacement.AddHandler(Events.Key, (_, eventArgs) =>
            order.Add($"new-{eventArgs.Phase}"));
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
            order.Add($"target-{eventArgs.Phase}"));

        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe([
            "old-Preview",
            "target-Preview",
            "target-Bubble",
            "old-Bubble",
            "old-default"
        ]);
        order.Clear();

        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe([
            "new-Preview",
            "target-Preview",
            "target-Bubble",
            "target-default",
            "new-Bubble",
            "new-default"
        ]);
    }

    /// <summary>Verifies stable-route observers still receive bubble delivery after preview
    /// invalidates the target, while the stale target's interactive default is suppressed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Route_WhenPreviewInvalidatesInteractiveTarget_SkipsItsDefault(bool dispose)
    {
        var root = new ProbeContainer();
        var target = new Button { Text = "Action" };
        root.Children.Add(target);
        var bubbleCalls = 0;
        var activations = 0;
        target.Click += (_, _) => activations++;
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble)
            {
                bubbleCalls++;
            }
        }, handledEventsToo: true);
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase != RoutingPhase.Preview)
            {
                return;
            }

            if (dispose)
            {
                target.Dispose();
            }
            else
            {
                _ = root.Children.Remove(target);
            }
        });
        var input = new KeyEventArgs(new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        Should.NotThrow(() =>
        {
            _ = Router.Route(target, Events.Key, input);
        });

        bubbleCalls.ShouldBe(dispose ? 0 : 1);
        activations.ShouldBe(0);
    }

    /// <summary>Verifies handlers added mid-route begin on the next route.</summary>
    [Fact]
    public void Route_WhenHandlerIsAddedDuringDispatch_UsesHandlerSnapshot()
    {
        List<string> order = [];
        var target = new RecordingControl("target", order);
        var added = false;
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
        {
            order.Add($"first-{eventArgs.Phase}");

            if (!added)
            {
                added = true;
                _ = target.AddHandler(Events.Key, (_, later) =>
                    order.Add($"later-{later.Phase}"));
            }
        });

        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe(["first-Preview", "first-Bubble", "target-default"]);
        order.Clear();

        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe([
            "first-Preview",
            "later-Preview",
            "first-Bubble",
            "later-Bubble",
            "target-default"
        ]);
    }

    /// <summary>Verifies bubble-only and direct identifiers use their configured ancestry.</summary>
    [Fact]
    public void Route_WhenStrategyDiffers_UsesConfiguredAncestry()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        var bubble = new Event<KeyEventArgs>("BubbleOnly", RoutingStrategy.Bubble);
        var direct = new Event<KeyEventArgs>("Direct", RoutingStrategy.Direct);
        _ = root.AddHandler(bubble, (_, eventArgs) =>
            order.Add($"root-{eventArgs.Phase}"));
        _ = target.AddHandler(bubble, (_, eventArgs) =>
            order.Add($"target-{eventArgs.Phase}"));
        _ = root.AddHandler(direct, (_, eventArgs) =>
            order.Add($"root-direct-{eventArgs.Phase}"));
        _ = target.AddHandler(direct, (_, eventArgs) =>
            order.Add($"target-direct-{eventArgs.Phase}"));

        _ = Router.Route(target, bubble, new KeyEventArgs(CreateStroke()));
        _ = Router.Route(target, direct, new KeyEventArgs(CreateStroke()));

        order.ShouldBe([
            "target-Bubble",
            "target-default",
            "root-Bubble",
            "root-default",
            "target-direct-Bubble",
            "target-default"
        ]);
    }

    /// <summary>Verifies every standard argument retains its strongly typed terminal payload.</summary>
    [Fact]
    public void Constructor_WhenPayloadIsValid_PreservesTypedTerminalValue()
    {
        var text = new TerminalText(new Rune('λ'));
        var pointer = new Pointer(
            new Point(2, 3),
            pixels: new Point(16, 48),
            Buttons.Primary,
            PointerAction.Move,
            wheelX: 0,
            wheelY: 0,
            Modifiers.Shift,
            isMotion: true,
            isCellPositionInferred: false);
        var paste = new Paste("hello"u8);
        var focus = new TerminalFocus(gained: true);

        new TextEventArgs(text).Text.ShouldBe(text);
        new PointerEventArgs(pointer).Pointer.ShouldBe(pointer);
        new PasteEventArgs(paste).Paste.Utf8.ShouldBe(paste.Utf8);
        new TerminalFocusEventArgs(focus).Focus.ShouldBe(focus);
    }

    /// <summary>Verifies explicit interaction-event construction validates reference and enum arguments.</summary>
    [Fact]
    public void Constructor_WhenCancellationPayloadIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new PointerCaptureLostEventArgs((PointerCaptureLossReason) int.MaxValue));
    }

    /// <summary>Verifies source retargeting is controlled while original source is immutable.</summary>
    [Fact]
    public void Retarget_WhenCalledDuringRoute_ChangesOnlySource()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview)
            {
                eventArgs.Retarget(root);
            }
        });
        var eventArgs = new KeyEventArgs(CreateStroke());

        _ = Router.Route(target, Events.Key, eventArgs);

        eventArgs.OriginalSource.ShouldBeSameAs(target);
        eventArgs.Source.ShouldBeSameAs(root);
        _ = Should.Throw<InvalidOperationException>(() => eventArgs.Retarget(target));
    }

    /// <summary>Verifies route cleanup after an exception permits reuse and later dispatch.</summary>
    [Fact]
    public void Route_WhenHandlerThrows_CleansStateBeforeRethrow()
    {
        List<string> order = [];
        var target = new RecordingControl("target", order);
        var failure = new InvalidOperationException("handler");
        var shouldThrow = true;
        _ = target.AddHandler(Events.Key, (_, _) =>
        {
            if (shouldThrow)
            {
                shouldThrow = false;
                throw failure;
            }
        });
        var eventArgs = new KeyEventArgs(CreateStroke());

        Should.Throw<InvalidOperationException>(() =>
            Router.Route(target, Events.Key, eventArgs)).ShouldBeSameAs(failure);
        _ = Router.Route(target, Events.Key, eventArgs);

        order.ShouldBe(["target-default"]);
    }

    /// <summary>Verifies event and route arguments fail before invocation.</summary>
    [Fact]
    public void Route_WhenArgumentsAreInvalid_ThrowsBeforeHandlers()
    {
        List<string> order = [];
        var target = new RecordingControl("target", order);

        _ = Should.Throw<ArgumentException>(() =>
            new Event<KeyEventArgs>(" ", RoutingStrategy.TunnelBubble));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Event<KeyEventArgs>("Invalid", (RoutingStrategy) int.MaxValue));
        _ = Should.Throw<ArgumentNullException>(() =>
            Router.Route(target, Events.Key, null!));
        _ = Should.Throw<ArgumentNullException>(() =>
            target.AddHandler<KeyEventArgs>(null!, (_, _) => { }));
        _ = Should.Throw<ArgumentNullException>(() =>
            target.AddHandler(Events.Key, null!));

        order.ShouldBeEmpty();
    }

    /// <summary>Verifies a disposed control rejects new handler registration.</summary>
    [Fact]
    public void AddHandler_WhenControlIsDisposed_ThrowsObjectDisposedException()
    {
        List<string> order = [];
        var target = new RecordingControl("target", order);
        target.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() =>
            target.AddHandler(Events.Key, (_, _) => { }));
    }

    /// <summary>Verifies attached routing is dispatcher-affine.</summary>
    [Fact]
    public async Task Route_WhenAttachedOffThread_ThrowsBeforeHandlersAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        List<string> order = [];
        var target = new RecordingControl("target", order);
        await dispatcher.InvokeAsync(
            () => target.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() =>
            Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke())));
        _ = Should.Throw<InvalidOperationException>(() =>
            target.AddHandler(Events.Key, (_, _) => { }));

        order.ShouldBeEmpty();
    }

    /// <summary>Verifies disposed registrations and route pools retain no user objects.</summary>
    [Fact]
    public void Route_WhenRegistrationIsDisposed_DoesNotRetainHandlerOrControl()
    {
        var (control, listener) = CreateCollectibleRoute();

        for (var attempt = 0; attempt < 5 && (control.IsAlive || listener.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        control.IsAlive.ShouldBeFalse();
        listener.IsAlive.ShouldBeFalse();
    }

    private static void AddRecorder(
        RecordingControl control,
        RecordingControl target,
        Stroke stroke,
        List<string> order) =>
        _ = control.AddHandler(Events.Key, (sender, eventArgs) =>
        {
            sender.ShouldBeSameAs(control);
            eventArgs.OriginalSource.ShouldBeSameAs(target);
            eventArgs.Source.ShouldBeSameAs(target);
            eventArgs.Stroke.ShouldBe(stroke);
            order.Add($"{control.Name}-{eventArgs.Phase}");
        });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Control, WeakReference Listener) CreateCollectibleRoute()
    {
        List<string> order = [];
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        var listener = new Listener();
        root.Children.Add(target);
        var registration = target.AddHandler(Events.Key, listener.Handle);
        _ = Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        registration.Dispose();
        _ = root.Children.Remove(target);
        return (new WeakReference(target), new WeakReference(listener));
    }

    private static Stroke CreateStroke() => new(
        Code.Enter,
        character: null,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press);
}
