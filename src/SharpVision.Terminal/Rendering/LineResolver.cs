using System.Diagnostics;
using System.Text;

namespace SharpVision.Terminal.Rendering;

/// <summary>Maps line-cell topology to and from Unicode or ASCII glyphs.</summary>
internal static class LineResolver
{
    /// <summary>Combines two topology values commutatively.</summary>
    /// <param name="left">The first topology.</param>
    /// <param name="right">The second topology.</param>
    /// <returns>The deterministic combined topology.</returns>
    internal static Topology Merge(Topology left, Topology right)
    {
        var connections = left.Connections | right.Connections;
        var weight = (LineWeight) Math.Max((int) left.Line.Weight, (int) right.Line.Weight);
        var straight = connections is (Connections.Left | Connections.Right) or
            (Connections.Up | Connections.Down);
        var pattern = straight && left.Line.Pattern == right.Line.Pattern
            ? left.Line.Pattern
            : LinePattern.Solid;
        var rounded = left.Line.HasRoundedCorners && right.Line.HasRoundedCorners;
        var ascii = left.Line.IsAscii && right.Line.IsAscii;
        return new Topology(connections, new LineStyle(weight, pattern, rounded, ascii));
    }

    /// <summary>Resolves a topology to its exact or safely degraded Rune.</summary>
    /// <param name="value">The topology to resolve.</param>
    /// <returns>A printable one-cell Rune.</returns>
    internal static Rune Resolve(Topology value)
    {
        return value.Line.IsAscii
            ? new Rune(ResolveAscii(value.Connections))
            : TryResolvePattern(value, out var patterned)
                ? new Rune(patterned)
                : ResolveSolid(value);
    }

    /// <summary>Attempts to decode one previously produced line Rune.</summary>
    /// <param name="value">The candidate Rune.</param>
    /// <param name="topology">The decoded topology when recognized.</param>
    /// <returns>Whether the Rune belongs to a supported line family.</returns>
    internal static bool TryDecode(Rune value, out Topology topology)
    {
        var decoded = value.Value switch
        {
            '-' => new Topology(Connections.Left | Connections.Right, LineStyle.Ascii),
            '|' => new Topology(Connections.Up | Connections.Down, LineStyle.Ascii),
            '+' => new Topology(
                Connections.Up | Connections.Right | Connections.Down | Connections.Left,
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
                Connections.Left | Connections.Right,
                new LineStyle(LineWeight.Light, LinePattern.DoubleDash)),
            '╎' => new Topology(
                Connections.Up | Connections.Down,
                new LineStyle(LineWeight.Light, LinePattern.DoubleDash)),
            '┄' => new Topology(
                Connections.Left | Connections.Right,
                new LineStyle(LineWeight.Light, LinePattern.TripleDash)),
            '┆' => new Topology(
                Connections.Up | Connections.Down,
                new LineStyle(LineWeight.Light, LinePattern.TripleDash)),
            '┈' => new Topology(
                Connections.Left | Connections.Right,
                new LineStyle(LineWeight.Light, LinePattern.QuadrupleDash)),
            '┊' => new Topology(
                Connections.Up | Connections.Down,
                new LineStyle(LineWeight.Light, LinePattern.QuadrupleDash)),
            '╍' => new Topology(
                Connections.Left | Connections.Right,
                new LineStyle(LineWeight.Heavy, LinePattern.DoubleDash)),
            '╏' => new Topology(
                Connections.Up | Connections.Down,
                new LineStyle(LineWeight.Heavy, LinePattern.DoubleDash)),
            '┅' => new Topology(
                Connections.Left | Connections.Right,
                new LineStyle(LineWeight.Heavy, LinePattern.TripleDash)),
            '┇' => new Topology(
                Connections.Up | Connections.Down,
                new LineStyle(LineWeight.Heavy, LinePattern.TripleDash)),
            '┉' => new Topology(
                Connections.Left | Connections.Right,
                new LineStyle(LineWeight.Heavy, LinePattern.QuadrupleDash)),
            '┋' => new Topology(
                Connections.Up | Connections.Down,
                new LineStyle(LineWeight.Heavy, LinePattern.QuadrupleDash)),
            _ => default,
        };

        topology = decoded;
        return decoded.Connections != Connections.None;
    }

