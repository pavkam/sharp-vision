// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

/// <summary>Owns one strict, bounded, redaction-safe Kitty graphics APC response.</summary>
[PublicAPI]
public sealed class KittyGraphicsResponse
{
    private KittyGraphicsResponse(
        bool isValid,
        uint imageId,
        uint placementId,
        bool isSuccess,
        string? message,
        Diagnostic? diagnostic)
    {
        IsValid = isValid;
        ImageId = imageId;
        PlacementId = placementId;
        IsSuccess = isSuccess;
        Message = message;
        Diagnostic = diagnostic;
    }

    /// <summary>Gets whether strict grammar and finite bounds were satisfied.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the nonzero correlated image identifier, or zero when invalid.</summary>
    public uint ImageId { get; }

    /// <summary>Gets the optional nonzero placement identifier.</summary>
    public uint PlacementId { get; }

    /// <summary>Gets whether the terminal returned the exact success token <c>OK</c>.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the bounded, owned printable terminal message, or null when invalid.</summary>
    /// <remarks>This is untrusted terminal text. Diagnostics and <see cref="ToString"/> never include it.</remarks>
    public string? Message { get; }

    /// <summary>Gets a redacted structural diagnostic for an invalid response.</summary>
    public Diagnostic? Diagnostic { get; }

    /// <summary>Parses one APC payload beginning with the Kitty <c>G</c> selector.</summary>
    /// <param name="value">The complete borrowed APC payload without framing.</param>
    /// <param name="limits">Optional finite metadata policy.</param>
    /// <returns>An owned valid response or redacted invalid value.</returns>
    public static KittyGraphicsResponse Parse(ReadOnlySpan<byte> value, KittyMetadataLimits? limits = null)
    {
        var policy = limits ?? KittyMetadataLimits.Default;

        if (value.Length > policy.MaxMetadataBytes)
        {
            return Invalid(DiagnosticCode.StringLimit, value.Length);
        }

        if (value.Length < 6 || value[0] != (byte) 'G')
        {
            return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
        }

        var separator = value.IndexOf((byte) ';');

        if (separator <= 1 || separator == value.Length - 1)
        {
            return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
        }

        var metadata = value[1..separator];
        var message = value[(separator + 1)..];
        uint imageId = 0;
        uint placementId = 0;
        var seenImage = false;
        var seenPlacement = false;

        while (!metadata.IsEmpty)
        {
            var comma = metadata.IndexOf((byte) ',');
            var field = comma < 0 ? metadata : metadata[..comma];
            metadata = comma < 0 ? [] : metadata[(comma + 1)..];
            var equals = field.IndexOf((byte) '=');

            if (equals != 1 || field.Length < 3)
            {
                return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
            }

            var number = field[2..];

            if (field[0] == (byte) 'i')
            {
                if (seenImage || !TryParseIdentifier(number, out imageId))
                {
                    return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
                }

                seenImage = true;
            }
            else if (field[0] == (byte) 'p')
            {
                if (seenPlacement || !TryParseIdentifier(number, out placementId))
                {
                    return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
                }

                seenPlacement = true;
            }
            else
            {
                return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
            }
        }

        if (!seenImage || !IsPrintable(message))
        {
            return Invalid(DiagnosticCode.InvalidMetadata, value.Length);
        }

        var ownedMessage = Encoding.ASCII.GetString(message);
        return new KittyGraphicsResponse(
            isValid: true,
            imageId,
            placementId,
            isSuccess: message.SequenceEqual("OK"u8),
            ownedMessage,
            diagnostic: null);
    }

    /// <summary>Returns only structural state and identifiers, never the terminal error message.</summary>
    /// <returns>A redaction-safe description.</returns>
    public override string ToString() =>
        $"KittyGraphicsResponse valid={IsValid} image={ImageId} placement={PlacementId} success={IsSuccess}";

    private static KittyGraphicsResponse Invalid(DiagnosticCode code, int discardedBytes) => new(
        isValid: false,
        imageId: 0,
        placementId: 0,
        isSuccess: false,
        message: null,
        new Diagnostic(code, SequenceKind.Apc, offset: 0, discardedBytes));

    private static bool IsPrintable(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        foreach (var item in value)
        {
            if (item is < 0x20 or > 0x7e)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseIdentifier(ReadOnlySpan<byte> value, out uint result)
    {
        result = 0;

        if (value.IsEmpty || (value.Length > 1 && value[0] == (byte) '0'))
        {
            return false;
        }

        foreach (var item in value)
        {
            if (item is < (byte) '0' or > (byte) '9')
            {
                result = 0;
                return false;
            }

            var digit = (uint) (item - (byte) '0');

            if (result > (uint.MaxValue - digit) / 10)
            {
                result = 0;
                return false;
            }

            result = (result * 10) + digit;
        }

        return result != 0;
    }
}
