// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies InputBase's independent, opt-in capabilities - press activation, a single
/// owned text caption, an optional command, segment editing, step-key translation, the shared
/// drop-down glyph, and an owned popup - compose without forcing every value editor to pay for
/// capabilities it never enables.</summary>
public sealed class InputBaseTests
{
    #region Base contract

    /// <summary>Verifies the unconditional base contract - focusable and Tab-stopping - applies
    /// even when no capability is enabled at all.</summary>
    [Fact]
    public void Construct_WhenNoCapabilityIsEnabled_IsFocusableAndTabStopping()
    {
        var probe = new NoCapabilityInputProbe();

        probe.IsFocusable.ShouldBeTrue();
        probe.IsTabStop.ShouldBeTrue();
    }

    /// <summary>Verifies a control that never calls EnablePopup allocates no popup state at all -
    /// no owned framework-part slot, no owned controls - proving capability isolation.</summary>
    [Fact]
    public void Construct_WhenPopupIsNotEnabled_AllocatesNoPopupSlotOrOwnedControls()
    {
        Type[] withoutPopup =
        [
            typeof(NoCapabilityInputProbe),
            typeof(PressOnlyInputProbe),
            typeof(SegmentOnlyInputProbe),
            typeof(StepOnlyInputProbe)
        ];

        foreach (var type in withoutPopup)
        {
            using var probe = (InputBase) Activator.CreateInstance(type, nonPublic: true)!;

            probe.OwnedControlCount.ShouldBe(0, type.FullName);
            probe.FindOwnedSlot("drop-down").ShouldBeNull(type.FullName);
        }
    }

    /// <summary>Verifies VerifyMutable exposes the same disposed guard the internal base method
    /// enforces, under the protected name a third-party derivative can actually call.</summary>
    [Fact]
    public void VerifyMutable_WhenDisposed_ThrowsObjectDisposedException()
    {
        var probe = new NoCapabilityInputProbe();
        probe.Dispose();

        _ = Should.Throw<ObjectDisposedException>(probe.ProbeVerifyMutable);
    }

    /// <summary>Verifies VerifyMutable is a silent no-op for a live, attached-or-not control.</summary>
    [Fact]
    public void VerifyMutable_WhenNotDisposed_DoesNotThrow()
    {
        var probe = new NoCapabilityInputProbe();

        Should.NotThrow(probe.ProbeVerifyMutable);
    }

    #endregion

    #region Press activation

    /// <summary>Verifies press activation composes on its own, independent of the caption
    /// capability's single-caption authoring role.</summary>
    [Fact]
    public void PressActivation_WhenEnabledAlone_ActivatesOnEnterWithoutOwningAnyChild()
    {
        var probe = new PressOnlyInputProbe();

        _ = Router.Route(probe, Events.Key, Enter());

        probe.Activations.ShouldBe([ActivationCause.Keyboard]);
        probe.OwnedControlCount.ShouldBe(0);
    }

    /// <summary>Verifies calling EnablePressActivation a second time throws, matching every other
    /// Enable* capability's idempotence guard.</summary>
    [Fact]
    public void EnablePressActivation_WhenCalledTwice_Throws()
    {
        var probe = new PressOnlyInputProbe();

        _ = Should.Throw<InvalidOperationException>(probe.EnablePressActivationAgain);
    }

    /// <summary>Verifies Space holds until matching release and Enter activates directly, through a
    /// control composing press activation, a caption, and a command together - the same combination
    /// the deleted PressableBase always imposed.</summary>
    [Fact]
    public void Dispatch_WhenKeyboardActivates_UsesExactTransitionAndCause()
    {
        var control = new ProbePressable();
        control.SetCapabilities(TestCapabilities.WithKeyReleases);

        Key(control, Code.Character, new Rune(' '), KeyAction.Press);
        control.IsPressed.ShouldBeTrue();
        control.Activations.ShouldBeEmpty();
        Key(control, Code.Character, new Rune(' '), KeyAction.Repeat);
        Key(control, Code.Character, new Rune(' '), KeyAction.Release);
        Key(control, Code.Enter, character: null, KeyAction.Press);

        control.IsPressed.ShouldBeFalse();
        control.Activations.ShouldBe([
            ActivationCause.Keyboard,
            ActivationCause.Keyboard
        ]);
        control.CanFocus.ShouldBeTrue();
    }

