// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

using System.Runtime.CompilerServices;

using SharpVision.Terminal.Input;


using KeyAction = Terminal.Input.Action;
using TerminalText = Terminal.Input.Text;

/// <summary>Verifies typed preview/bubble dispatch over stable route snapshots.</summary>
public sealed class RoutingTests
{
    /// <summary>Verifies preview, bubble, source state, payload, and default ordering.</summary>
    [Fact]
    public void Route_WhenTreeIsNested_InvokesPreviewThenBubbleAndDefault()
    {
        List<string> order = [];
        RecordingControl root = new("root", order);
        RecordingControl middle = new("middle", order);
        RecordingControl target = new("target", order);
        root.Children.Add(middle);
        middle.Children.Add(target);
        Stroke stroke = CreateStroke();

        AddRecorder(root, target, stroke, order);
        AddRecorder(middle, target, stroke, order);
        AddRecorder(target, target, stroke, order);

        KeyEventArgs eventArgs = new(stroke);
        Router.Route(target, Events.Key, eventArgs);

        order.ShouldBe([
            "root-Preview",
            "middle-Preview",
            "target-Preview",
            "target-Bubble",
            "middle-Bubble",
            "root-Bubble",
            "target-default",
            "middle-default",
            "root-default",
        ]);
        eventArgs.OriginalSource.ShouldBeSameAs(target);
        eventArgs.Source.ShouldBeSameAs(target);
    }

