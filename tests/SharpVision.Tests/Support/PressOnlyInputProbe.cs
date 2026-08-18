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

    /// <summary>Attempts to enable press activation a second time.</summary>
    internal void EnablePressActivationAgain() => EnablePressActivation();

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) => Activations.Add(cause);

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }
}
