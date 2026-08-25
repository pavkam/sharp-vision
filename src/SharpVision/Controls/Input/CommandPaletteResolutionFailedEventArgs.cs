// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Reports a command-palette resolver failure for the still-current search terms.</summary>
[PublicAPI]
public sealed class CommandPaletteResolutionFailedEventArgs: EventArgs
{
    /// <summary>Initializes one failed resolution report.</summary>
    /// <param name="searchTerms">The non-null search terms passed to the resolver.</param>
    /// <param name="exception">The non-null resolver failure.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CommandPaletteResolutionFailedEventArgs(string searchTerms, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(searchTerms);
        ArgumentNullException.ThrowIfNull(exception);
        SearchTerms = searchTerms;
        Exception = exception;
    }

    /// <summary>Gets the search terms whose resolution failed.</summary>
    public string SearchTerms { get; }

    /// <summary>Gets the resolver failure.</summary>
    public Exception Exception { get; }
}
