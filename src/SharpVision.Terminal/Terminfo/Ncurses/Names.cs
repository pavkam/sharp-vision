// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

using Input;

/// <summary>Owns the finite, case-sensitive ncurses capability allowlist consumed by SharpVision.</summary>
internal static class Names
{
    /// <summary>Gets the exact canonical Boolean identifiers.</summary>
    public static IReadOnlyList<string> Booleans { get; } =
    [
        "am", "bce", "ccc", "gn", "hc", "km", "mir", "msgr", "npc", "xenl", "xon",
        "AX", "Tc", "XF", "XT"
    ];

    /// <summary>Gets the exact canonical numeric identifiers.</summary>
    public static IReadOnlyList<string> Numbers { get; } =
    [
        "cols", "lines", "colors", "pairs", "ncv", "U8"
    ];

    /// <summary>Gets the exact canonical string identifiers.</summary>
    public static IReadOnlyList<string> Strings { get; } = CreateStrings();

    /// <summary>Gets whether an exact identifier is an input key byte sequence rather than an output program.</summary>
    public static bool IsKey(string name) =>
        name is "kbs" or "kcbt" or "kent" or "kcuu1" or "kcud1" or "kcub1" or "kcuf1" or
        "khome" or "kend" or "kich1" or "kdch1" or "kpp" or "knp" or "kbeg" or "ka1" or
        "ka3" or "kb2" or "kc1" or "kc3" || name.StartsWith("kf", StringComparison.Ordinal) ||
        IsModifiedKey(name);

    /// <summary>Maps one exact retained input identifier to its typed logical key and modifiers.</summary>
    public static bool TryMapKey(string name, out Code code, out Modifiers modifiers)
    {
        modifiers = Modifiers.None;
        code = name switch
        {
            "kbs" => Code.Backspace,
            "kcbt" => Code.Tab,
            "kent" => Code.Enter,
            "kcuu1" => Code.Up,
            "kcud1" => Code.Down,
            "kcub1" => Code.Left,
            "kcuf1" => Code.Right,
            "khome" => Code.Home,
            "kend" => Code.End,
            "kich1" => Code.Insert,
            "kdch1" => Code.Delete,
            "kpp" => Code.PageUp,
            "knp" => Code.PageDown,
            "kbeg" or "kb2" => Code.Begin,
            "ka1" => Code.Home,
            "ka3" => Code.PageUp,
            "kc1" => Code.End,
            "kc3" => Code.PageDown,
            _ => Code.Unknown
        };

        if (code != Code.Unknown)
        {
            modifiers = name == "kcbt" ? Modifiers.Shift : Modifiers.None;
            return true;
        }

        if (name.StartsWith("kf", StringComparison.Ordinal) &&
            int.TryParse(name.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var function) &&
            FunctionKeys.TryGet(function, out code))
        {
            return true;
        }

        var suffixIndex = name.Length - 1;
        var suffix = suffixIndex >= 0 && name[suffixIndex] is >= '3' and <= '8'
            ? name[suffixIndex] - '0'
            : 0;
        var baseName = suffix == 0 ? name : name[..suffixIndex];

        code = baseName switch
        {
            "kUP" => Code.Up,
            "kDN" => Code.Down,
            "kLFT" => Code.Left,
            "kRIT" => Code.Right,
            "kHOM" => Code.Home,
            "kEND" => Code.End,
            "kIC" => Code.Insert,
            "kDC" => Code.Delete,
            "kNXT" => Code.PageDown,
            "kPRV" => Code.PageUp,
            _ => Code.Unknown
        };

        if (code == Code.Unknown)
        {
            return false;
        }

        modifiers = suffix switch
        {
            3 => Modifiers.Alt,
            4 => Modifiers.Shift | Modifiers.Alt,
            5 => Modifiers.Control,
            6 => Modifiers.Shift | Modifiers.Control,
            7 => Modifiers.Alt | Modifiers.Control,
            8 => Modifiers.Shift | Modifiers.Alt | Modifiers.Control,
            _ => Modifiers.Shift
        };
        return true;
    }

    private static ReadOnlyCollection<string> CreateStrings()
    {
        var values = new List<string>
        {
            "bel", "clear", "E3", "cup", "home", "cud1", "cuu1", "cub1", "cuf1", "cud", "cuu",
            "cub", "cuf", "hpa", "vpa", "csr", "ind", "ri", "indn", "rin", "ed", "el", "el1",
            "ech", "ich", "ich1", "dch", "dch1", "il", "il1", "dl", "dl1", "sc", "rc", "civis",
            "cnorm", "cvvis", "smcup", "rmcup", "smkx", "rmkx", "sgr0", "sgr", "bold", "dim",
            "sitm", "ritm", "smul", "rmul", "rev", "smso", "rmso", "blink", "invis", "smxx", "rmxx",
            "setaf", "setab", "setrgbf", "setrgbb", "setdf", "setdb", "op", "oc", "initc", "Smulx",
            "Setulc", "setal", "Smol", "Rmol",
            "kbs", "kcbt", "kent", "kcuu1", "kcud1", "kcub1", "kcuf1", "khome", "kend", "kich1",
            "kdch1", "kpp", "knp", "kbeg", "ka1", "ka3", "kb2", "kc1", "kc3", "kmous", "XM", "xm",
            "fe", "fd", "kxIN", "kxOUT", "BE", "BD", "PS", "PE", "Ms", "Ss", "Se", "Cs", "Cr", "TS", "fsl",
            "RV", "rv", "XR", "xr"
        };

        for (var function = 1; function <= 63; function++)
        {
            values.Add($"kf{function.ToString(CultureInfo.InvariantCulture)}");
        }

        foreach (var name in new[] { "kUP", "kDN", "kLFT", "kRIT", "kHOM", "kEND", "kIC", "kDC", "kNXT", "kPRV" })
        {
            values.Add(name);

            for (var modifier = 3; modifier <= 8; modifier++)
            {
                values.Add($"{name}{modifier.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        return Array.AsReadOnly(values.ToArray());
    }

    private static bool IsModifiedKey(string name)
    {
        foreach (var prefix in new[] { "kUP", "kDN", "kLFT", "kRIT", "kHOM", "kEND", "kIC", "kDC", "kNXT", "kPRV" })
        {
            if (name == prefix ||
                (name.StartsWith(prefix, StringComparison.Ordinal) &&
                 name.Length == prefix.Length + 1 &&
                 name[^1] is >= '3' and <= '8'))
            {
                return true;
            }
        }

        return false;
    }
}
