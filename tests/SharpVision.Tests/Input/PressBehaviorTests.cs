// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>Verifies the Space key semantics of the composed press behavior under both keyboard
/// protocols.
///
/// <para>Space historically armed on Press and completed only on Release - correct under the
/// Kitty keyboard protocol, where releases are reported, and permanently broken everywhere else:
/// a press-only terminal delivered the arming press, latched the held flag forever, and no
/// activation or un-press ever followed. Every legacy terminal therefore had a dead Space on
/// every pressable control while Enter and pointer clicks worked.</para>
/// </summary>
public sealed class PressBehaviorTests
{
    private static KeyEventArgs Space(KeyAction action, Modifiers modifiers = Modifiers.None) => new(
        new Stroke(Code.Character, new Rune(' '), 0, modifiers, action));

    private static KeyEventArgs Enter(KeyAction action, Modifiers modifiers = Modifiers.None) => new(
        new Stroke(Code.Enter, character: null, 0, modifiers, action));

    private static PointerEventArgs Pointer(Buttons buttons, PointerAction action) => new(new Pointer(
        new Point(5, 0),
        pixels: null,
        buttons,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false));

    private static PressBehavior Create(
        bool releasesExpected,
        List<ActivationCause> activations,
        List<bool> pressedChanges)
    {
        return new PressBehavior(
            static () => new Rect(0, 0, 10, 1),
            static () => true,
            static () => true,
            static () => true,
            static () => true,
            static () => false,
            static () => { },
            pressedChanges.Add,
            activations.Add,
            () => releasesExpected);
    }

