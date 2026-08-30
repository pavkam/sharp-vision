// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

using SharpVision.Terminal.Graphics;

using DiagnosticImage = Image;

/// <summary>Hosts explicit terminal-service probes and visual rendering specimens.</summary>
internal sealed class TerminalProbePanel: CompositeControlBase
{
    private readonly CapabilityDashboard _capabilities;
    private readonly Text _status;
    private readonly Button _bell;
    private readonly Button _title;
    private readonly Button _notification;
    private readonly Button _clipboard;
    private readonly Button _showSpecimens;
    private readonly Button _synchronizedOutput;
    private readonly Button _styledUnderlines;
    private readonly Button _underlineColor;
    private readonly Button _overline;
    private readonly Button _showGraphics;
    private readonly Button _pass;
    private readonly Button _fail;
    private readonly Stack _specimens;
    private readonly Text _modeGuide;
    private readonly DiagnosticImage _image;
    private readonly ClipboardRoundTripProbe _clipboardProbe;
    private Application? _application;
    private TerminalProtocol[] _pendingProtocols = [];
    private bool _graphicsConfirmationPending;

    /// <summary>Initializes the explicit test panel.</summary>
    /// <param name="capabilities">The non-null capability dashboard to update.</param>
    internal TerminalProbePanel(CapabilityDashboard capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _capabilities = capabilities;
        _clipboardProbe = new ClipboardRoundTripProbe(SetStatus, _capabilities.SetVerification);
        _status = new Text("<d>Select a test. Nothing on this page runs automatically.</d>")
        {
            Overflow = Overflow.Wrap
        };
        _bell = new Button("&Ring bell");
        _bell.Click += (_, _) => RingBell();
        _title = new Button("Test &title");
        _title.Click += (_, _) => TestTitle();
        _notification = new Button("Send &notification");
        _notification.Click += (_, _) => TestNotification();
        _clipboard = new Button("Clipboard &round trip");
        _clipboard.Click += (_, _) => _clipboardProbe.Start();
        _showSpecimens = new Button("Show &rendition + Unicode");
        _showSpecimens.Click += (_, _) => ShowSpecimens();
        _synchronizedOutput = new Button("Check s&ync output");
        _synchronizedOutput.Click += (_, _) => CheckRendition(
            TerminalProtocol.SynchronizedOutput,
            "Resize or switch tabs repeatedly and look for partial-frame tearing, then record the observed result.");
        _styledUnderlines = new Button("Check underline st&yles");
        _styledUnderlines.Click += (_, _) => CheckRendition(
            TerminalProtocol.StyledUnderlines,
            "Inspect straight, double, curly, dotted, and dashed underline shapes.");
        _underlineColor = new Button("Check underline &color");
        _underlineColor.Click += (_, _) => CheckRendition(
            TerminalProtocol.UnderlineColor,
            "Inspect whether the curly underline under 'curly yellow' is yellow.");
        _overline = new Button("Check &overline");
        _overline.Click += (_, _) => CheckRendition(
            TerminalProtocol.Overline,
            "Inspect whether 'overline' has a line above its glyphs.");
        _showGraphics = new Button("Show &graphics sample");
        _showGraphics.Click += (_, _) => ShowGraphics();
        _pass = new Button("✓ &Pass") { IsEnabled = false };
        _pass.Click += (_, _) => Confirm(passed: true);
        _fail = new Button("! &Fail") { IsEnabled = false };
        _fail.Click += (_, _) => Confirm(passed: false);

        _image = new DiagnosticImage
        {
            Width = Length.Cells(30),
            Height = Length.Cells(8),
            AlternateText = "[graphics fallback]",
            Stretch = ImageStretch.Contain
        };
        _specimens = BuildSpecimens(_image);
        _specimens.Visibility = Visibility.Collapsed;
        _modeGuide = new Text("<d>Runtime mode instructions will appear after attachment.</d>")
        {
            Overflow = Overflow.Wrap
        };

        var actions = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { _bell, _title, _notification, _clipboard }
        };
        var visualActions = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { _showSpecimens, _synchronizedOutput, _showGraphics }
        };
        var confirmationActions = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { _styledUnderlines, _underlineColor, _overline, _pass, _fail }
        };
        var content = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Padding = new Thickness(1),
            Spacing = 1,
            Children =
            {
                new Text("<accent><b>Explicit terminal checks</b></accent>\n" +
                         "These actions may ring, change the title, notify, modify the clipboard briefly, or emit image data."),
                actions,
                _status,
                new Text("<accent><b>Passive input checks</b></accent>"),
                _modeGuide,
                new Text("<accent><b>Visual specimens</b></accent>\n" +
                         "Reveal the samples, then verify each optional rendition independently."),
                visualActions,
                confirmationActions,
                _specimens
            }
        };
        InitializeContent(content);
    }

    /// <summary>Attaches service availability and probe completion handlers.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void Attach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null)
        {
            throw new InvalidOperationException("The terminal probe panel is already attached.");
        }

        _application = application;
        RefreshAvailability(application);
        _clipboardProbe.Attach(application);
        application.GraphicsDiagnostic += OnGraphicsDiagnostic;
    }

    /// <summary>Refreshes service and protocol action availability after capability refinement.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void RefreshAvailability(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _bell.IsEnabled = application.Terminal.Bell.IsSupported;
        _title.IsEnabled = application.Terminal.IsTitleSupported;
        _notification.IsEnabled = application.Terminal.Notifications.IsSupported;
        _clipboard.IsEnabled = application.Terminal.Clipboard.IsSupported;
        _styledUnderlines.IsEnabled = application.Capabilities.StyledUnderlines.Authoritative;
        _underlineColor.IsEnabled = application.Capabilities.UnderlineColor.Authoritative;
        _overline.IsEnabled = application.Capabilities.Overline.Authoritative;
        _synchronizedOutput.IsEnabled = application.Capabilities.SynchronizedOutput.Authoritative;
        _showGraphics.IsEnabled = HasGraphicsSupport(application);
        var modes = application.TerminalDiagnostics.Modes;
        _modeGuide.Content =
            $"Focus reporting: {Mode(modes.FocusReportingActive)} — switch to another terminal window and back.\n" +
            $"Bracketed paste: {Mode(modes.BracketedPasteActive)} — paste text containing spaces or line breaks.\n" +
            $"Mouse: {Mode(modes.MouseActive)} — click, drag, scroll, and inspect cell/pixel coordinates.\n" +
            $"Kitty keyboard: {Mode(modes.KittyKeyboardActive)}; xterm modifyOtherKeys: {Mode(modes.ModifyOtherKeysActive)} — press modified keys, repeats, and releases.\n" +
            "Open Input events to inspect the exact decoded values. Matching events mark optional protocols Observed automatically.";
    }

    private void RingBell()
    {
        if (_application is not { } application)
        {
            return;
        }

        application.Terminal.Bell.Ring();
        BeginConfirmation([], "Did you hear or otherwise perceive the terminal alert?");
    }

    private void TestTitle()
    {
        if (_application is not { } application)
        {
            return;
        }

        application.Terminal.SetTitle("SharpVision Terminal Debugger — title test");
        BeginConfirmation(
            [],
            "Did the terminal window title change? The previous title cannot be read or restored by this API.");
    }

    private void TestNotification()
    {
        if (_application is not { } application)
        {
            return;
        }

        application.Terminal.Notifications.Notify(
            "SharpVision Terminal Debugger",
            "Explicit desktop-notification test");
        BeginConfirmation([TerminalProtocol.Notifications], "Did a desktop notification appear?");
    }

    private void ShowSpecimens()
    {
        _specimens.Visibility = Visibility.Visible;
        _pendingProtocols = [];
        _pass.IsEnabled = false;
        _fail.IsEnabled = false;
        SetStatus("<info>Specimens revealed. Use the three focused checks to record optional rendition results.</info>");
    }

    private void CheckRendition(TerminalProtocol protocol, string prompt)
    {
        _specimens.Visibility = Visibility.Visible;
        BeginConfirmation([protocol], prompt);
    }

    private void ShowGraphics()
    {
        if (_application is not { } application || !HasGraphicsSupport(application))
        {
            return;
        }

        _specimens.Visibility = Visibility.Visible;
        _image.Source = CreateDiagnosticImage();
        _graphicsConfirmationPending = true;
        var protocols = GraphicsProtocols(application);
        BeginConfirmation(
            protocols,
            protocols.Length == 0
                ? "Inspect the checkerboard image. The shared non-retained backend may choose Sixel or iTerm2 per placement, so this result will not be attributed to either protocol."
                : $"Inspect the generated checkerboard image. Selected backend: {application.TerminalDiagnostics.GraphicsBackend}.");
    }

    private void BeginConfirmation(TerminalProtocol[] protocols, string prompt)
    {
        _pendingProtocols = protocols;
        _pass.IsEnabled = true;
        _fail.IsEnabled = true;
        SetStatus($"<warning>{TextMarkup.Escape(prompt)}</warning> Choose Pass or Fail.");
    }

    private void Confirm(bool passed)
    {
        var attributed = _pendingProtocols.Length > 0;
        var state = passed ? VerificationState.Passed : VerificationState.Failed;
        var detail = passed
            ? "The user confirmed the visible or audible result in this terminal session."
            : "The user reported that the visible or audible result was incorrect or absent.";

        foreach (var protocol in _pendingProtocols)
        {
            _capabilities.SetVerification(protocol, state, detail);
        }

        _graphicsConfirmationPending = false;
        _pendingProtocols = [];
        _pass.IsEnabled = false;
        _fail.IsEnabled = false;
        SetStatus(attributed
            ? passed
                ? "<success>Result recorded as passed.</success>"
                : "<error>Result recorded as failed.</error>"
            : "<info>Visual result acknowledged without attributing the shared backend to one protocol.</info>");
    }

    private void OnGraphicsDiagnostic(object? sender, GraphicsDiagnosticEventArgs eventArgs)
    {
        var detail = string.Join(", ", eventArgs.Placements.Select(static placement => placement.Reason));
        var backend = _application?.TerminalDiagnostics.GraphicsBackend.ToString() ?? "unknown";
        SetStatus(
            $"<error>{TextMarkup.Escape(backend)} graphics fell back to cells: {TextMarkup.Escape(detail)}.</error>");

        if (_graphicsConfirmationPending)
        {
            foreach (var protocol in _pendingProtocols)
            {
                _capabilities.SetVerification(
                    protocol,
                    VerificationState.Failed,
                    $"The selected {backend} graphics path fell back to ordinary cells: {detail}.");
            }

            _graphicsConfirmationPending = false;
            _pendingProtocols = [];
            _pass.IsEnabled = false;
            _fail.IsEnabled = false;
        }
    }

    private void SetStatus(string value) => _status.Content = value;

    private static string Mode(bool enabled) => enabled ? "<success>enabled</success>" : "<warning>not active</warning>";

    private static Stack BuildSpecimens(DiagnosticImage image) => new()
    {
        Spacing = 1,
        Children =
        {
            new GroupBox
            {
                HeaderText = "Color and rendition",
                Content = new Text(
                    "16 colors  <black>██</black><red>██</red><green>██</green><yellow>██</yellow><blue>██</blue><magenta>██</magenta><cyan>██</cyan><white>██</white>  " +
                    "<brightblack>██</brightblack><brightred>██</brightred><brightgreen>██</brightgreen><brightyellow>██</brightyellow><brightblue>██</brightblue><brightmagenta>██</brightmagenta><brightcyan>██</brightcyan><brightwhite>██</brightwhite>\n" +
                    "RGB ramp   <fg=#ff3b30>██</fg><fg=#ff9500>██</fg><fg=#ffcc00>██</fg><fg=#34c759>██</fg><fg=#00c7be>██</fg><fg=#007aff>██</fg><fg=#5856d6>██</fg><fg=#af52de>██</fg>\n" +
                    "Underline  <u=straight>straight</u>  <u=double>double</u>  <u=curly><uc=brightyellow>curly yellow</uc></u>  <u=dotted>dotted</u>  <u=dashed>dashed</u>\n" +
                    "Attributes <b>bold</b> <i>italic</i> <strike>strike</strike> <overline>overline</overline> <reverse>reverse</reverse>")
                {
                    Padding = new Thickness(1),
                    Overflow = Overflow.Wrap
                }
            },
            new GroupBox
            {
                HeaderText = "Unicode cell geometry",
                Content = new Text(
                    "Cell guide  |0|1|2|3|4|5|6|7|8|9|\n" +
                    "Combining   |é|x|  e + U+0301 should occupy one cell\n" +
                    "Wide CJK   |界 |x|  the glyph owns two cells\n" +
                    "Emoji ZWJ  |👩‍💻 |x|  one grapheme, terminal-dependent width\n" +
                    "Variation   |✈️ |x|  text plus VS16 stays one grapheme\n" +
                    "Ambiguous   |·|Ω|—|  compare with the profile policy")
                {
                    Padding = new Thickness(1),
                    Overflow = Overflow.Wrap
                }
            },
            new GroupBox
            {
                HeaderText = "Graphics backend",
                Content = image
            }
        }
    };

    private static ImageSource CreateDiagnosticImage()
    {
        const int width = 64;
        const int height = 32;
        var rgba = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                var light = ((x / 8) + (y / 8)) % 2 == 0;
                rgba[offset] = light ? (byte) 32 : (byte) 255;
                rgba[offset + 1] = light ? (byte) 190 : (byte) 77;
                rgba[offset + 2] = light ? (byte) 255 : (byte) 166;
                rgba[offset + 3] = 255;
            }
        }

        return ImageSource.FromRgba(new Size(width, height), rgba);
    }

    private static bool HasGraphicsSupport(Application application) =>
        application.Capabilities.KittyGraphics.Authoritative ||
        application.Capabilities.Sixel.Authoritative ||
        application.Capabilities.ItermImages.Authoritative;

    private static TerminalProtocol[] GraphicsProtocols(Application application) =>
        application.TerminalDiagnostics.GraphicsBackend switch
        {
            TerminalGraphicsBackend.Kitty => [TerminalProtocol.KittyGraphics],
            TerminalGraphicsBackend.NonRetained when application.Capabilities.Sixel.Authoritative &&
                                                     !application.Capabilities.ItermImages.Authoritative =>
                [TerminalProtocol.Sixel],
            TerminalGraphicsBackend.NonRetained when application.Capabilities.ItermImages.Authoritative &&
                                                     !application.Capabilities.Sixel.Authoritative =>
                [TerminalProtocol.ItermImages],
            TerminalGraphicsBackend.NonRetained => [],
            TerminalGraphicsBackend.CellFallback => [],
            _ => throw new InvalidOperationException("Terminal diagnostics contained an unknown graphics backend.")
        };

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            if (_application is { } application)
            {
                application.GraphicsDiagnostic -= OnGraphicsDiagnostic;
            }

            _clipboardProbe.Dispose();
            _application = null;
        }

        base.OnUnavailable(reason);
    }
}
