// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Maps line-cell topology to and from Unicode or ASCII glyphs.</summary>
internal static class LineResolver
{
    /// <summary>Combines two topology values commutatively.</summary>
    /// <param name="left">The first topology.</param>
    /// <param name="right">The second topology.</param>
    /// <returns>The deterministic combined topology.</returns>
    public static Topology Merge(Topology left, Topology right)
    {
        var connections = left.Connections | right.Connections;
        var weight = (LineWeight) Math.Max((int) left.Line.Weight, (int) right.Line.Weight);
        var straight = connections is (LineConnections.Left | LineConnections.Right) or
            (LineConnections.Up | LineConnections.Down);
        var pattern = straight && left.Line.Pattern == right.Line.Pattern
            ? left.Line.Pattern
            : LinePattern.Solid;
        var rounded = left.Line.HasRoundedCorners && right.Line.HasRoundedCorners;
        var ascii = left.Line.IsAscii && right.Line.IsAscii;
        return new Topology(connections, new LineStyle(weight, pattern, rounded, ascii));
    }

    /// <summary>Resolves a topology to its exact or safely degraded Rune.</summary>
    /// <param name="value">The topology to resolve.</param>
    /// <param name="ambiguousWidth">The active frame width policy.</param>
    /// <returns>A printable one-cell Rune.</returns>
    public static Rune Resolve(Topology value, Ambiguous ambiguousWidth)
    {
        return value.Line.IsAscii || ambiguousWidth == Ambiguous.Wide
            ? new Rune(ResolveAscii(value.Connections))
            : TryResolvePattern(value, out var patterned)
                ? new Rune(patterned)
                : ResolveSolid(value);
    }

    /// <summary>Attempts to decode one previously produced line Rune.</summary>
    /// <param name="value">The candidate Rune.</param>
    /// <param name="topology">The decoded topology when recognized.</param>
    /// <returns>Whether the Rune belongs to a supported line family.</returns>
    public static bool TryDecode(Rune value, out Topology topology)
    {
        var decoded = value.Value switch
        {
            '-' => new Topology(LineConnections.Left | LineConnections.Right, LineStyle.Ascii),
            '|' => new Topology(LineConnections.Up | LineConnections.Down, LineStyle.Ascii),
            '+' => new Topology(
                LineConnections.Up | LineConnections.Right | LineConnections.Down | LineConnections.Left,
                LineStyle.Ascii),
            '─' or '╴' or '╶' => new Topology(DecodeLight(value.Value), LineStyle.Light),
            '│' or '╵' or '╷' => new Topology(DecodeLight(value.Value), LineStyle.Light),
            '┌' or '┐' or '└' or '┘' or '├' or '┤' or '┬' or '┴' or '┼' =>
                new Topology(DecodeLight(value.Value), LineStyle.Light),
            '╭' or '╮' or '╰' or '╯' =>
                new Topology(DecodeLight(value.Value), LineStyle.Rounded),
            '━' or '┃' or '╹' or '╺' or '╻' or '╸' or '┏' or '┓' or '┗' or '┛' or
                '┣' or '┫' or '┳' or '┻' or '╋' =>
                new Topology(DecodeHeavy(value.Value), LineStyle.Heavy),
            '═' or '║' or '╔' or '╗' or '╚' or '╝' or '╠' or '╣' or '╦' or '╩' or '╬' =>
                new Topology(DecodePaired(value.Value), LineStyle.Paired),
            '╌' => new Topology(
                LineConnections.Left | LineConnections.Right,
                new LineStyle(LineWeight.Light, LinePattern.DoubleDash)),
            '╎' => new Topology(
                LineConnections.Up | LineConnections.Down,
                new LineStyle(LineWeight.Light, LinePattern.DoubleDash)),
            '┄' => new Topology(
                LineConnections.Left | LineConnections.Right,
                new LineStyle(LineWeight.Light, LinePattern.TripleDash)),
            '┆' => new Topology(
                LineConnections.Up | LineConnections.Down,
                new LineStyle(LineWeight.Light, LinePattern.TripleDash)),
            '┈' => new Topology(
                LineConnections.Left | LineConnections.Right,
                new LineStyle(LineWeight.Light, LinePattern.QuadrupleDash)),
            '┊' => new Topology(
                LineConnections.Up | LineConnections.Down,
                new LineStyle(LineWeight.Light, LinePattern.QuadrupleDash)),
            '╍' => new Topology(
                LineConnections.Left | LineConnections.Right,
                new LineStyle(LineWeight.Heavy, LinePattern.DoubleDash)),
            '╏' => new Topology(
                LineConnections.Up | LineConnections.Down,
                new LineStyle(LineWeight.Heavy, LinePattern.DoubleDash)),
            '┅' => new Topology(
                LineConnections.Left | LineConnections.Right,
                new LineStyle(LineWeight.Heavy, LinePattern.TripleDash)),
            '┇' => new Topology(
                LineConnections.Up | LineConnections.Down,
                new LineStyle(LineWeight.Heavy, LinePattern.TripleDash)),
            '┉' => new Topology(
                LineConnections.Left | LineConnections.Right,
                new LineStyle(LineWeight.Heavy, LinePattern.QuadrupleDash)),
            '┋' => new Topology(
                LineConnections.Up | LineConnections.Down,
                new LineStyle(LineWeight.Heavy, LinePattern.QuadrupleDash)),
            _ => default
        };

        topology = decoded;
        return decoded.Connections != LineConnections.None;
    }

