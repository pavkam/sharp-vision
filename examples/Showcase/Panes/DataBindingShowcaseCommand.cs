// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Windows.Input;

/// <summary>Provides an always-enabled command for the data-binding showcase.</summary>
internal sealed class DataBindingShowcaseCommand: ICommand
{
    private readonly Action<object?> _execute;

    /// <summary>Initializes a command that forwards its borrowed parameter to one action.</summary>
    /// <param name="execute">The non-null action invoked by <see cref="Execute"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> is null.</exception>
    internal DataBindingShowcaseCommand(Action<object?> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
    }

    /// <summary>Accepts handlers for interface compatibility; this always-enabled command never changes availability.</summary>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <summary>Returns true for every borrowed parameter.</summary>
    /// <param name="parameter">The ignored borrowed parameter.</param>
    /// <returns>Always true.</returns>
    public bool CanExecute(object? parameter) => true;

    /// <summary>Forwards the borrowed parameter to the configured action.</summary>
    /// <param name="parameter">The borrowed parameter.</param>
    public void Execute(object? parameter) => _execute(parameter);
}
