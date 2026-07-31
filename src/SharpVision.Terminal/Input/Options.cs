// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Clipboard;

/// <summary>Defines finite immutable input decoding policy.</summary>
[PublicAPI]
public sealed record Options
{
    /// <summary>Gets conservative default input policy.</summary>
    public static Options Default { get; } = new();

    /// <summary>Gets the lone-Escape ambiguity timeout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan EscapeTimeout
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The Escape timeout must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromMilliseconds(50);

    /// <summary>Gets the positive maximum retained paste byte count.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxPasteBytes
    {
        get;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The paste limit must be positive.");
    } = 16 * 1024 * 1024;

    /// <summary>Gets protocol parser limits used by the decoder.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public ParserLimits ParserLimits
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = ParserLimits.Default;

    /// <summary>Gets terminfo parameter-program limits used to interpret key and control programs.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public ProgramLimits ProgramLimits
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = ProgramLimits.Default;

    /// <summary>Gets capability-query limits used to bound XTGETTCAP and similar in-band replies.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public QueryLimits QueryLimits
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = QueryLimits.Default;

    /// <summary>Gets clipboard transfer limits used to bound Kitty graphics APC responses.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public TransferLimits TransferLimits
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TransferLimits.Default;

    /// <summary>Gets optional positive cell-pixel dimensions for pixel mouse inference.</summary>
    public Metrics? CellMetrics { get; init; }

    /// <summary>
    /// Gets whether SGR pointer coordinates are pixels rather than cells.
    /// </summary>
    public bool PixelMouse { get; init; }

    /// <summary>Gets the active immutable terminal-description key map.</summary>
    internal KeyMap KeyMap { get; init; } = KeyMap.Empty;

    /// <summary>Gets whether the explicit built-in ANSI key grammar is active.</summary>
    internal bool UseAnsiKeyGrammar { get; init; } = true;

    /// <summary>Creates decoder policy carrying one active profile key map.</summary>
    /// <param name="keyMap">The non-null immutable map.</param>
    /// <param name="useAnsiKeyGrammar">Whether the explicit ANSI compatibility grammar remains active.</param>
    /// <returns>A copy with the requested key policy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keyMap"/> is null.</exception>
    internal Options WithKeyMap(KeyMap keyMap, bool useAnsiKeyGrammar)
    {
        ArgumentNullException.ThrowIfNull(keyMap);

        return this with
        {
            KeyMap = keyMap,
            UseAnsiKeyGrammar = useAnsiKeyGrammar
        };
    }
}