    private static int ResolveAscii(Connections value) => value switch
    {
        Connections.None => ' ',
        Connections.Left or Connections.Right or (Connections.Left | Connections.Right) => '-',
        Connections.Up or Connections.Down or (Connections.Up | Connections.Down) => '|',
        _ => '+',
    };

    private static bool TryResolvePattern(Topology value, out int rune)
    {
        rune = (value.Line.Weight, value.Line.Pattern, value.Connections) switch
        {
            (LineWeight.Light, LinePattern.DoubleDash, Connections.Left | Connections.Right) => '╌',
            (LineWeight.Light, LinePattern.DoubleDash, Connections.Up | Connections.Down) => '╎',
            (LineWeight.Light, LinePattern.TripleDash, Connections.Left | Connections.Right) => '┄',
            (LineWeight.Light, LinePattern.TripleDash, Connections.Up | Connections.Down) => '┆',
            (LineWeight.Light, LinePattern.QuadrupleDash, Connections.Left | Connections.Right) => '┈',
            (LineWeight.Light, LinePattern.QuadrupleDash, Connections.Up | Connections.Down) => '┊',
            (LineWeight.Heavy, LinePattern.DoubleDash, Connections.Left | Connections.Right) => '╍',
            (LineWeight.Heavy, LinePattern.DoubleDash, Connections.Up | Connections.Down) => '╏',
            (LineWeight.Heavy, LinePattern.TripleDash, Connections.Left | Connections.Right) => '┅',
            (LineWeight.Heavy, LinePattern.TripleDash, Connections.Up | Connections.Down) => '┇',
            (LineWeight.Heavy, LinePattern.QuadrupleDash, Connections.Left | Connections.Right) => '┉',
            (LineWeight.Heavy, LinePattern.QuadrupleDash, Connections.Up | Connections.Down) => '┋',
            _ => 0,
        };
        return rune != 0;
    }

    private static bool TryResolveRounded(Connections value, out int rune)
    {
        rune = value switch
        {
            Connections.None => 0,
            Connections.Right | Connections.Down => '╭',
            Connections.Down | Connections.Left => '╮',
            Connections.Up | Connections.Right => '╰',
            Connections.Up | Connections.Left => '╯',
            Connections.Up or Connections.Right or Connections.Down or Connections.Left => 0,
            _ => 0,
        };
        return rune != 0;
    }

    private static int ResolveLight(Connections value) => value switch
    {
        Connections.None => ' ',
        Connections.Up => '╵',
        Connections.Right => '╶',
        Connections.Down => '╷',
        Connections.Left => '╴',
        Connections.Left | Connections.Right => '─',
        Connections.Up | Connections.Down => '│',
        Connections.Right | Connections.Down => '┌',
        Connections.Down | Connections.Left => '┐',
        Connections.Up | Connections.Right => '└',
        Connections.Up | Connections.Left => '┘',
        Connections.Right | Connections.Down | Connections.Left => '┬',
        Connections.Up | Connections.Down | Connections.Left => '┤',
        Connections.Up | Connections.Right | Connections.Left => '┴',
        Connections.Up | Connections.Right | Connections.Down => '├',
        Connections.Up | Connections.Right | Connections.Down | Connections.Left => '┼',
        _ => ' ',
    };

    private static int ResolveHeavy(Connections value) => value switch
    {
        Connections.None => ' ',
        Connections.Up => '╹',
        Connections.Right => '╺',
        Connections.Down => '╻',
        Connections.Left => '╸',
        Connections.Left | Connections.Right => '━',
        Connections.Up | Connections.Down => '┃',
        Connections.Right | Connections.Down => '┏',
        Connections.Down | Connections.Left => '┓',
        Connections.Up | Connections.Right => '┗',
        Connections.Up | Connections.Left => '┛',
        Connections.Right | Connections.Down | Connections.Left => '┳',
        Connections.Up | Connections.Down | Connections.Left => '┫',
        Connections.Up | Connections.Right | Connections.Left => '┻',
        Connections.Up | Connections.Right | Connections.Down => '┣',
        Connections.Up | Connections.Right | Connections.Down | Connections.Left => '╋',
        _ => ' ',
    };

