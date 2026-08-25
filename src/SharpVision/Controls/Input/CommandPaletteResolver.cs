// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Resolves a fresh command-palette result snapshot for freely edited search terms.</summary>
/// <param name="searchTerms">The current non-null editor text.</param>
/// <param name="cancellationToken">Cancels work superseded by a newer query or component disposal.</param>
/// <returns>A non-null borrowed result snapshot. The palette copies it before publication.</returns>
public delegate ValueTask<IReadOnlyList<object?>> CommandPaletteResolver(
    string searchTerms,
    CancellationToken cancellationToken);
