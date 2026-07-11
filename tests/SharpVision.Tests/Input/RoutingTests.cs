using System.Runtime.CompilerServices;
using System.Text;

using SharpVision.Input;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using KeyAction = SharpVision.Terminal.Input.Action;
using TerminalText = SharpVision.Terminal.Input.Text;

namespace SharpVision.Tests.Input;

/// <summary>Verifies typed preview/bubble dispatch over stable route snapshots.</summary>
public sealed class RoutingTests
{
    /// <summary>Verifies preview, bubble, source state, payload, and default ordering.</summary>
    [Fact]
    public void Route_WhenTreeIsNested_InvokesPreviewThenBubbleAndDefault()
    {
        var order = new List<string>();
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

    /// <summary>Verifies handled state suppresses ordinary handlers and default behavior.</summary>
    [Fact]
    public void Route_WhenHandled_InvokesOnlyOptedInHandlersAfterward()
    {
        var order = new List<string>();
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
        var order = new List<string>();
        var target = new RecordingControl("target", order);
        var calls = 0;
        var registration = target.AddHandler(Events.Key, Handle);

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
        var order = new List<string>();
        var root = new RecordingControl("old", order);
        var replacement = new RecordingControl("new", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        var moved = false;
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
        var order = new List<string>();
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
        var order = new List<string>();
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        var bubble = new Event<KeyEventArgs>("BubbleOnly", Strategy.Bubble);
        var direct = new Event<KeyEventArgs>("Direct", Strategy.Direct);
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
        var focus = new Focus(Gained: true);

        new TextEventArgs(text).Text.ShouldBe(text);
        new PointerEventArgs(pointer).Pointer.ShouldBe(pointer);
        new PasteEventArgs(paste).Paste.Utf8.ShouldBe(paste.Utf8);
        new FocusEventArgs(focus).Focus.ShouldBe(focus);
    }

    /// <summary>Verifies source retargeting is controlled while original source is immutable.</summary>
    [Fact]
    public void Retarget_WhenCalledDuringRoute_ChangesOnlySource()
    {
        var order = new List<string>();
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        root.Children.Add(target);
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == Phase.Preview)
            {
                eventArgs.Retarget(root);
            }
        });
        var eventArgs = new KeyEventArgs(CreateStroke());

        Router.Route(target, Events.Key, eventArgs);

        eventArgs.OriginalSource.ShouldBeSameAs(target);
        eventArgs.Source.ShouldBeSameAs(root);
        _ = Should.Throw<InvalidOperationException>(() => eventArgs.Retarget(target));
    }

    /// <summary>Verifies route cleanup after an exception permits reuse and later dispatch.</summary>
    [Fact]
    public void Route_WhenHandlerThrows_CleansStateBeforeRethrow()
    {
        var order = new List<string>();
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
        Router.Route(target, Events.Key, eventArgs);

        order.ShouldBe(["target-default"]);
    }

    /// <summary>Verifies event and route arguments fail before invocation.</summary>
    [Fact]
    public void Route_WhenArgumentsAreInvalid_ThrowsBeforeHandlers()
    {
        var order = new List<string>();
        var target = new RecordingControl("target", order);

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
        await using var dispatcher = Dispatcher.Start();
        var order = new List<string>();
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
        var order = new List<string>();
        var root = new RecordingControl("root", order);
        var target = new RecordingControl("target", order);
        var listener = new Listener();
        root.Children.Add(target);
        var registration = target.AddHandler(Events.Key, listener.Handle);
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