    private static int ResolveAscii(LineConnections value) => value switch
    {
        LineConnections.None => ' ',
        LineConnections.Left or LineConnections.Right or (LineConnections.Left | LineConnections.Right) => '-',
        LineConnections.Up or LineConnections.Down or (LineConnections.Up | LineConnections.Down) => '|',
        _ => '+'
    };

    private static bool TryResolvePattern(Topology value, out int rune)
    {
        rune = (value.Line.Weight, value.Line.Pattern, value.Connections) switch
        {
            (LineWeight.Light, LinePattern.DoubleDash, LineConnections.Left | LineConnections.Right) => '╌',
            (LineWeight.Light, LinePattern.DoubleDash, LineConnections.Up | LineConnections.Down) => '╎',
            (LineWeight.Light, LinePattern.TripleDash, LineConnections.Left | LineConnections.Right) => '┄',
            (LineWeight.Light, LinePattern.TripleDash, LineConnections.Up | LineConnections.Down) => '┆',
            (LineWeight.Light, LinePattern.QuadrupleDash, LineConnections.Left | LineConnections.Right) => '┈',
            (LineWeight.Light, LinePattern.QuadrupleDash, LineConnections.Up | LineConnections.Down) => '┊',
            (LineWeight.Heavy, LinePattern.DoubleDash, LineConnections.Left | LineConnections.Right) => '╍',
            (LineWeight.Heavy, LinePattern.DoubleDash, LineConnections.Up | LineConnections.Down) => '╏',
            (LineWeight.Heavy, LinePattern.TripleDash, LineConnections.Left | LineConnections.Right) => '┅',
            (LineWeight.Heavy, LinePattern.TripleDash, LineConnections.Up | LineConnections.Down) => '┇',
            (LineWeight.Heavy, LinePattern.QuadrupleDash, LineConnections.Left | LineConnections.Right) => '┉',
            (LineWeight.Heavy, LinePattern.QuadrupleDash, LineConnections.Up | LineConnections.Down) => '┋',
            _ => 0
        };
        return rune != 0;
    }

    private static bool TryResolveRounded(LineConnections value, out int rune)
    {
        rune = value switch
        {
            LineConnections.None => 0,
            LineConnections.Right | LineConnections.Down => '╭',
            LineConnections.Down | LineConnections.Left => '╮',
            LineConnections.Up | LineConnections.Right => '╰',
            LineConnections.Up | LineConnections.Left => '╯',
            LineConnections.Up or LineConnections.Right or LineConnections.Down or LineConnections.Left => 0,
            _ => 0
        };
        return rune != 0;
    }

    private static int ResolveLight(LineConnections value) => value switch
    {
        LineConnections.None => ' ',
        LineConnections.Up => '╵',
        LineConnections.Right => '╶',
        LineConnections.Down => '╷',
        LineConnections.Left => '╴',
        LineConnections.Left | LineConnections.Right => '─',
        LineConnections.Up | LineConnections.Down => '│',
        LineConnections.Right | LineConnections.Down => '┌',
        LineConnections.Down | LineConnections.Left => '┐',
        LineConnections.Up | LineConnections.Right => '└',
        LineConnections.Up | LineConnections.Left => '┘',
        LineConnections.Right | LineConnections.Down | LineConnections.Left => '┬',
        LineConnections.Up | LineConnections.Down | LineConnections.Left => '┤',
        LineConnections.Up | LineConnections.Right | LineConnections.Left => '┴',
        LineConnections.Up | LineConnections.Right | LineConnections.Down => '├',
        LineConnections.Up | LineConnections.Right | LineConnections.Down | LineConnections.Left => '┼',
        _ => ' '
    };

    private static int ResolveHeavy(LineConnections value) => value switch
    {
        LineConnections.None => ' ',
        LineConnections.Up => '╹',
        LineConnections.Right => '╺',
        LineConnections.Down => '╻',
        LineConnections.Left => '╸',
        LineConnections.Left | LineConnections.Right => '━',
        LineConnections.Up | LineConnections.Down => '┃',
        LineConnections.Right | LineConnections.Down => '┏',
        LineConnections.Down | LineConnections.Left => '┓',
        LineConnections.Up | LineConnections.Right => '┗',
        LineConnections.Up | LineConnections.Left => '┛',
        LineConnections.Right | LineConnections.Down | LineConnections.Left => '┳',
        LineConnections.Up | LineConnections.Down | LineConnections.Left => '┫',
        LineConnections.Up | LineConnections.Right | LineConnections.Left => '┻',
        LineConnections.Up | LineConnections.Right | LineConnections.Down => '┣',
        LineConnections.Up | LineConnections.Right | LineConnections.Down | LineConnections.Left => '╋',
        _ => ' '
    };

