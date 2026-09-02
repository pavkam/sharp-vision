// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

/// <summary>Raises desktop notifications through the terminal when supported.</summary>
/// <remarks>
/// There is no reliable environment or query signal for desktop-notification support, so this is
/// never auto-detected: only an explicit
/// <see cref="CapabilityOverrides.Notifications"/> opt-in can
/// make <see cref="IsSupported"/> true. See the
/// <a href="../../docs/concepts/safe-degradation.md">safe-degradation contract</a> for the general
/// fallback rule this follows.
/// </remarks>
[PublicAPI]
public interface INotifications
{
    /// <summary>Gets whether the active profile authorizes notification output through an
    /// explicit opt-in override.</summary>
    public bool IsSupported { get; }

    /// <summary>Raises a body-only desktop notification using OSC 9; a no-op when unsupported.</summary>
    /// <param name="body">The non-null notification body without terminal control characters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="IsSupported"/> is <see langword="true"/> and <paramref name="body"/> contains a
    /// terminal control character.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="IsSupported"/> is <see langword="true"/> and the dispatcher's bounded post queue
    /// was full at the moment of the call.
    /// </exception>
    public void Notify(string body);

    /// <summary>Raises a titled desktop notification using OSC 777; a no-op when unsupported.</summary>
    /// <param name="title">
    /// The non-null notification title without terminal control characters or a literal
    /// <c>;</c> character.
    /// </param>
    /// <param name="body">The non-null notification body without terminal control characters.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="title"/> or <paramref name="body"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="IsSupported"/> is <see langword="true"/> and <paramref name="title"/> or
    /// <paramref name="body"/> contains a terminal control character, or <paramref name="title"/>
    /// contains a <c>;</c> character.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="IsSupported"/> is <see langword="true"/> and the dispatcher's bounded post queue
    /// was full at the moment of the call.
    /// </exception>
    public void Notify(string title, string body);
}
