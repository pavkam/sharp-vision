namespace SharpVision.Threading;

/// <summary>Provides one finite dispatcher work item.</summary>
internal abstract class Work
{
    /// <summary>Executes the work item on the owning dispatcher thread.</summary>
    internal abstract void Execute();

    /// <summary>Cancels the work item before execution.</summary>
    internal virtual void Cancel()
    {
    }
}
