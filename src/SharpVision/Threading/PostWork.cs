namespace SharpVision.Threading;

/// <summary>Executes one fire-and-observe dispatcher callback.</summary>
internal sealed class PostWork(Action action): Work
{
    /// <inheritdoc/>
    internal override void Execute() => action();
}
