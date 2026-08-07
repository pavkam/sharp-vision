// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies every concrete shipped control is sealed, per the documented contract in
/// docs/concepts/custom-components.md: third parties derive from the abstract roles or compose
/// sealed controls, never depend on internal subclassing of a shipped concrete control.</summary>
public sealed class SealedControlsContractTests
{
    // Unsealed only because the library itself subclasses them internally: Flyout/Tooltip : Popup;
    // Dialog<TResult> : Window (and every concrete dialog type derives from Dialog<TResult> in
    // turn) — the extensibility seam WindowTests.cs already asserts explicitly
    // (type.IsSealed.ShouldBeFalse()). ContextMenu is not a ControlBase and TextInputContextMenu
    // : ContextMenu is unsealed for the same internal-subclassing reason, but neither is scanned here
    // since the predicate below is scoped to ControlBase-assignable types.
    private static readonly HashSet<Type> _documentedExceptions =
        [typeof(Popup), typeof(Window)];

    private static readonly string[] _controlNamespaces =
    [
        "SharpVision.Controls",
        "SharpVision.Menus",
        "SharpVision.Navigation",
        "SharpVision.Dialogs",
        "SharpVision.Popups",
        "SharpVision.Windows"
    ];

    /// <summary>Verifies every concrete control type in the public control namespaces is sealed,
    /// except the documented internally-subclassed exceptions — Slider was the one control
    /// the library forgot to seal.</summary>
    [Fact]
    public void ConcreteControls_WhenInspected_AreSealedExceptDocumentedExceptions()
    {
        var assembly = typeof(ControlBase).Assembly;

        var violations = assembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                type.IsPublic &&
                !type.IsAbstract &&
                !type.IsSealed &&
                typeof(ControlBase).IsAssignableFrom(type) &&
                !_documentedExceptions.Contains(type) &&
                _controlNamespaces.Any(value =>
                    type.Namespace == value ||
                    (type.Namespace?.StartsWith(value + ".", StringComparison.Ordinal) ?? false)))
            .ToList();

        violations.ShouldBeEmpty();
    }
}
