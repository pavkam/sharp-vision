// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies one immutable typed routed event.</summary>
/// <typeparam name="TArgs">The exact event-argument type accepted by handlers.</typeparam>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Event is the intentional public routed-input domain term.")]
[PublicAPI]
public sealed class Event<TArgs>: IEvent where TArgs : RoutedEventArgs
{
    /// <summary>Initializes a named routed-event identifier.</summary>
    /// <param name="name">The non-empty diagnostic name.</param>
    /// <param name="strategy">The route traversal strategy.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="strategy"/> is unknown.
    /// </exception>
    public Event(string name, Strategy strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(strategy),
                strategy,
                "The routing strategy is unknown.");
        }

        Name = name;
        Strategy = strategy;
    }

    /// <summary>Gets the diagnostic event name.</summary>
    public string Name { get; }

    /// <summary>Gets the ancestry traversal strategy.</summary>
    public Strategy Strategy { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
