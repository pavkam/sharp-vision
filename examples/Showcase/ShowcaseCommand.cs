// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

using System.Windows.Input;

/// <summary>Provides an ordinary delegate-backed command for executable showcase recipes.</summary>
internal sealed class ShowcaseCommand: ICommand
{
    private readonly Predicate<object?> _canExecute;
    private readonly Action<object?> _execute;

    /// <summary>Initializes a command from non-null execution and availability delegates.</summary>
    /// <param name="execute">The action that receives the borrowed command parameter.</param>
    /// <param name="canExecute">The query that decides whether the action is currently available.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> or <paramref name="canExecute"/> is null.</exception>
    internal ShowcaseCommand(Action<object?> execute, Predicate<object?> canExecute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(canExecute);
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute(parameter);

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>Notifies observers after the availability inputs change.</summary>
    internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
