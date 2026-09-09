// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Windows.Input;

/// <summary>Holds one command and its parameter captured at an activation entry point.</summary>
/// <remarks>
/// <see cref="InputBase.CaptureCommand"/> returns this borrowed snapshot before an activation
/// publishes any callback, so reentrant rebinding or disposal during that callback cannot redirect
/// work the activation already accepted. A concrete control captures once, then calls
/// <see cref="ExecuteIfAny"/> (or checks <see cref="CanExecute"/> itself when it needs to gate
/// other work, such as suppressing an event, on the same decision) against the captured value
/// rather than re-reading <see cref="InputBase.Command"/> and
/// <see cref="InputBase.CommandParameter"/> after the callback runs.
/// </remarks>
public readonly record struct CommandBinding
{
    /// <summary>Initializes a binding with its captured command and parameter.</summary>
    /// <param name="command">The captured command, or null when none is bound.</param>
    /// <param name="parameter">The captured parameter passed to <paramref name="command"/>.</param>
    public CommandBinding(ICommand? command, object? parameter)
    {
        Command = command;
        Parameter = parameter;
    }

    /// <summary>Gets the captured command, or null when none is bound.</summary>
    public ICommand? Command { get; }

    /// <summary>Gets the captured parameter passed to <see cref="Command"/>.</summary>
    public object? Parameter { get; }

    /// <summary>Gets whether this binding allows execution: no command is bound, or the bound
    /// command reports it can currently execute with <see cref="Parameter"/>.</summary>
    /// <returns>True when execution is currently allowed.</returns>
    public bool CanExecute() => Command is null || Command.CanExecute(Parameter);

    /// <summary>Executes the bound command with <see cref="Parameter"/> when one is bound and
    /// <see cref="CanExecute"/> allows it. A no-op when no command is bound.</summary>
    public void ExecuteIfAny()
    {
        if (Command is not null && Command.CanExecute(Parameter))
        {
            Command.Execute(Parameter);
        }
    }

    /// <summary>Deconstructs this binding into its captured command and parameter, for a caller
    /// that prefers tuple-style consumption.</summary>
    /// <param name="command">Receives <see cref="Command"/>.</param>
    /// <param name="parameter">Receives <see cref="Parameter"/>.</param>
    public void Deconstruct(out ICommand? command, out object? parameter)
    {
        command = Command;
        parameter = Parameter;
    }
}
