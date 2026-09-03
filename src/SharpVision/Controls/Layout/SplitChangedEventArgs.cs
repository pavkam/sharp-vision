// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Reports one committed authored leading-pane length transition.</summary>
[PublicAPI]
public sealed class SplitChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable split-length transition.</summary>
    /// <param name="previousLength">The authored leading-pane length before the transition.</param>
    /// <param name="length">The committed authored leading-pane length.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="previousLength"/> or <paramref name="length"/> is not a fixed-cell or
    /// percentage length.
    /// </exception>
    public SplitChangedEventArgs(Length previousLength, Length length)
    {
        ValidateAuthoredLength(previousLength, nameof(previousLength));
        ValidateAuthoredLength(length, nameof(length));
        PreviousLength = previousLength;
        Length = length;
    }

    /// <summary>Gets the authored leading-pane length before the transition.</summary>
    public Length PreviousLength { get; }

    /// <summary>Gets the committed authored leading-pane length.</summary>
    public Length Length { get; }

    private static void ValidateAuthoredLength(Length length, string parameterName)
    {
        if (length.Kind is not (LengthKind.Cells or LengthKind.Percent))
        {
            throw new ArgumentException(
                "A split length must use fixed cells or a percentage.",
                parameterName);
        }
    }
}
