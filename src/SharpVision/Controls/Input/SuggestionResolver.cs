// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Resolves one stable editor-text snapshot into an owned suggestion source snapshot.</summary>
/// <param name="searchTerms">The non-null text captured for this resolution.</param>
/// <param name="cancellationToken">Signals that a newer request or owner lifetime superseded this resolution.</param>
/// <returns>A task producing the non-null borrowed values to copy into the current suggestion snapshot.</returns>
[PublicAPI]
public delegate ValueTask<IReadOnlyList<object?>> SuggestionResolver(
    string searchTerms,
    CancellationToken cancellationToken);
