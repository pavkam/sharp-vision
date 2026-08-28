// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>An <see cref="InputBase"/> derivative that enables only press activation, with no
/// caption, segment editing, or popup - proving press activation composes independently of the
/// caption capability's single-caption role.</summary>
internal sealed class PressOnlyInputProbe: InputBase
{
    /// <summary>Initializes a probe with only press activation enabled.</summary>
    internal PressOnlyInputProbe() => EnablePressActivation();

    /// <summary>Gets completed activation causes in commit order.</summary>
    internal List<ActivationCause> Activations { get; } = [];

    /// <summary>Gets or sets an optional callback invoked after an activation is recorded.</summary>
    internal Action? ActivationCallback { get; set; }

    /// <summary>Attempts to enable press activation a second time.</summary>
    internal void EnablePressActivationAgain() => EnablePressActivation();

    /// <summary>Routes one semantic activation through the shared availability gate.</summary>
    /// <param name="cause">The activation source to validate and attempt.</param>
    /// <returns><see langword="true"/> when activation was admitted; otherwise <see langword="false"/>.</returns>
    internal bool TryActivateFromTest(ActivationCause cause) => TryActivate(cause);

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        Activations.Add(cause);
        ActivationCallback?.Invoke();
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }
}
