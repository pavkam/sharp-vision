// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Retains one control's fully resolved prospective inherited context.</summary>
internal readonly struct ControlContextTransition
{
    /// <summary>Initializes one cache-neutral prospective context entry.</summary>
    internal ControlContextTransition(
        ControlBase control,
        Dispatcher? dispatcher,
        UnicodePolicy cellPolicy,
        FocusManager? focusOwner,
        PointerManager? captureOwner,
        ModalityManager? modalityOwner,
        Theme? theme,
        bool commitsContext,
        ThemeTransition? themeTransition)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(cellPolicy);

        Control = control;
        Dispatcher = dispatcher;
        CellPolicy = cellPolicy;
        FocusOwner = focusOwner;
        CaptureOwner = captureOwner;
        ModalityOwner = modalityOwner;
        Theme = theme;
        CommitsContext = commitsContext;
        ThemeTransition = themeTransition;
    }

    /// <summary>Gets the control receiving this prospective context.</summary>
    internal ControlBase Control { get; }

    /// <summary>Gets the prospective dispatcher, or null.</summary>
    internal Dispatcher? Dispatcher { get; }

    /// <summary>Gets the prospective Unicode cell policy.</summary>
    internal UnicodePolicy CellPolicy { get; }

    /// <summary>Gets the prospective focus manager, or null.</summary>
    internal FocusManager? FocusOwner { get; }

    /// <summary>Gets the prospective pointer manager, or null.</summary>
    internal PointerManager? CaptureOwner { get; }

    /// <summary>Gets the prospective modality manager, or null.</summary>
    internal ModalityManager? ModalityOwner { get; }

    /// <summary>Gets the prospective inherited Theme, or null.</summary>
    internal Theme? Theme { get; }

    /// <summary>Gets whether inherited context fields commit for this entry.</summary>
    internal bool CommitsContext { get; }

    /// <summary>Gets the fully resolved Theme transition, or null when identity is unchanged.</summary>
    internal ThemeTransition? ThemeTransition { get; }
}
