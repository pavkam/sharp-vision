namespace SharpVision.Tests.Support;

using System.Windows.Input;

/// <summary>Records command queries/executions and exposes deterministic executability.</summary>
internal sealed class ProbeCommand: ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

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

    /// <summary>Raises executability change synchronously.</summary>
    internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