    /// <summary>Verifies primary pointer press focuses, captures, and activates once inside.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerReleasesInside_ActivatesOnceAndReleasesCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Press));
            control.IsPressed.ShouldBeTrue();
            capture.Captured.ShouldBeSameAs(control);
            focus.Focused.ShouldBeSameAs(control);
            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Release));

            control.Activations.ShouldBe([ActivationCause.Pointer]);
            control.IsPressed.ShouldBeFalse();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies moving and releasing outside cancels without activation.</summary>
    [Fact]
    public async Task Dispatch_WhenCapturedPointerLeaves_CancelsPressedActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(15, 7), PointerAction.Move));
            control.IsPressed.ShouldBeFalse();
            _ = capture.Dispatch(Pointer(new Point(15, 7), PointerAction.Release));

            control.Activations.ShouldBeEmpty();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disable, hide, and detach clear held state without activation.</summary>
    [Fact]
    public async Task Dispatch_WhenControlBecomesUnavailable_ClearsEveryHeldStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            var other = new ProbePressable { Bounds = new Rect(12, 2, 6, 3) };
            root.Children.Add(control);
            root.Children.Add(other);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);
            // Held-Space state only exists under a release-reporting terminal.
            control.SetCapabilities(TestCapabilities.WithKeyReleases);
            focus.Focus(control).ShouldBeTrue();
            Key(control, Code.Character, new Rune(' '), KeyAction.Press);

            focus.Focus(other).ShouldBeTrue();
            control.IsPressed.ShouldBeFalse();
            focus.Focus(control).ShouldBeTrue();
            Key(control, Code.Character, new Rune(' '), KeyAction.Press);

            control.IsEnabled = false;

            control.IsPressed.ShouldBeFalse();
            control.Activations.ShouldBeEmpty();
            focus.Focused.ShouldBeNull();
            control.IsEnabled = true;
            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Press));
            _ = root.Children.Remove(control);
            control.IsPressed.ShouldBeFalse();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies secondary pointer transitions never capture or activate.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerIsNotPrimary_IgnoresTransitionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(
                new Point(3, 3),
                PointerAction.Press,
                Buttons.Secondary));

            capture.Captured.ShouldBeNull();
            control.Activations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a content-originated route focuses and captures the semantic caption owner
    /// itself, not the owned caption child the pointer physically landed on.</summary>
    [Fact]
    public async Task Route_WhenContentIsOriginalPointerTarget_OwnerOwnsFocusCaptureAndActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new Button { Text = "Go" };
            var content = control.TextControl!;
            new LayoutEngine().Layout(control, new Size(8, 3));
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using PointerManager capture = new(control);
            var point = new Point(content.Bounds.X, content.Bounds.Y);

            _ = Router.Route(content, Events.Pointer, new PointerEventArgs(Pointer(point, PointerAction.Press)));

            focus.Focused.ShouldBeSameAs(control);
            capture.Captured.ShouldBeSameAs(control);
            control.IsPressed.ShouldBeTrue();

            _ = Router.Route(content, Events.Pointer, new PointerEventArgs(Pointer(point, PointerAction.Release)));

            capture.Captured.ShouldBeNull();
            control.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies terminal focus loss clears shared held state before the protected cancellation callback.</summary>
    [Fact]
    public async Task TerminalFocusLost_WhenPointerIsHeld_CancelsBeforeCallbackAndLaterReleaseDoesNotActivateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ProbePressable { Bounds = new Rect(0, 0, 8, 3) };
            control.Attach(dispatcher);
            using PointerManager capture = new(control);
            _ = capture.Dispatch(Pointer(new Point(1, 1), PointerAction.Press));

            capture.TerminalFocusLost();
            _ = capture.Dispatch(Pointer(new Point(1, 1), PointerAction.Release));

            control.CaptureCancellations.ShouldBe([PointerCaptureLossReason.TerminalFocusLost]);
            control.HadCaptureDuringCancellation.ShouldBeFalse();
            control.WasPressedDuringCancellation.ShouldBeFalse();
            control.Activations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Caption

    /// <summary>Verifies calling EnableCaption a second time throws, matching every other Enable*
    /// capability's idempotence guard.</summary>
    [Fact]
    public void EnableCaption_WhenCalledTwice_Throws()
    {
        var probe = new ProbePressable();

        _ = Should.Throw<InvalidOperationException>(probe.EnableCaptionAgain);
    }

    /// <summary>Verifies setting Text before EnableCaption runs throws, instead of silently
    /// materializing a caption child for a control that never opted into the capability.</summary>
    [Fact]
    public void Text_WhenCaptionIsNotEnabled_ThrowsOnSet()
    {
        var probe = new PressOnlyInputProbe();

        _ = Should.Throw<InvalidOperationException>(() => probe.Text = "value");
    }

    /// <summary>Verifies reading Text before EnableCaption runs never throws, staying null-safe for
    /// every InputBase derivative regardless of whether it opts into a caption.</summary>
    [Fact]
    public void Text_WhenCaptionIsNotEnabled_ReadsAsEmptyString()
    {
        var probe = new PressOnlyInputProbe();

        probe.Text.ShouldBe(string.Empty);
    }

    /// <summary>Verifies Text materializes the owned caption child on its first assignment and
    /// updates that same child's content directly afterward, requesting re-measurement both times
    /// since the caption's own content width can change.</summary>
    [Fact]
    public void Text_WhenSetAfterCaptionIsEnabled_MaterializesChildAndInvalidatesMeasure()
    {
        var probe = new ProbePressable();
        probe.Clear(Invalidation.All);

        probe.Text = "first";

        probe.Text.ShouldBe("first");
        probe.TextControl.ShouldNotBeNull().Content.ShouldBe("first");
        ((probe.Pending & Invalidation.Measure) != 0).ShouldBeTrue();
        var materializedChild = probe.TextControl;
        probe.Clear(Invalidation.All);

        probe.Text = "second";

        probe.Text.ShouldBe("second");
        probe.TextControl.ShouldBeSameAs(materializedChild);
        probe.TextControl.Content.ShouldBe("second");
        ((probe.Pending & Invalidation.Measure) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies assigning the same committed Text value again is a silent no-op - no
    /// notification and no invalidation - since the caption's content did not actually change.</summary>
    [Fact]
    public void Text_WhenSetToSameCommittedValue_IsNoOp()
    {
        var probe = new ProbePressable { Text = "same" };
        probe.Clear(Invalidation.All);
        List<string?> notifications = [];
        probe.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        probe.Text = "same";

        notifications.ShouldBeEmpty();
        probe.Pending.ShouldBe(Invalidation.None);
    }

    #endregion

    #region Command

    /// <summary>Verifies calling EnableCommand a second time throws, matching every other Enable*
    /// capability's idempotence guard.</summary>
    [Fact]
    public void EnableCommand_WhenCalledTwice_Throws()
    {
        var probe = new ProbePressable();

        _ = Should.Throw<InvalidOperationException>(probe.EnableCommandAgain);
    }

    /// <summary>Verifies reading or setting Command before EnableCommand runs throws, matching
    /// IsOpen's precedent for an un-opted-into capability.</summary>
    [Fact]
    public void Command_WhenCommandIsNotEnabled_ThrowsOnGetAndSet()
    {
        var probe = new PressOnlyInputProbe();

        _ = Should.Throw<InvalidOperationException>(() => probe.Command);
        _ = Should.Throw<InvalidOperationException>(() => probe.Command = new ProbeCommand());
    }

    /// <summary>Verifies reading or setting CommandParameter before EnableCommand runs throws.</summary>
    [Fact]
    public void CommandParameter_WhenCommandIsNotEnabled_ThrowsOnGetAndSet()
    {
        var probe = new PressOnlyInputProbe();

        _ = Should.Throw<InvalidOperationException>(() => probe.CommandParameter);
        _ = Should.Throw<InvalidOperationException>(() => probe.CommandParameter = new object());
    }

    /// <summary>Verifies Command round-trips and requests a repaint - a bound command's own
    /// resolved identity can change how a control renders (for example, a disabled visual for a
    /// command that cannot currently execute).</summary>
    [Fact]
    public void Command_WhenSet_RoundTripsAndInvalidatesRender()
    {
        var probe = new ProbePressable();
        probe.Clear(Invalidation.All);
        var command = new ProbeCommand();

        probe.Command = command;

        probe.Command.ShouldBeSameAs(command);
        ((probe.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies CommandParameter round-trips, raises PropertyChanged exactly once, and
    /// requests a repaint, matching Command's own invalidation impact since the two are always
    /// read together on activation.</summary>
    [Fact]
    public void CommandParameter_WhenSet_RoundTripsRaisesPropertyChangedAndInvalidatesRender()
    {
        var probe = new ProbePressable();
        probe.Clear(Invalidation.All);
        List<string?> notifications = [];
        probe.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        var parameter = new object();

        probe.CommandParameter = parameter;

        probe.CommandParameter.ShouldBeSameAs(parameter);
        notifications.ShouldBe([nameof(InputBase.CommandParameter)]);
        ((probe.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies a consumer-derived InputBase's activation is recorded before command execution.</summary>
    [Fact]
    public void Dispatch_WhenConsumerDerivedControlHasCommand_ActivatesBeforeExecute()
    {
        var parameter = new object();
        var activationCountDuringExecute = -1;
        ProbePressable control = null!;
        var command = new ProbeCommand { Executing = _ => activationCountDuringExecute = control.Activations.Count };
        control = new ProbePressable { Command = command, CommandParameter = parameter };

        Key(control, Code.Enter, character: null, KeyAction.Press);

        activationCountDuringExecute.ShouldBe(1);
        control.Activations.ShouldBe([ActivationCause.Keyboard]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies a command that cannot execute suppresses both activation and execution.</summary>
    [Fact]
    public void Dispatch_WhenCommandCanExecuteIsFalse_RaisesNoActivationAndDoesNotExecute()
    {
        var command = new ProbeCommand { CanExecuteValue = false };
        var control = new ProbePressable { Command = command };

        Key(control, Code.Enter, character: null, KeyAction.Press);

        control.Activations.ShouldBeEmpty();
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies replacing Command unsubscribes the previous instance's CanExecuteChanged
    /// and subscribes the new one, so only the currently assigned command can invalidate render.</summary>
    [Fact]
    public void Command_WhenReplaced_UnsubscribesPreviousAndSubscribesNew()
    {
        var previous = new ProbeCommand();
        var next = new ProbeCommand();
        var control = new ProbePressable { Command = previous };
        previous.HasCanExecuteChangedSubscribers.ShouldBeTrue();

        control.Command = next;

        previous.HasCanExecuteChangedSubscribers.ShouldBeFalse();
        next.HasCanExecuteChangedSubscribers.ShouldBeTrue();
    }

    /// <summary>Verifies a PropertyChanged replacement supersedes the interrupted command
    /// transition without duplicating the final command subscription.</summary>
    [Fact]
    public void Command_WhenPropertyChangedReplacesCandidate_SubscribesOnlyFinalCommand()
    {
        var candidate = new ProbeCommand();
        var winner = new ProbeCommand();
        var control = new ProbePressable();
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InputBase.Command) &&
                ReferenceEquals(control.Command, candidate))
            {
                control.Command = winner;
            }
        };

        control.Command = candidate;

        candidate.AddCount.ShouldBe(1);
        candidate.RemoveCount.ShouldBe(1);
        candidate.SubscriberCount.ShouldBe(0);
        winner.AddCount.ShouldBe(1);
        winner.SubscriberCount.ShouldBe(1);

        control.Dispose();

        winner.RemoveCount.ShouldBe(1);
        winner.SubscriberCount.ShouldBe(0);
    }

    /// <summary>Verifies multiple nested property replacements settle on one subscription to the
    /// final committed command.</summary>
    [Fact]
    public void Command_WhenPropertyChangedReplacesMultipleTimes_SubscribesOnlyLastWinner()
    {
        var first = new ProbeCommand();
        var second = new ProbeCommand();
        var third = new ProbeCommand();
        var control = new ProbePressable();
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(InputBase.Command))
            {
                return;
            }

            if (ReferenceEquals(control.Command, first))
            {
                control.Command = second;
            }
            else if (ReferenceEquals(control.Command, second))
            {
                control.Command = third;
            }
        };

        control.Command = first;

        first.SubscriberCount.ShouldBe(0);
        second.SubscriberCount.ShouldBe(0);
        third.SubscriberCount.ShouldBe(1);
        third.AddCount.ShouldBe(1);
    }

    /// <summary>Verifies add-accessor reentry cannot leave its superseded candidate subscribed.</summary>
    [Fact]
    public void Command_WhenCandidateAddReentersWithWinner_SubscribesOnlyWinner()
    {
        var candidate = new ProbeCommand();
        var winner = new ProbeCommand();
        var control = new ProbePressable();
        candidate.Adding = () =>
        {
            candidate.Adding = null;
            control.Command = winner;
        };

        control.Command = candidate;

        control.Command.ShouldBeSameAs(winner);
        candidate.SubscriberCount.ShouldBe(0);
        winner.SubscriberCount.ShouldBe(1);
    }

    /// <summary>Verifies remove-accessor reentry supersedes the interrupted replacement instead of
    /// allowing that stale outer setter to overwrite the winner.</summary>
    [Fact]
    public void Command_WhenCurrentRemoveReentersWithWinner_PreservesWinner()
    {
        var previous = new ProbeCommand();
        var interrupted = new ProbeCommand();
        var winner = new ProbeCommand();
        var control = new ProbePressable { Command = previous };
        previous.Removing = () =>
        {
            previous.Removing = null;
            control.Command = winner;
        };

        control.Command = interrupted;

        control.Command.ShouldBeSameAs(winner);
        previous.SubscriberCount.ShouldBe(0);
        interrupted.SubscriberCount.ShouldBe(0);
        winner.SubscriberCount.ShouldBe(1);
    }

    /// <summary>Verifies a failed add remains uncommitted and a same-value retry installs one
    /// subscription without publishing another property transition.</summary>
    [Fact]
    public void Command_WhenCandidateAddThrows_SameValueRetrySubscribesOnce()
    {
        var command = new ProbeCommand { ThrowOnNextAdd = true };
        var control = new ProbePressable();
        var propertyChanges = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InputBase.Command))
            {
                propertyChanges++;
            }
        };

        _ = Should.Throw<InvalidOperationException>(() => control.Command = command);
        command.SubscriberCount.ShouldBe(0);
        propertyChanges.ShouldBe(1);

        control.Command = command;

        command.SubscriberCount.ShouldBe(1);
        propertyChanges.ShouldBe(1);
    }

    /// <summary>Verifies a candidate handler registered before an add failure is compensated so a
    /// same-reference retry cannot duplicate it.</summary>
    [Fact]
    public void Command_WhenCandidateAddThrowsAfterRegistration_RetrySubscribesOnce()
    {
        var command = new ProbeCommand { ThrowAfterNextAdd = true };
        var control = new ProbePressable();

        _ = Should.Throw<InvalidOperationException>(() => control.Command = command);
        command.SubscriberCount.ShouldBe(0);

        control.Command = command;

        command.SubscriberCount.ShouldBe(1);
    }

    /// <summary>Verifies a failed old-source removal keeps its tracked subscription retryable while
    /// the new public command remains authoritative.</summary>
    [Fact]
    public void Command_WhenCurrentRemoveThrows_SameValueRetryReconcilesWinner()
    {
        var previous = new ProbeCommand();
        var winner = new ProbeCommand();
        var control = new ProbePressable { Command = previous };
        previous.ThrowOnNextRemove = true;

        _ = Should.Throw<InvalidOperationException>(() => control.Command = winner);
        control.Command.ShouldBeSameAs(winner);
        previous.SubscriberCount.ShouldBe(1);
        winner.SubscriberCount.ShouldBe(0);

        control.Command = winner;

        previous.SubscriberCount.ShouldBe(0);
        winner.SubscriberCount.ShouldBe(1);
    }

    /// <summary>Verifies disposal retries every tracked removal after replacement reported a
    /// cleanup failure.</summary>
    [Fact]
    public void Dispose_WhenCommandRemovalPreviouslyFailed_ReleasesRetiredSubscription()
    {
        var previous = new ProbeCommand();
        var winner = new ProbeCommand();
        var control = new ProbePressable { Command = previous };
        previous.ThrowOnNextRemove = true;
        _ = Should.Throw<InvalidOperationException>(() => control.Command = winner);

        control.Dispose();

        previous.SubscriberCount.ShouldBe(0);
        winner.SubscriberCount.ShouldBe(0);
    }

    /// <summary>Verifies assigning the identical healthy command reference neither publishes nor
    /// churns its established event subscription.</summary>
    [Fact]
    public void Command_WhenSameReferenceAssigned_DoesNotChurnSubscription()
    {
        var command = new ProbeCommand();
        var control = new ProbePressable { Command = command };
        var propertyChanges = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InputBase.Command))
            {
                propertyChanges++;
            }
        };

        control.Command = command;

        command.AddCount.ShouldBe(1);
        command.RemoveCount.ShouldBe(0);
        propertyChanges.ShouldBe(0);
    }

    /// <summary>Verifies mutable command objects use reference identity even when their custom
    /// equality implementation reports two distinct instances as equal.</summary>
    [Fact]
    public void Command_WhenDistinctInstancesCompareEqual_CommitsReplacement()
    {
        var previous = new ProbeCommand { EqualsOtherCommands = true };
        var replacement = new ProbeCommand { EqualsOtherCommands = true };
        var control = new ProbePressable { Command = previous };

        control.Command = replacement;

        control.Command.ShouldBeSameAs(replacement);
        previous.SubscriberCount.ShouldBe(0);
        replacement.SubscriberCount.ShouldBe(1);
    }

    /// <summary>Verifies replacing a command with null removes the one established subscription.</summary>
    [Fact]
    public void Command_WhenReplacedWithNull_UnsubscribesPrevious()
    {
        var command = new ProbeCommand();
        var control = new ProbePressable { Command = command };

        control.Command = null;

        control.Command.ShouldBeNull();
        command.SubscriberCount.ShouldBe(0);
        command.RemoveCount.ShouldBe(1);
    }

    /// <summary>Verifies dispatcher and disposal validation reject replacement before touching any
    /// command event accessor.</summary>
    [Fact]
    public async Task Command_WhenMutationIsInvalid_DoesNotTouchSubscriptionsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var current = new ProbeCommand();
        var rejected = new ProbeCommand();
        var control = new ProbePressable { Command = current };
        await dispatcher.InvokeAsync(() => control.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.Command = rejected);
        current.SubscriberCount.ShouldBe(1);
        rejected.AddCount.ShouldBe(0);

        await dispatcher.InvokeAsync(control.Dispose, TestContext.Current.CancellationToken);

        _ = Should.Throw<ObjectDisposedException>(() => control.Command = rejected);
        rejected.AddCount.ShouldBe(0);
    }

    /// <summary>Verifies disposing a control with an assigned Command unsubscribes CanExecuteChanged
    /// exactly once, leaving no dangling subscription.</summary>
    [Fact]
    public async Task Dispose_WhenCommandIsAssigned_UnsubscribesCanExecuteChangedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var command = new ProbeCommand();
            var control = new ProbePressable { Command = command };
            control.Attach(dispatcher);

            control.Dispose();

            command.HasCanExecuteChangedSubscribers.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies OnCanExecuteChanged does not let InvalidOperationException escape when a
    /// bound command raises CanExecuteChanged from an off-thread caller while the owning
    /// dispatcher's bounded queue is full, matching the same recipe
    /// <c>DispatcherTests.Post_WhenQueueIsFull_ThrowsBeforeEnqueueAsync</c> uses: an unguarded
    /// InvalidOperationException here would abort delivery to any other subscriber still later in
    /// the same CanExecuteChanged invocation list.</summary>
    [Fact]
    public async Task Command_WhenCanExecuteChangedFiresOffThreadWithSaturatedDispatcher_DoesNotThrowAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var command = new ProbeCommand();
        var control = new ProbePressable { Command = command };

        await dispatcher.InvokeAsync(() => control.Attach(dispatcher), TestContext.Current.CancellationToken);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            // The dispatcher's queue has exactly one free slot before this post; the fill below
            // saturates it, matching DispatcherTests' own recipe for a guaranteed queue-full post.
            dispatcher.Post(static () => { });

            // Raised directly from this test thread - an off-dispatcher caller, exactly like a
            // bound command legitimately raising CanExecuteChanged from a worker thread.
            Should.NotThrow(command.RaiseCanExecuteChanged);
        }
        finally
        {
            release.Set();
        }
    }

    /// <summary>Verifies a detached command source cannot dirty a control that has no dispatcher
    /// lifecycle capable of consuming the invalidation.</summary>
    [Fact]
    public async Task Command_WhenCanExecuteChangedFiresWhileDetached_DoesNotInvalidateAsync()
    {
        var command = new ProbeCommand();
        var control = new ProbePressable { Command = command };
        control.Clear(Invalidation.All);

        await Task.Run(command.RaiseCanExecuteChanged, TestContext.Current.CancellationToken);

        control.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies work queued for an old dispatcher attachment cannot cross a detach and
    /// reattach boundary to dirty the control under its new dispatcher.</summary>
    [Fact]
    public async Task Command_WhenQueuedCanExecuteChangedOutlivesAttachment_DoesNotInvalidateNewAttachmentAsync()
    {
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var command = new ProbeCommand();
        var control = new ProbePressable { Command = command };
        await previousDispatcher.InvokeAsync(
            () =>
            {
                control.Attach(previousDispatcher);
                control.Clear(Invalidation.All);
            },
            TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim raised = new();
        using ManualResetEventSlim release = new();
        previousDispatcher.Post(() =>
        {
            entered.SetResult();
            raised.Wait();
            control.Detach();
            detached.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        await Task.Run(
            () =>
            {
                command.RaiseCanExecuteChanged();
                raised.Set();
            },
            TestContext.Current.CancellationToken);
        await detached.Task.WaitAsync(TestContext.Current.CancellationToken);
        await currentDispatcher.InvokeAsync(
            () =>
            {
                control.Attach(currentDispatcher);
                control.Clear(Invalidation.All);
            },
            TestContext.Current.CancellationToken);

        release.Set();
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        await currentDispatcher.InvokeAsync(
            () => control.Pending.ShouldBe(Invalidation.None),
            TestContext.Current.CancellationToken);
    }

    #endregion

    #region Segment editing

    /// <summary>Verifies segment editing composes on its own: typed digits and increments commit
    /// through the shared engine without press activation or a popup ever getting involved.</summary>
    [Fact]
    public void SegmentEditing_WhenEnabledAlone_CommitsDigitsAndIncrements()
    {
        var probe = new SegmentOnlyInputProbe();

        _ = probe.TypeDigit(1);
        _ = probe.TypeDigit(2);

        probe.Value.ShouldBe(12);

        _ = probe.IncrementSegment(1);

        probe.Value.ShouldBe(13);

        _ = probe.IncrementSegment(-5);

        probe.Value.ShouldBe(8);
    }

    /// <summary>Verifies calling EnableSegmentEditing a second time throws.</summary>
    [Fact]
    public void EnableSegmentEditing_WhenCalledTwice_Throws()
    {
        var probe = new SegmentOnlyInputProbe();

        _ = Should.Throw<InvalidOperationException>(probe.EnableSegmentEditingAgain);
    }

    #endregion

    #region Stepping

    /// <summary>Verifies TryGetStepDelta translates Up to +1 and Down to -1 with no other
    /// capability enabled - the helper is a free-standing static seam.</summary>
    [Fact]
    public void TryGetStepDelta_WhenUpOrDownPressed_TranslatesToPositiveOrNegativeOne()
    {
        var probe = new StepOnlyInputProbe();

        _ = Router.Route(probe, Events.Key, Key(Code.Up));
        _ = Router.Route(probe, Events.Key, Key(Code.Down));
        _ = Router.Route(probe, Events.Key, Key(Code.Down));

        probe.Deltas.ShouldBe([1, -1, -1]);
    }

    /// <summary>Verifies a key other than Up/Down is left untranslated.</summary>
    [Fact]
    public void TryGetStepDelta_WhenKeyIsNotUpOrDown_ReturnsFalse()
    {
        var probe = new StepOnlyInputProbe();

        _ = Router.Route(probe, Events.Key, Key(Code.Left));
        _ = Router.Route(probe, Events.Key, Key(Code.Enter));

        probe.Deltas.ShouldBeEmpty();
    }

    #endregion

    #region Drop-down glyph

    /// <summary>Verifies the shared drop-down indicator reserves exactly one cell, matching every
    /// concrete popup-backed field's own layout constant before the hoist.</summary>
    [Fact]
    public void DropDownIndicatorWidth_IsOneCell() =>
        PopupListInputProbe.ProbeDropDownIndicatorWidth.ShouldBe(1);

    /// <summary>Verifies ResolveDropDownGlyph falls back to the supplied code-owned glyph when no
    /// theme is attached, matching every concrete popup-backed field's own resolution.</summary>
    [Fact]
    public void ResolveDropDownGlyph_WhenNoThemeIsAttached_ResolvesAgainstDefaultTheme()
    {
        var probe = new PopupListInputProbe();
        var fallback = new Rune('v');

        var resolved = probe.ProbeResolveDropDownGlyph(fallback);

        var expected = ThemeCatalog.Dark
            .GetStyleSet(InputStyle.Default)
            .Normal.DropDownGlyph.Resolve(fallback, probe.CellPolicy.AmbiguousWidth);
        resolved.ShouldBe(expected);
    }

    #endregion

    #region Popup

    /// <summary>Verifies the popup capability registers exactly one owned framework-part slot.</summary>
    [Fact]
    public void EnablePopup_WhenEnabled_RegistersOneOwnedFrameworkPartSlot()
    {
        var probe = new PopupListInputProbe();

        probe.OwnedControlCount.ShouldBe(1);
        var slot = probe.FindOwnedSlot("drop-down").ShouldNotBeNull();
        slot.Count.ShouldBe(1);
        OwnedTree.Find<Popup>(probe).ShouldBeSameAs(probe.Popup);
    }

    /// <summary>Verifies calling EnablePopup a second time throws.</summary>
    [Fact]
    public void EnablePopup_WhenCalledTwice_Throws()
    {
        var probe = new PopupListInputProbe();

        _ = Should.Throw<InvalidOperationException>(probe.EnablePopupAgain);
    }

    /// <summary>Verifies reading or setting IsOpen before EnablePopup runs throws, instead of
    /// silently no-op'ing against a coordinator that was never constructed.</summary>
    [Fact]
    public void Opened_WhenPopupIsNotEnabled_ThrowsOnGetAndSet()
    {
        var probe = new NoCapabilityInputProbe();

        _ = Should.Throw<InvalidOperationException>(() => probe.ProbeGetOpened());
        _ = Should.Throw<InvalidOperationException>(() => probe.ProbeSetOpened(true));
    }

    /// <summary>Verifies accepting without an open session is an inert authoring operation.</summary>
    [Fact]
    public void AcceptPopupAndClose_WhenNoSessionIsOpen_DoesNotInvokeAcceptanceOrThrow()
    {
        var probe = new PopupListInputProbe();

        Should.NotThrow(probe.ProbeAcceptPopupAndClose);

        probe.IsOpen.ShouldBeFalse();
        probe.AcceptSessionCount.ShouldBe(0);
    }

    /// <summary>Verifies the protected acceptance seam commits the active session before closing
    /// its popup.</summary>
    [Fact]
    public void AcceptPopupAndClose_WhenSessionIsOpen_AcceptsOnceAndCloses()
    {
        var probe = new PopupListInputProbe { IsOpen = true };

        probe.ProbeAcceptPopupAndClose();

        probe.IsOpen.ShouldBeFalse();
        probe.AcceptSessionCount.ShouldBe(1);
    }

    /// <summary>Verifies the owner lifecycle guard wins over the missing-capability guard after
    /// disposal.</summary>
    [Fact]
    public void AcceptPopupAndClose_WhenDisposed_ThrowsObjectDisposedException()
    {
        var probe = new NoCapabilityInputProbe();
        probe.Dispose();

        _ = Should.Throw<ObjectDisposedException>(probe.ProbeAcceptPopupAndClose);
    }

    /// <summary>Verifies accepting an attached popup session remains dispatcher-affine.</summary>
    [Fact]
    public async Task AcceptPopupAndClose_WhenAttachedAndCalledOffDispatcher_ThrowsInvalidOperationExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var root = new ProbeContainer();
        var probe = new PopupListInputProbe();
        await dispatcher.InvokeAsync(
            () =>
            {
                root.Children.Add(probe);
                root.Attach(dispatcher);
                probe.IsOpen = true;
            },
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(probe.ProbeAcceptPopupAndClose);

        await dispatcher.InvokeAsync(root.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies press activation toggles the owned popup and DropDownOpened/DropDownClosed
    /// fire through the virtual override seam, composing the two capabilities together the same
    /// way ComboBox does.</summary>
    [Fact]
    public void Activate_WhenPressAndPopupAreComposed_TogglesOpenedAndRaisesDropDownEvents()
    {
        var probe = new PopupListInputProbe();

        _ = Router.Route(probe, Events.Key, Enter());

        probe.IsOpen.ShouldBeTrue();
        probe.DropDownOpenedCount.ShouldBe(1);
        probe.DropDownClosedCount.ShouldBe(0);

        _ = Router.Route(probe, Events.Key, Enter());

        probe.IsOpen.ShouldBeFalse();
        probe.DropDownOpenedCount.ShouldBe(1);
        probe.DropDownClosedCount.ShouldBe(1);
    }

    /// <summary>Verifies popup, press activation, and segment editing coexist on one control without
    /// collision - mirroring DateInput's full capability combination.</summary>
    [Fact]
    public void Capabilities_WhenPopupPressAndSegmentEditingAreComposed_AllOperateIndependently()
    {
        var probe = new PopupCalendarInputProbe();

        _ = probe.IncrementSegment(1);
        probe.Value.ShouldBe(1);

        _ = Router.Route(probe, Events.Key, Enter());

        probe.IsOpen.ShouldBeTrue();
        probe.Activations.ShouldBe([ActivationCause.Keyboard]);

        _ = probe.IncrementSegment(2);
        probe.Value.ShouldBe(3);
    }

    /// <summary>Verifies closing an open popup returns focus to the owning control when focus was
    /// still inside the popup's content - the same focus-restoration contract
    /// PopupDropDownCoordinatorTests verifies directly against the coordinator.</summary>
    [Fact]
    public async Task Opened_WhenClosedWithFocusInsideContent_RestoresFocusToOwnerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var probe = new PopupListInputProbe();
            root.Children.Add(probe);
            root.Attach(dispatcher);

            using var focus = new FocusManager(root);
            focus.Focus(probe).ShouldBeTrue();

            probe.IsOpen = true;
            _ = focus.Focus(probe.Item);
            focus.Focused.ShouldBeSameAs(probe.Item);

            probe.IsOpen = false;

            focus.Focused.ShouldBeSameAs(probe);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies OnAttached re-enters the modal scope for an already-open popup, and
    /// disposal detaches the coordinator cleanly with no leaked subscriptions or exceptions -
    /// exercising the full attach/open/dispose lifecycle.</summary>
    [Fact]
    public async Task Lifecycle_WhenAttachedOpenedAndDisposed_CompletesWithoutExceptionsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var probe = new PopupListInputProbe();
            root.Children.Add(probe);

            probe.IsOpen = true;
            root.Attach(dispatcher);

            probe.IsOpen.ShouldBeTrue();

            Should.NotThrow(() => probe.Dispose());
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies measuring and arranging a popup-backed control at zero-sized bounds
    /// completes without throwing.</summary>
    [Fact]
    public void Layout_WhenBoundsAreTiny_CompletesWithoutThrowing()
    {
        var probe = new PopupCalendarInputProbe();

        Should.NotThrow(() => new LayoutEngine().Layout(probe, new Size(0, 0)));
        Should.NotThrow(() => new LayoutEngine().Layout(probe, new Size(1, 1)));
    }

    /// <summary>Verifies resolving a root structural glyph registers the render dependency used
    /// when a later Theme swap changes only that non-appearance member.</summary>
    [Fact]
    public void SetTheme_WhenResolvedDropDownGlyphChanges_InvalidatesRender()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create(inputExtra: ", \"dropDownGlyph\": \"v\""));
        var current = ThemeCatalog.Parse(ThemeJson.Create(inputExtra: ", \"dropDownGlyph\": \"x\""));
        var probe = new PopupListInputProbe();
        probe.SetTheme(previous);
        _ = probe.ProbeResolveDropDownGlyph(new Rune('?'));
        probe.Clear(Invalidation.All);

        probe.SetTheme(current);

        probe.Pending.ShouldBe(Invalidation.Render);
    }

    #endregion

    private static KeyEventArgs Key(Code code, Modifiers modifiers = Modifiers.None) => new(new Stroke(
        code,
        character: null,
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    private static KeyEventArgs Enter() => Key(Code.Enter);

    private static void Key(
        ProbePressable control,
        Code code,
        Rune? character,
        KeyAction action) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character,
                nativeCode: 0,
                Modifiers.None,
                action)));

    private static Pointer Pointer(
        Point cells,
        PointerAction action,
        Buttons buttons = Buttons.Primary) => new(
        cells,
        pixels: null,
        buttons,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);
}
