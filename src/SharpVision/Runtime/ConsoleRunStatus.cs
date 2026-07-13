namespace SharpVision.Runtime;

/// <summary>Reports how an interactive console run completed.</summary>
public enum ConsoleRunStatus
{
    /// <summary>Standard input or output was redirected, so no application started.</summary>
    Redirected,

    /// <summary>The application started and shut down without a primary failure.</summary>
    Completed,

    /// <summary>The caller or host requested shutdown, typically through Ctrl+C.</summary>
    Cancelled,

    /// <summary>The application reported a primary runtime failure.</summary>
    Failed,
}
