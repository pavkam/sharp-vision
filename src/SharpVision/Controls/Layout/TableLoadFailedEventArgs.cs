// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Describes one progressive range that exhausted its bounded retry attempts.</summary>
public sealed class TableLoadFailedEventArgs: EventArgs
{
    /// <summary>Initializes a validated load-failure snapshot.</summary>
    /// <param name="request">The range that failed.</param>
    /// <param name="exception">The non-null final failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public TableLoadFailedEventArgs(TableDataRequest request, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Request = request;
        Exception = exception;
    }

    /// <summary>Gets the range that failed.</summary>
    public TableDataRequest Request { get; }

    /// <summary>Gets the final failure after retries were exhausted.</summary>
    public Exception Exception { get; }
}