    private static int ResolvePaired(LineConnections value) => value switch
    {
        LineConnections.None => ' ',
        LineConnections.Up => '╵',
        LineConnections.Right => '╶',
        LineConnections.Down => '╷',
        LineConnections.Left => '╴',
        LineConnections.Left | LineConnections.Right => '═',
        LineConnections.Up | LineConnections.Down => '║',
        LineConnections.Right | LineConnections.Down => '╔',
        LineConnections.Down | LineConnections.Left => '╗',
        LineConnections.Up | LineConnections.Right => '╚',
        LineConnections.Up | LineConnections.Left => '╝',
        LineConnections.Right | LineConnections.Down | LineConnections.Left => '╦',
        LineConnections.Up | LineConnections.Down | LineConnections.Left => '╣',
        LineConnections.Up | LineConnections.Right | LineConnections.Left => '╩',
        LineConnections.Up | LineConnections.Right | LineConnections.Down => '╠',
        LineConnections.Up | LineConnections.Right | LineConnections.Down | LineConnections.Left => '╬',
        _ => ' '
    };

    private static LineConnections DecodeLight(int value) => value switch
    {
        '╵' => LineConnections.Up,
        '╶' => LineConnections.Right,
        '╷' => LineConnections.Down,
        '╴' => LineConnections.Left,
        '─' => LineConnections.Left | LineConnections.Right,
        '│' => LineConnections.Up | LineConnections.Down,
        '┌' or '╭' => LineConnections.Right | LineConnections.Down,
        '┐' or '╮' => LineConnections.Down | LineConnections.Left,
        '└' or '╰' => LineConnections.Up | LineConnections.Right,
        '┘' or '╯' => LineConnections.Up | LineConnections.Left,
        '┬' => LineConnections.Right | LineConnections.Down | LineConnections.Left,
        '┤' => LineConnections.Up | LineConnections.Down | LineConnections.Left,
        '┴' => LineConnections.Up | LineConnections.Right | LineConnections.Left,
        '├' => LineConnections.Up | LineConnections.Right | LineConnections.Down,
        '┼' => LineConnections.Up | LineConnections.Right | LineConnections.Down | LineConnections.Left,
        _ => LineConnections.None
    };

    private static LineConnections DecodeHeavy(int value) => value switch
    {
        '╹' => LineConnections.Up,
        '╺' => LineConnections.Right,
        '╻' => LineConnections.Down,
        '╸' => LineConnections.Left,
        '━' => LineConnections.Left | LineConnections.Right,
        '┃' => LineConnections.Up | LineConnections.Down,
        '┏' => LineConnections.Right | LineConnections.Down,
        '┓' => LineConnections.Down | LineConnections.Left,
        '┗' => LineConnections.Up | LineConnections.Right,
        '┛' => LineConnections.Up | LineConnections.Left,
        '┳' => LineConnections.Right | LineConnections.Down | LineConnections.Left,
        '┫' => LineConnections.Up | LineConnections.Down | LineConnections.Left,
        '┻' => LineConnections.Up | LineConnections.Right | LineConnections.Left,
        '┣' => LineConnections.Up | LineConnections.Right | LineConnections.Down,
        '╋' => LineConnections.Up | LineConnections.Right | LineConnections.Down | LineConnections.Left,
        _ => LineConnections.None
    };

    private static LineConnections DecodePaired(int value) => value switch
    {
        '═' => LineConnections.Left | LineConnections.Right,
        '║' => LineConnections.Up | LineConnections.Down,
        '╔' => LineConnections.Right | LineConnections.Down,
        '╗' => LineConnections.Down | LineConnections.Left,
        '╚' => LineConnections.Up | LineConnections.Right,
        '╝' => LineConnections.Up | LineConnections.Left,
        '╦' => LineConnections.Right | LineConnections.Down | LineConnections.Left,
        '╣' => LineConnections.Up | LineConnections.Down | LineConnections.Left,
        '╩' => LineConnections.Up | LineConnections.Right | LineConnections.Left,
        '╠' => LineConnections.Up | LineConnections.Right | LineConnections.Down,
        '╬' => LineConnections.Up | LineConnections.Right | LineConnections.Down | LineConnections.Left,
        _ => LineConnections.None
    };

    private static Rune ResolveSolid(Topology value) =>
        value.Line.HasRoundedCorners && TryResolveRounded(value.Connections, out var rounded)
            ? new Rune(rounded)
            : new Rune(value.Line.Weight switch
            {
                LineWeight.Light => ResolveLight(value.Connections),
                LineWeight.Heavy => ResolveHeavy(value.Connections),
                LineWeight.Paired => ResolvePaired(value.Connections),
                _ => throw new UnreachableException()
            });
}
