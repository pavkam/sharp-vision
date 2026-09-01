// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Describes a failed current suggestion resolution and its stable search terms.</summary>
[PublicAPI]
public sealed class SuggestionResolutionFailedEventArgs: EventArgs
{
    /// <summary>Initializes a failure payload for one non-null search-text snapshot.</summary>
    /// <param name="searchTerms">The non-null search terms supplied to the resolver; an empty value is valid.</param>
    /// <param name="exception">The non-null resolver or completion failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="searchTerms"/> is null.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public SuggestionResolutionFailedEventArgs(string searchTerms, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(searchTerms);
        ArgumentNullException.ThrowIfNull(exception);

        SearchTerms = searchTerms;
        Exception = exception;
    }

    /// <summary>Gets the immutable search-text snapshot whose current resolution failed.</summary>
    public string SearchTerms { get; }

    /// <summary>Gets the resolver or completion failure.</summary>
    public Exception Exception { get; }
}