    private static int ResolvePaired(Connections value) => value switch
    {
        Connections.None => ' ',
        Connections.Up => '╵',
        Connections.Right => '╶',
        Connections.Down => '╷',
        Connections.Left => '╴',
        Connections.Left | Connections.Right => '═',
        Connections.Up | Connections.Down => '║',
        Connections.Right | Connections.Down => '╔',
        Connections.Down | Connections.Left => '╗',
        Connections.Up | Connections.Right => '╚',
        Connections.Up | Connections.Left => '╝',
        Connections.Right | Connections.Down | Connections.Left => '╦',
        Connections.Up | Connections.Down | Connections.Left => '╣',
        Connections.Up | Connections.Right | Connections.Left => '╩',
        Connections.Up | Connections.Right | Connections.Down => '╠',
        Connections.Up | Connections.Right | Connections.Down | Connections.Left => '╬',
        _ => ' ',
    };

    private static Connections DecodeLight(int value) => value switch
    {
        '╵' => Connections.Up,
        '╶' => Connections.Right,
        '╷' => Connections.Down,
        '╴' => Connections.Left,
        '─' => Connections.Left | Connections.Right,
        '│' => Connections.Up | Connections.Down,
        '┌' or '╭' => Connections.Right | Connections.Down,
        '┐' or '╮' => Connections.Down | Connections.Left,
        '└' or '╰' => Connections.Up | Connections.Right,
        '┘' or '╯' => Connections.Up | Connections.Left,
        '┬' => Connections.Right | Connections.Down | Connections.Left,
        '┤' => Connections.Up | Connections.Down | Connections.Left,
        '┴' => Connections.Up | Connections.Right | Connections.Left,
        '├' => Connections.Up | Connections.Right | Connections.Down,
        '┼' => Connections.Up | Connections.Right | Connections.Down | Connections.Left,
        _ => Connections.None,
    };

    private static Connections DecodeHeavy(int value) => value switch
    {
        '╹' => Connections.Up,
        '╺' => Connections.Right,
        '╻' => Connections.Down,
        '╸' => Connections.Left,
        '━' => Connections.Left | Connections.Right,
        '┃' => Connections.Up | Connections.Down,
        '┏' => Connections.Right | Connections.Down,
        '┓' => Connections.Down | Connections.Left,
        '┗' => Connections.Up | Connections.Right,
        '┛' => Connections.Up | Connections.Left,
        '┳' => Connections.Right | Connections.Down | Connections.Left,
        '┫' => Connections.Up | Connections.Down | Connections.Left,
        '┻' => Connections.Up | Connections.Right | Connections.Left,
        '┣' => Connections.Up | Connections.Right | Connections.Down,
        '╋' => Connections.Up | Connections.Right | Connections.Down | Connections.Left,
        _ => Connections.None,
    };

    private static Connections DecodePaired(int value) => value switch
    {
        '═' => Connections.Left | Connections.Right,
        '║' => Connections.Up | Connections.Down,
        '╔' => Connections.Right | Connections.Down,
        '╗' => Connections.Down | Connections.Left,
        '╚' => Connections.Up | Connections.Right,
        '╝' => Connections.Up | Connections.Left,
        '╦' => Connections.Right | Connections.Down | Connections.Left,
        '╣' => Connections.Up | Connections.Down | Connections.Left,
        '╩' => Connections.Up | Connections.Right | Connections.Left,
        '╠' => Connections.Up | Connections.Right | Connections.Down,
        '╬' => Connections.Up | Connections.Right | Connections.Down | Connections.Left,
        _ => Connections.None,
    };

    private static Rune ResolveSolid(Topology value) =>
        value.Line.HasRoundedCorners && TryResolveRounded(value.Connections, out var rounded)
            ? new Rune(rounded)
            : new Rune(value.Line.Weight switch
            {
                LineWeight.Light => ResolveLight(value.Connections),
                LineWeight.Heavy => ResolveHeavy(value.Connections),
                LineWeight.Paired => ResolvePaired(value.Connections),
                _ => throw new UnreachableException(),
            });
}