    /// <summary>Verifies Shift+Tab uses the shared default to move focus backward.</summary>
    [Fact]
    public async Task Route_WhenShiftTabIsPressed_MovesFocusToPreviousTabStopAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeContainer root = new();
            ProbeControl first = new() { CanFocus = true };
            ProbeControl second = new() { CanFocus = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(second).ShouldBeTrue();
            KeyEventArgs eventArgs = new(new Stroke(
                Code.Tab,
                character: null,
                nativeCode: 0,
                Modifiers.Shift,
                KeyAction.Press));

            Router.Route(second, Events.Key, eventArgs);

            focus.Focused.ShouldBeSameAs(first);
            eventArgs.Handled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies handled state suppresses ordinary handlers and default behavior.</summary>
    [Fact]
    public void Route_WhenHandled_InvokesOnlyOptedInHandlersAfterward()
    {
        List<string> order = [];
        RecordingControl root = new("root", order);
        RecordingControl target = new("target", order);
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
            eventArgs.Handled = true;
        });
        _ = target.AddHandler(Events.Key, (_, eventArgs) =>
            order.Add($"target-skipped-{eventArgs.Phase}"));
        _ = target.AddHandler(
            Events.Key,
            (_, eventArgs) => order.Add($"target-always-{eventArgs.Phase}"),
            handledEventsToo: true);

        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        order.ShouldBe([
            "root-ordinary-Preview",
            "root-always-Preview",
            "target-handle-Preview",
            "target-always-Preview",
            "target-always-Bubble",
            "root-always-Bubble",
        ]);
    }

    /// <summary>Verifies duplicate registration fails and disposal is idempotent.</summary>
    [Fact]
    public void AddHandler_WhenDuplicateOrDisposed_RejectsDuplicateAndStopsLaterRoutes()
    {
        List<string> order = [];
        RecordingControl target = new("target", order);
        int calls = 0;
        IDisposable registration = target.AddHandler(Events.Key, Handle);

        _ = Should.Throw<ArgumentException>(() => target.AddHandler(Events.Key, Handle));
        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        registration.Dispose();
        registration.Dispose();
        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));

        calls.ShouldBe(2);

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
        RecordingControl root = new("old", order);
        RecordingControl replacement = new("new", order);
        RecordingControl target = new("target", order);
        root.Children.Add(target);
        bool moved = false;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            order.Add($"old-{eventArgs.Phase}");

            if (!moved && eventArgs.Phase == Phase.Preview)
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

        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe([
            "old-Preview",
            "target-Preview",
            "target-Bubble",
            "old-Bubble",
            "target-default",
            "old-default",
        ]);
        order.Clear();

        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe([
            "new-Preview",
            "target-Preview",
            "target-Bubble",
            "new-Bubble",
            "target-default",
            "new-default",
        ]);
    }

    /// <summary>Verifies handlers added mid-route begin on the next route.</summary>
    [Fact]
    public void Route_WhenHandlerIsAddedDuringDispatch_UsesHandlerSnapshot()
    {
        List<string> order = [];
        RecordingControl target = new("target", order);
        bool added = false;
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

        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe(["first-Preview", "first-Bubble", "target-default"]);
        order.Clear();

        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
        order.ShouldBe([
            "first-Preview",
            "later-Preview",
            "first-Bubble",
            "later-Bubble",
            "target-default",
        ]);
    }

    /// <summary>Verifies bubble-only and direct identifiers use their configured ancestry.</summary>
    [Fact]
    public void Route_WhenStrategyDiffers_UsesConfiguredAncestry()
    {
        List<string> order = [];
        RecordingControl root = new("root", order);
        RecordingControl target = new("target", order);
        root.Children.Add(target);
        Event<KeyEventArgs> bubble = new("BubbleOnly", Strategy.Bubble);
        Event<KeyEventArgs> direct = new("Direct", Strategy.Direct);
        _ = root.AddHandler(bubble, (_, eventArgs) =>
            order.Add($"root-{eventArgs.Phase}"));
        _ = target.AddHandler(bubble, (_, eventArgs) =>
            order.Add($"target-{eventArgs.Phase}"));
        _ = root.AddHandler(direct, (_, eventArgs) =>
            order.Add($"root-direct-{eventArgs.Phase}"));
        _ = target.AddHandler(direct, (_, eventArgs) =>
            order.Add($"target-direct-{eventArgs.Phase}"));

        Router.Route(target, bubble, new KeyEventArgs(CreateStroke()));
        Router.Route(target, direct, new KeyEventArgs(CreateStroke()));

        order.ShouldBe([
            "target-Bubble",
            "root-Bubble",
            "target-default",
            "root-default",
            "target-direct-Bubble",
            "target-default",
        ]);
    }

    /// <summary>Verifies every standard argument retains its strongly typed terminal payload.</summary>
    [Fact]
    public void Constructor_WhenPayloadIsValid_PreservesTypedTerminalValue()
    {
        TerminalText text = new(new Rune('λ'));
        Pointer pointer = new(
            new Point(2, 3),
            pixels: new Point(16, 48),
            Buttons.Primary,
            PointerAction.Move,
            wheelX: 0,
            wheelY: 0,
            Modifiers.Shift,
            isMotion: true,
            isCellPositionInferred: false);
        Paste paste = new("hello"u8);
        Focus focus = new(gained: true);

        new TextEventArgs(text).Text.ShouldBe(text);
        new PointerEventArgs(pointer).Pointer.ShouldBe(pointer);
        new PasteEventArgs(paste).Paste.Utf8.ShouldBe(paste.Utf8);
        new FocusEventArgs(focus).Focus.ShouldBe(focus);
    }

    /// <summary>Verifies explicit interaction-event construction validates reference and enum arguments.</summary>
    [Fact]
    public void Constructor_WhenCancellationPayloadIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentNullException>(() =>
            new CaptureCancelledEventArgs(null!, ReleaseReason.Detached));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CaptureCancelledEventArgs(new ProbeControl(), (ReleaseReason) int.MaxValue));
    }

    /// <summary>Verifies source retargeting is controlled while original source is immutable.</summary>
    [Fact]
    public void Retarget_WhenCalledDuringRoute_ChangesOnlySource()
    {
        List<string> order = [];
        RecordingControl root = new("root", order);
        RecordingControl target = new("target", order);
        root.Children.Add(target);
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == Phase.Preview)
            {
                eventArgs.Retarget(root);
            }
        });
        KeyEventArgs eventArgs = new(CreateStroke());

        Router.Route(target, Events.Key, eventArgs);

        eventArgs.OriginalSource.ShouldBeSameAs(target);
        eventArgs.Source.ShouldBeSameAs(root);
        _ = Should.Throw<InvalidOperationException>(() => eventArgs.Retarget(target));
    }

    /// <summary>Verifies route cleanup after an exception permits reuse and later dispatch.</summary>
    [Fact]
    public void Route_WhenHandlerThrows_CleansStateBeforeRethrow()
    {
        List<string> order = [];
        RecordingControl target = new("target", order);
        InvalidOperationException failure = new("handler");
        bool shouldThrow = true;
        _ = target.AddHandler(Events.Key, (_, _) =>
        {
            if (shouldThrow)
            {
                shouldThrow = false;
                throw failure;
            }
        });
        KeyEventArgs eventArgs = new(CreateStroke());

        Should.Throw<InvalidOperationException>(() =>
            Router.Route(target, Events.Key, eventArgs)).ShouldBeSameAs(failure);
        Router.Route(target, Events.Key, eventArgs);

        order.ShouldBe(["target-default"]);
    }

    /// <summary>Verifies event and route arguments fail before invocation.</summary>
    [Fact]
    public void Route_WhenArgumentsAreInvalid_ThrowsBeforeHandlers()
    {
        List<string> order = [];
        RecordingControl target = new("target", order);

        _ = Should.Throw<ArgumentException>(() =>
            new Event<KeyEventArgs>(" ", Strategy.TunnelBubble));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Event<KeyEventArgs>("Invalid", (Strategy) int.MaxValue));
        _ = Should.Throw<ArgumentNullException>(() =>
            Router.Route(target, Events.Key, null!));
        _ = Should.Throw<ArgumentNullException>(() =>
            target.AddHandler<KeyEventArgs>(null!, (_, _) => { }));
        _ = Should.Throw<ArgumentNullException>(() =>
            target.AddHandler(Events.Key, null!));

        order.ShouldBeEmpty();
    }

    /// <summary>Verifies attached routing is dispatcher-affine.</summary>
    [Fact]
    public async Task Route_WhenAttachedOffThread_ThrowsBeforeHandlersAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();
        List<string> order = [];
        RecordingControl target = new("target", order);
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
        (WeakReference? control, WeakReference? listener) = CreateCollectibleRoute();

        for (int attempt = 0; attempt < 5 && (control.IsAlive || listener.IsAlive); attempt++)
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
        RecordingControl root = new("root", order);
        RecordingControl target = new("target", order);
        Listener listener = new();
        root.Children.Add(target);
        IDisposable registration = target.AddHandler(Events.Key, listener.Handle);
        Router.Route(target, Events.Key, new KeyEventArgs(CreateStroke()));
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
