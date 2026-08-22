// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents;

/// <summary>Defines format-independent bounded-reading limits.</summary>
[PublicAPI]
public sealed class DocumentReadOptions
{
    /// <summary>Gets or sets the positive maximum UTF-16 source length.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaximumCharacters
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 4 * 1024 * 1024;
}