    /// <summary>The press-only regression: one Space press must pulse the pressed frame and
    /// activate immediately, because the completing release never arrives.</summary>
    [Fact]
    public void Handle_WhenReleasesAreNotExpected_ActivatesOnThePressItself()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: false, activations, pressed);

        var press = Space(KeyAction.Press);
        behavior.Handle(press);

        press.IsHandled.ShouldBeTrue();
        activations.ShouldBe([ActivationCause.Keyboard]);
        pressed.ShouldBe([true, false]);

        // A second press activates again - nothing latched.
        behavior.Handle(Space(KeyAction.Press));
        activations.Count.ShouldBe(2);
    }

    /// <summary>The release-reporting protocol keeps the hold semantics: press arms and shows the
    /// pressed frame, release completes.</summary>
    [Fact]
    public void Handle_WhenReleasesAreExpected_CompletesOnTheRelease()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        behavior.Handle(Space(KeyAction.Press));
        activations.ShouldBeEmpty();
        pressed.ShouldBe([true]);

        behavior.Handle(Space(KeyAction.Release));
        activations.ShouldBe([ActivationCause.Keyboard]);
        pressed.ShouldBe([true, false]);
    }

    /// <summary>An incidental Control modifier must not commit a held-Space activation, and the
    /// press itself must stay unhandled so a Ctrl+Space shortcut bound elsewhere still sees it.</summary>
    [Fact]
    public void Handle_WhenReleasesAreExpectedAndSpacePressHasControl_DoesNotActivateAndLeavesUnhandled()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var press = Space(KeyAction.Press, Modifiers.Control);
        behavior.Handle(press);

        press.IsHandled.ShouldBeFalse();
        pressed.ShouldBeEmpty();

        behavior.Handle(Space(KeyAction.Release, Modifiers.Control));
        activations.ShouldBeEmpty();
    }

    /// <summary>The press-only-terminal pulse path must gate the same as the held path, or a
    /// press-only terminal leaks Ctrl+Space straight through to activation.</summary>
    [Fact]
    public void Handle_WhenReleasesAreNotExpectedAndSpacePressHasControl_DoesNotActivateAndLeavesUnhandled()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: false, activations, pressed);

        var press = Space(KeyAction.Press, Modifiers.Control);
        behavior.Handle(press);

        press.IsHandled.ShouldBeFalse();
        activations.ShouldBeEmpty();
        pressed.ShouldBeEmpty();
    }

    /// <summary>Shift rides along with plenty of ordinary Space presses (capitalized text, etc.)
    /// and must not block activation.</summary>
    [Fact]
    public void Handle_WhenSpacePressHasShift_StillActivates()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var press = Space(KeyAction.Press, Modifiers.Shift);
        behavior.Handle(press);

        press.IsHandled.ShouldBeTrue();
        pressed.ShouldBe([true]);

        var release = Space(KeyAction.Release, Modifiers.Shift);
        behavior.Handle(release);

        release.IsHandled.ShouldBeTrue();
        activations.ShouldBe([ActivationCause.Keyboard]);
    }

    /// <summary>Space's gate applies symmetrically to the release arm too: an incidental Control
    /// modifier that only rides the release must not commit the activation the eligible press
    /// armed, even though the release still consumes the stroke and clears the pressed frame.</summary>
    [Fact]
    public void Handle_WhenSpaceReleaseHasControlAfterEligiblePress_IsHandledWithoutActivating()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var press = Space(KeyAction.Press);
        behavior.Handle(press);
        press.IsHandled.ShouldBeTrue();

        var release = Space(KeyAction.Release, Modifiers.Control);
        behavior.Handle(release);

        release.IsHandled.ShouldBeTrue();
        activations.ShouldBeEmpty();
        pressed.ShouldBe([true, false]);
    }

    /// <summary>The gate covers Space's paired release too, not just the arming press - an
    /// ancestor that saw a gated Ctrl+Space press bubble past it must see the paired release
    /// bubble as well, instead of finding it silently swallowed here.</summary>
    [Fact]
    public void Handle_WhenSpacePressAndReleaseHaveControl_LeavesReleaseUnhandled()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var press = Space(KeyAction.Press, Modifiers.Control);
        behavior.Handle(press);
        press.IsHandled.ShouldBeFalse();

        var release = Space(KeyAction.Release, Modifiers.Control);
        behavior.Handle(release);

        release.IsHandled.ShouldBeFalse();
        activations.ShouldBeEmpty();
        pressed.ShouldBeEmpty();
    }

    /// <summary>An incidental Control modifier must not commit an Enter activation, and the press
    /// itself must stay unhandled so a Ctrl+Enter shortcut bound elsewhere still sees it.</summary>
    [Fact]
    public void Handle_WhenEnterPressHasControl_DoesNotActivateAndLeavesUnhandled()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var press = Enter(KeyAction.Press, Modifiers.Control);
        behavior.Handle(press);

        press.IsHandled.ShouldBeFalse();
        activations.ShouldBeEmpty();
        pressed.ShouldBeEmpty();
    }

    /// <summary>Shift-held Enter (a common terminal chord) must still activate immediately.</summary>
    [Fact]
    public void Handle_WhenEnterPressHasShift_StillActivates()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var press = Enter(KeyAction.Press, Modifiers.Shift);
        behavior.Handle(press);

        press.IsHandled.ShouldBeTrue();
        activations.ShouldBe([ActivationCause.Keyboard]);
        pressed.ShouldBe([true, false]);
    }

    /// <summary>The gate covers Enter's paired release too, not just the activating press - an
    /// ancestor that saw a gated Ctrl+Enter press bubble past it must see the paired release bubble
    /// as well, instead of finding it silently swallowed here.</summary>
    [Fact]
    public void Handle_WhenEnterReleaseHasControl_LeavesUnhandled()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var release = Enter(KeyAction.Release, Modifiers.Control);
        behavior.Handle(release);

        release.IsHandled.ShouldBeFalse();
        activations.ShouldBeEmpty();
        pressed.ShouldBeEmpty();
    }

    /// <summary>An unmodified Enter release keeps its existing behavior: swallowed without
    /// activating, since Enter already committed on the press.</summary>
    [Fact]
    public void Handle_WhenEnterReleaseHasNoModifiers_IsHandledWithoutActivating()
    {
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);

        var release = Enter(KeyAction.Release);
        behavior.Handle(release);

        release.IsHandled.ShouldBeTrue();
        activations.ShouldBeEmpty();
        pressed.ShouldBeEmpty();
    }

    /// <summary>An inherited convenience event may consume input before the composed default is
    /// reached; press behavior must honor that routed verdict instead of activating anyway.</summary>
    [Fact]
    public void Handle_WhenEventIsAlreadyHandled_DoesNotRunPressDefaults()
    {
        // Arrange
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var behavior = Create(releasesExpected: true, activations, pressed);
        var input = Enter(KeyAction.Press);
        input.IsHandled = true;

        // Act
        behavior.Handle(input);

        // Assert
        activations.ShouldBeEmpty();
        pressed.ShouldBeEmpty();
    }

    /// <summary>A release for another physical button must not complete an armed primary
    /// gesture or give up its capture.</summary>
    [Fact]
    public void Handle_WhenSecondaryReleaseArrivesDuringPrimaryHold_PreservesGesture()
    {
        // Arrange
        List<ActivationCause> activations = [];
        List<bool> pressed = [];
        var captured = false;
        var behavior = new PressBehavior(
            static () => new Rect(0, 0, 10, 1),
            static () => true,
            static () => true,
            static () => true,
            () => captured = true,
            () => captured,
            () => captured = false,
            pressed.Add,
            activations.Add,
            static () => true);
        behavior.Handle(Pointer(Buttons.Primary, PointerAction.Press));
        var secondaryRelease = Pointer(Buttons.Secondary, PointerAction.Release);

        // Act
        behavior.Handle(secondaryRelease);

        // Assert
        secondaryRelease.IsHandled.ShouldBeFalse();
        captured.ShouldBeTrue();
        pressed.ShouldBe([true]);
        activations.ShouldBeEmpty();

        behavior.Handle(Pointer(Buttons.Primary, PointerAction.Release));
        captured.ShouldBeFalse();
        pressed.ShouldBe([true, true, false]);
        activations.ShouldBe([ActivationCause.Pointer]);
    }
}
