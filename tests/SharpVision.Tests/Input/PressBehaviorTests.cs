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
    private static KeyEventArgs Space(KeyAction action) => new(
        new Stroke(Code.Character, new Rune(' '), 0, Modifiers.None, action));

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

        press.Handled.ShouldBeTrue();
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
}
