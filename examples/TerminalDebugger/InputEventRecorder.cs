// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Copies public decoded application events into one bounded diagnostic log.</summary>
internal sealed class InputEventRecorder: IDisposable
{
    private readonly DiagnosticEventLog _log;
    private readonly Action<TerminalProtocol, VerificationState, string> _verify;
    private readonly List<IDisposable> _registrations = [];
    private Application? _application;

    /// <summary>Initializes one recorder.</summary>
    /// <param name="log">The non-null destination log.</param>
    /// <param name="verify">The non-null passive-verification callback.</param>
    internal InputEventRecorder(
        DiagnosticEventLog log,
        Action<TerminalProtocol, VerificationState, string> verify)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(verify);
        _log = log;
        _verify = verify;
    }

    /// <summary>Attaches the recorder to one running application and routed-input root.</summary>
    /// <param name="application">The non-null application.</param>
    /// <param name="root">The non-null routed-input root.</param>
    /// <exception cref="InvalidOperationException">The recorder is already attached.</exception>
    internal void Attach(Application application, ControlBase root)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(root);

        if (_application is not null)
        {
            throw new InvalidOperationException("The input event recorder is already attached.");
        }

        _application = application;
        _registrations.Add(root.AddHandler(Events.Key, OnKey, handledEventsToo: true));
        _registrations.Add(root.AddHandler(Events.Text, OnText, handledEventsToo: true));
        _registrations.Add(root.AddHandler(Events.Pointer, OnPointer, handledEventsToo: true));
        _registrations.Add(root.AddHandler(Events.Paste, OnPaste, handledEventsToo: true));
        _registrations.Add(root.AddHandler(Events.TerminalFocusChanged, OnFocus, handledEventsToo: true));
        application.Resize += OnResize;
        application.Terminal.Clipboard.ClipboardPasteReceived += OnClipboardPaste;
        application.Terminal.Clipboard.KittyClipboardReplyReceived += OnClipboardReply;
    }

    private void OnKey(object? sender, KeyEventArgs eventArgs)
    {
        if (_log.IsPaused || eventArgs.Phase != RoutingPhase.Bubble)
        {
            return;
        }

        var stroke = eventArgs.Stroke;
        var character = stroke.Character?.ToString() ?? "—";
        _log.Add(
            DiagnosticEventKind.Key,
            $"{stroke.Action} {stroke.Code} {character}",
            "SharpVision decoded one logical key transition. Key and text input are separate events.",
            Fields(
                ("Action", stroke.Action.ToString()),
                ("Code", stroke.Code.ToString()),
                ("Character", DiagnosticTextFormatter.EscapeText(character)),
                ("Modifiers", stroke.Modifiers.ToString()),
                ("Native code", stroke.NativeCode.ToString(CultureInfo.InvariantCulture)),
                ("Shifted identity", stroke.Shifted?.ToString() ?? "—"),
                ("Base-layout identity", stroke.BaseLayout?.ToString() ?? "—"),
                ("Route phase", eventArgs.Phase.ToString()),
                ("Handled", eventArgs.IsHandled.ToString(CultureInfo.InvariantCulture)),
                ("Source", eventArgs.Source?.GetType().Name ?? "—")));

        if (stroke.Action == KeyAction.Release)
        {
            _verify(
                TerminalProtocol.KittyKeyboard,
                VerificationState.Observed,
                "A decoded key-release transition was received; Kitty keyboard is the implemented protocol that provides releases.");
        }
    }

    private void OnText(object? sender, TextEventArgs eventArgs)
    {
        if (_log.IsPaused || eventArgs.Phase != RoutingPhase.Bubble)
        {
            return;
        }

        var rune = eventArgs.Text.Value;
        _log.Add(
            DiagnosticEventKind.Text,
            $"U+{rune.Value:X4} {DiagnosticTextFormatter.EscapeText(rune.ToString())}",
            "This is the decoded printable Unicode scalar delivered independently from its key transition.",
            Fields(
                ("Unicode scalar", $"U+{rune.Value:X4}"),
                ("Text", DiagnosticTextFormatter.EscapeText(rune.ToString())),
                ("UTF-8 bytes", DiagnosticTextFormatter.FormatBytes(Encoding.UTF8.GetBytes(rune.ToString()))),
                ("Route phase", eventArgs.Phase.ToString()),
                ("Handled", eventArgs.IsHandled.ToString(CultureInfo.InvariantCulture))));
    }

    private void OnPointer(object? sender, PointerEventArgs eventArgs)
    {
        if (_log.IsPaused || eventArgs.Phase != RoutingPhase.Bubble)
        {
            return;
        }

        var pointer = eventArgs.Pointer;
        _log.Add(
            DiagnosticEventKind.Pointer,
            $"{pointer.Action} · {FormatPoint(pointer.Cells)} cells · {pointer.Buttons}",
            "The pointer event preserves cell coordinates and optional pixel coordinates from the terminal decoder.",
            Fields(
                ("Action", pointer.Action.ToString()),
                ("Buttons", pointer.Buttons.ToString()),
                ("Cell position", FormatPoint(pointer.Cells)),
                ("Local cell position", FormatPoint(eventArgs.LocalCells)),
                ("Pixel position", FormatPoint(pointer.Pixels)),
                ("Cell position inferred", pointer.CellPositionInferred.ToString(CultureInfo.InvariantCulture)),
                ("Motion explicitly reported", pointer.MotionReported.ToString(CultureInfo.InvariantCulture)),
                ("Wheel delta", $"x={pointer.WheelX}, y={pointer.WheelY}"),
                ("Click count", eventArgs.ClickCount.ToString(CultureInfo.InvariantCulture)),
                ("Modifiers", pointer.Modifiers.ToString()),
                ("Route phase", eventArgs.Phase.ToString()),
                ("Handled", eventArgs.IsHandled.ToString(CultureInfo.InvariantCulture))));

        if (pointer.Cells is not null)
        {
            _verify(TerminalProtocol.CellMouse, VerificationState.Observed, "A pointer event supplied terminal-cell coordinates.");
        }

        if (pointer.Pixels is not null)
        {
            _verify(TerminalProtocol.PixelMouse, VerificationState.Observed, "A pointer event supplied terminal-pixel coordinates.");
        }
    }

    private void OnPaste(object? sender, PasteEventArgs eventArgs)
    {
        if (_log.IsPaused || eventArgs.Phase != RoutingPhase.Bubble)
        {
            return;
        }

        var bytes = eventArgs.Paste.Utf8.Span;
        var decoded = Encoding.UTF8.GetString(bytes);
        _log.Add(
            DiagnosticEventKind.Paste,
            $"{bytes.Length} UTF-8 bytes",
            "Bracketed paste kept the payload together instead of replaying it as individual keys.",
            Fields(
                ("Byte count", bytes.Length.ToString(CultureInfo.InvariantCulture)),
                ("Rune count", decoded.EnumerateRunes().Count().ToString(CultureInfo.InvariantCulture)),
                ("Escaped text", DiagnosticTextFormatter.EscapeText(decoded)),
                ("Bytes", DiagnosticTextFormatter.FormatBytes(bytes)),
                ("Route phase", eventArgs.Phase.ToString()),
                ("Handled", eventArgs.IsHandled.ToString(CultureInfo.InvariantCulture))));
        _verify(TerminalProtocol.BracketedPaste, VerificationState.Observed, "A complete bounded bracketed-paste event was received.");
    }

    private void OnFocus(object? sender, TerminalFocusEventArgs eventArgs)
    {
        if (_log.IsPaused || eventArgs.Phase != RoutingPhase.Bubble)
        {
            return;
        }

        var state = eventArgs.Focus.Gained ? "gained" : "lost";
        _log.Add(
            DiagnosticEventKind.Focus,
            $"Terminal focus {state}",
            "The terminal emitted an explicit focus transition; this is distinct from focus between SharpVision controls.",
            Fields(
                ("State", state),
                ("Route phase", eventArgs.Phase.ToString()),
                ("Handled", eventArgs.IsHandled.ToString(CultureInfo.InvariantCulture))));
        _verify(TerminalProtocol.FocusReporting, VerificationState.Observed, $"A terminal focus-{state} event was received.");
    }

    private void OnResize(object? sender, ResizeEventArgs eventArgs)
    {
        if (_log.IsPaused)
        {
            return;
        }

        var dimensions = eventArgs.Dimensions;
        _log.Add(
            DiagnosticEventKind.Resize,
            $"{dimensions.Cells.Width}×{dimensions.Cells.Height} cells",
            "Resize is published after the newest size commits and root layout completes.",
            Fields(
                ("Cells", $"{dimensions.Cells.Width}×{dimensions.Cells.Height}"),
                ("Pixels", dimensions.Pixels is { } pixels ? $"{pixels.Width}×{pixels.Height}" : "not reported"),
                ("Cell metrics", dimensions.CellMetrics is { } metrics ? metrics.ToString() : "not derivable"),
                ("Suspended", dimensions.Suspended.ToString(CultureInfo.InvariantCulture))));
    }

    private void OnClipboardPaste(object? sender, ClipboardPasteEventArgs eventArgs)
    {
        if (_log.IsPaused)
        {
            return;
        }

        _log.Add(
            DiagnosticEventKind.Clipboard,
            $"Kitty paste notification · {eventArgs.Selection}",
            "The terminal advertised clipboard MIME types. The one-time credential is deliberately redacted.",
            Fields(
                ("Selection", eventArgs.Selection.ToString()),
                ("MIME types", string.Join(", ", eventArgs.MimeTypes)),
                ("One-time password", $"[redacted, {eventArgs.Password.Length} bytes]")));
    }

    private void OnClipboardReply(object? sender, KittyClipboardReplyEventArgs eventArgs)
    {
        if (_log.IsPaused)
        {
            eventArgs.KittyResult?.Dispose();
            return;
        }

        var fields = new List<DiagnosticField>
        {
            new("Selection", eventArgs.Selection.ToString()),
            new("Succeeded", eventArgs.IsSucceeded.ToString(CultureInfo.InvariantCulture)),
            new("Failure", eventArgs.Failure.ToString()),
            new("Diagnostic", eventArgs.Diagnostic?.ToString() ?? "—")
        };

        if (eventArgs.Text is { } text)
        {
            fields.Add(new DiagnosticField("OSC 52 payload", $"[content redacted, {text.Length} bytes]"));
        }

        if (eventArgs.KittyResult is { } kitty)
        {
            foreach (var item in kitty.Items)
            {
                fields.Add(new DiagnosticField(
                    $"Kitty {item.Mime}",
                    $"[content redacted, {item.Data.Length} bytes]"));
            }

            kitty.Dispose();
        }

        _log.Add(
            DiagnosticEventKind.Clipboard,
            eventArgs.IsSucceeded ? "Clipboard transfer succeeded" : "Clipboard transfer failed",
            "This is the completed public clipboard-service outcome. Payload content is never retained in the general event history.",
            fields);
    }

    private static DiagnosticField[] Fields(params (string Name, string Value)[] fields) =>
        [.. fields.Select(static field => new DiagnosticField(field.Name, field.Value))];

    private static string FormatPoint(Point? point) => point is { } value
        ? $"({value.X}, {value.Y})"
        : "not reported";

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();

        if (_application is { } application)
        {
            application.Resize -= OnResize;
            application.Terminal.Clipboard.ClipboardPasteReceived -= OnClipboardPaste;
            application.Terminal.Clipboard.KittyClipboardReplyReceived -= OnClipboardReply;
            _application = null;
        }
    }
}
