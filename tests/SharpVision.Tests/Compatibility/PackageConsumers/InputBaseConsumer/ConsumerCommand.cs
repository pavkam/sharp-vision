// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.InputBaseConsumer;

/// <summary>A minimal always-executable command recording its last executed parameter, used to
/// prove the packed InputBase's command capability resolves and executes from outside the
/// assembly.</summary>
internal sealed class ConsumerCommand: ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <summary>Gets the parameter passed to the most recent <see cref="Execute"/> call, or null
    /// before one occurs.</summary>
    public object? ExecutedParameter { get; private set; }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        ExecutedParameter = parameter;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
