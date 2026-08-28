// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Windows.Input;

/// <summary>Records command queries/executions and exposes deterministic executability.</summary>
internal sealed class ProbeCommand: ICommand
{
    private EventHandler? _canExecuteChanged;

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add
        {
            Adding?.Invoke();

            if (ThrowOnNextAdd)
            {
                ThrowOnNextAdd = false;
                throw new InvalidOperationException("Synthetic command subscription failure.");
            }

            _canExecuteChanged += value;

            if (ThrowAfterNextAdd)
            {
                ThrowAfterNextAdd = false;
                throw new InvalidOperationException("Synthetic post-registration command subscription failure.");
            }

            AddCount++;
        }
        remove
        {
            Removing?.Invoke();

            if (ThrowOnNextRemove)
            {
                ThrowOnNextRemove = false;
                throw new InvalidOperationException("Synthetic command unsubscription failure.");
            }

            _canExecuteChanged -= value;
            RemoveCount++;
        }
    }

    /// <summary>Gets whether anything is currently subscribed to <see cref="CanExecuteChanged"/>.</summary>
    internal bool HasCanExecuteChangedSubscribers => _canExecuteChanged is not null;

    /// <summary>Gets the number of currently registered handlers.</summary>
    internal int SubscriberCount => _canExecuteChanged?.GetInvocationList().Length ?? 0;

    /// <summary>Gets the number of completed add accessors.</summary>
    internal int AddCount { get; private set; }

    /// <summary>Gets the number of completed remove accessors.</summary>
    internal int RemoveCount { get; private set; }

    /// <summary>Gets optional work invoked from the event add accessor.</summary>
    internal Action? Adding { get; set; }

    /// <summary>Gets optional work invoked from the event remove accessor.</summary>
    internal Action? Removing { get; set; }

    /// <summary>Gets or sets whether the next add accessor throws before registration.</summary>
    internal bool ThrowOnNextAdd { get; set; }

    /// <summary>Gets or sets whether the next add accessor throws after handler registration.</summary>
    internal bool ThrowAfterNextAdd { get; set; }

    /// <summary>Gets or sets whether the next remove accessor throws before removal.</summary>
    internal bool ThrowOnNextRemove { get; set; }

    /// <summary>Gets or sets whether this instance compares equal to every other probe command.</summary>
    internal bool EqualsOtherCommands { get; set; }

    /// <summary>Gets or sets the query result.</summary>
    internal bool CanExecuteValue { get; set; } = true;

    /// <summary>Gets optional work invoked during execution.</summary>
    internal Action<object?>? Executing { get; set; }

    /// <summary>Gets query parameters in order.</summary>
    internal List<object?> Queries { get; } = [];

    /// <summary>Gets execution parameters in order.</summary>
    internal List<object?> Executions { get; } = [];

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        Queries.Add(parameter);
        return CanExecuteValue;
    }

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        Executions.Add(parameter);
        Executing?.Invoke(parameter);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => EqualsOtherCommands && obj is ProbeCommand;

    /// <inheritdoc/>
    public override int GetHashCode() => EqualsOtherCommands ? 0 : base.GetHashCode();

    /// <summary>Raises executability change synchronously.</summary>
    internal void RaiseCanExecuteChanged() => _canExecuteChanged?.Invoke(this, EventArgs.Empty);
}
