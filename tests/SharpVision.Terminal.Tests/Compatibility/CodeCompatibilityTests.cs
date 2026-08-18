// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Compatibility;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;

/// <summary>Freezes the shipped public logical-key numeric contract.</summary>
public sealed class CodeCompatibilityTests
{
    /// <summary>Verifies every pre-description key retains its original numeric value.</summary>
    [Fact]
    public void Code_WhenReadByConsumer_PreservesShippedNumericValues()
    {
        Code[] shipped =
        [
            Code.Unknown, Code.Character, Code.Escape, Code.Enter, Code.Tab,
            Code.Backspace, Code.Up, Code.Down, Code.Left, Code.Right, Code.Home,
            Code.End, Code.Insert, Code.Delete, Code.PageUp, Code.PageDown,
            Code.F1, Code.F2, Code.F3, Code.F4, Code.F5, Code.F6, Code.F7,
            Code.F8, Code.F9, Code.F10, Code.F11, Code.F12, Code.F13, Code.F14,
            Code.F15, Code.F16, Code.F17, Code.F18, Code.F19, Code.F20,
            Code.F21, Code.F22, Code.F23, Code.F24, Code.F25, Code.F26,
            Code.F27, Code.F28, Code.F29, Code.F30, Code.F31, Code.F32,
            Code.F33, Code.F34, Code.F35, Code.CapsLock, Code.ScrollLock,
            Code.NumLock, Code.PrintScreen, Code.Pause, Code.Menu
        ];

        for (var expected = 0; expected < shipped.Length; expected++)
        {
            ((int) shipped[expected]).ShouldBe(expected, shipped[expected].ToString());
        }

        ((int) Code.Begin).ShouldBe(57);
        ((int) Code.F36).ShouldBe(58);
        ((int) Code.F63).ShouldBe(85);
    }

    /// <summary>Verifies description diagnostics append without renumbering shipped values.</summary>
    [Fact]
    public void DescriptionDiagnosticCode_WhenReadByConsumer_PreservesShippedNumericValues()
    {
        DescriptionDiagnosticCode[] shipped =
        [
            DescriptionDiagnosticCode.WrongType,
            DescriptionDiagnosticCode.InvalidProgram,
            DescriptionDiagnosticCode.UnsupportedPadding,
            DescriptionDiagnosticCode.MissingRequired,
            DescriptionDiagnosticCode.NativeFailure,
            DescriptionDiagnosticCode.CleanupFailure,
            DescriptionDiagnosticCode.TermcapLimit,
            DescriptionDiagnosticCode.ConflictingKey,
            DescriptionDiagnosticCode.MissingOrGeneric,
            DescriptionDiagnosticCode.EnvironmentLimit,
            DescriptionDiagnosticCode.AnsiFallback,
            DescriptionDiagnosticCode.DescriptionLimit
        ];

        for (var expected = 0; expected < shipped.Length; expected++)
        {
            ((int) shipped[expected]).ShouldBe(expected, shipped[expected].ToString());
        }

        ((int) DescriptionDiagnosticCode.InvalidKey).ShouldBe(12);
    }
}
