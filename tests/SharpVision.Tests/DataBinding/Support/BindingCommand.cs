// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding.Support;

using System.Windows.Input;

/// <summary>Records one bound command execution.</summary>
internal sealed class BindingCommand: ICommand
{
    /// <summary>Raised when availability changes.</summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>Gets or sets whether execution is available.</summary>
    internal bool Enabled { get; set; } = true;

    /// <summary>Gets the last borrowed execution parameter.</summary>
    internal object? ExecutedParameter { get; private set; }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        _ = parameter;
        return Enabled;
    }

    /// <inheritdoc/>
    public void Execute(object? parameter) => ExecutedParameter = parameter;

    /// <summary>Publishes changed availability.</summary>
    internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
