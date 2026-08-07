// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Enumerates raw CSI parameter and subparameter fields without allocation.
/// </summary>
/// <remarks>
/// The input span is borrowed for the lifetime of this reader. A private marker
/// in the range 0x3c through 0x3f is exposed separately. Callers retain colon
/// boundaries so SGR subparameters are never flattened into semicolon values.
/// </remarks>
/// <example>
/// <code>
/// var values = new Parameters("?25"u8);
/// var status = values.Read(out var value, out var separator);
/// </code>
/// </example>
[PublicAPI]
public ref struct Parameters
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly int _maxCount;
    private readonly int _maxValue;
    private int _position;
    private int _count;
    private bool _finished;

    /// <summary>
    /// Initializes a bounded reader over <paramref name="input"/>.
    /// </summary>
    /// <param name="input">Raw CSI parameter bytes in the range 0x30 through 0x3f.</param>
    /// <param name="maxCount">The maximum number of fields to return.</param>
    /// <param name="maxValue">The maximum accepted numeric value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxCount"/> or <paramref name="maxValue"/> is not
    /// positive.
    /// </exception>
    public Parameters(ReadOnlySpan<byte> input, int maxCount = 32, int maxValue = 65_535)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxValue);

        if (!input.IsEmpty && input[0] is >= 0x3c and <= 0x3f)
        {
            PrivateMarker = input[0];
            _input = input[1..];
        }
        else
        {
            PrivateMarker = 0;
            _input = input;
        }

        _maxCount = maxCount;
        _maxValue = maxValue;
        _position = 0;
        _count = 0;
        _finished = _input.IsEmpty;
    }

    /// <summary>
    /// Gets the initial private marker or zero when the sequence has none.
    /// </summary>
    public readonly byte PrivateMarker { get; }

    /// <summary>
    /// Reads the next decimal or default field.
    /// </summary>
    /// <param name="value">
    /// Receives the numeric value, or zero for a non-value result.
    /// </param>
    /// <param name="separator">
    /// Receives the delimiter following this field.
    /// </param>
    /// <returns>The field status.</returns>
    public ParameterStatus Read(out int value, out ParameterSeparator separator)
    {
        value = 0;
        separator = ParameterSeparator.None;

        if (_finished)
        {
            return ParameterStatus.End;
        }

        if (_count >= _maxCount)
        {
            _finished = true;

            return ParameterStatus.Limit;
        }

        _count++;
        var start = _position;
        var status = ParameterStatus.Value;

        // Preserve the delimiter kind because colon fields have different
        // semantics from independent semicolon parameters.
        while (_position < _input.Length)
        {
            var item = _input[_position];

            if (item is (byte) ';' or (byte) ':')
            {
                separator = item == (byte) ';'
                    ? ParameterSeparator.Semicolon
                    : ParameterSeparator.Colon;
                break;
            }

            if (item is < (byte) '0' or > (byte) '9')
            {
                // Overflow is a more specific diagnosis than a bare non-decimal byte; once a
                // field has already overflowed, a later stray byte in the same field must not
                // downgrade that diagnosis back to the less specific Invalid.
                if (status != ParameterStatus.Overflow)
                {
                    status = ParameterStatus.Invalid;
                }
            }
            else if (status == ParameterStatus.Value)
            {
                var digit = item - (byte) '0';

                if (value > _maxValue / 10 ||
                    (value == _maxValue / 10 && digit > _maxValue % 10))
                {
                    value = 0;
                    status = ParameterStatus.Overflow;
                }
                else
                {
                    value = (value * 10) + digit;
                }
            }

            _position++;
        }

        if (_position == start)
        {
            status = ParameterStatus.Default;
        }

        if (_position < _input.Length)
        {
            _position++;
        }
        else
        {
            _finished = true;
        }

        return status;
    }
}
