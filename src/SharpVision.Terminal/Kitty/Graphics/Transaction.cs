// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

using Capabilities;

/// <summary>Correlates exactly one Kitty graphics response and rejects identifier reuse.</summary>
[PublicAPI]
public sealed class Transaction
{
    private readonly uint _imageId;

    /// <summary>Initializes one nonzero image correlation.</summary>
    /// <param name="imageId">The nonzero renderer-owned query identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="imageId"/> is zero.</exception>
    public Transaction(uint imageId)
    {
        if (imageId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageId), imageId, "A transaction identifier must be nonzero.");
        }

        _imageId = imageId;
    }

    /// <summary>Gets the first matched owned response.</summary>
    public KittyGraphicsResponse? Response { get; private set; }

    /// <summary>Matches one typed response without consuming another image identity.</summary>
    /// <param name="response">The non-null response.</param>
    /// <returns>Matched, duplicate, or unknown correlation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public QueryMatch Accept(KittyGraphicsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsValid || response.ImageId != _imageId)
        {
            return QueryMatch.Unknown;
        }

        if (Response is not null)
        {
            return QueryMatch.Duplicate;
        }

        Response = response;
        return QueryMatch.Matched;
    }
}
