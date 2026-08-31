// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

using SharpVision.Terminal.Graphics;

using DiagnosticImage = Image;

/// <summary>Hosts explicit terminal-service tests and always-visible rendering specimens.</summary>
internal sealed class TerminalProbePanel: CompositeControlBase
{
    private readonly CapabilityDashboard _capabilities;
    private readonly Text _status;
    private readonly Button _bell;
    private readonly Button _title;
    private readonly Button _notification;
    private readonly Button _clipboard;
    private readonly Dictionary<string, Text> _modeStates = [];
    private readonly Text _paletteState;
    private readonly Text _paletteExpected;
    private readonly Text _rgbState;
    private readonly Text _rgbExpected;
    private readonly Text _synchronizedOutputState;
    private readonly Text _styledUnderlinesState;
    private readonly Text _underlineColorState;
    private readonly Text _overlineState;
    private readonly Text _graphicsState;
    private readonly DiagnosticImage _image;
    private readonly ImageSource _diagnosticImage;
    private readonly ClipboardRoundTripProbe _clipboardProbe;
    private Application? _application;
    private (TerminalGraphicsBackend Backend, bool Kitty, bool Sixel, bool Iterm)? _graphicsSelection;
    private bool _graphicsFallbackHandled;

    /// <summary>Initializes the live terminal test lab.</summary>
    /// <param name="capabilities">The non-null capability dashboard to update.</param>
    internal TerminalProbePanel(CapabilityDashboard capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _capabilities = capabilities;
        _clipboardProbe = new ClipboardRoundTripProbe(SetStatus, _capabilities.SetVerification);
        _status = new Text(
            "<info>Visual specimens are already live.</info> " +
            "Use the buttons only for tests that need an intentional side effect.")
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

        var actions = CreateActionsTable();
        var passiveChecks = CreatePassiveChecksTable();
        _paletteState = WaitingState();
        _paletteExpected = new Text("Waiting for the active color policy…") { Overflow = Overflow.Wrap };
        _rgbState = WaitingState();
        _rgbExpected = new Text("Waiting for the active color policy…") { Overflow = Overflow.Wrap };
        _synchronizedOutputState = WaitingState();
        _styledUnderlinesState = WaitingState();
        _underlineColorState = WaitingState();
        _overlineState = WaitingState();
        _graphicsState = WaitingState();
        _diagnosticImage = CreateDiagnosticImage();
        _image = new DiagnosticImage
        {
            Width = Length.Cells(30),
            Height = Length.Cells(8),
            AlternateText = "[graphics unavailable]",
            Stretch = ImageStretch.Contain
        };
        var rendition = CreateRenditionTable();
        var unicode = CreateUnicodeTable();

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
                new Text(
                    "<accent><b>Terminal test lab</b></accent> · " +
                    "Samples below render immediately so broken output is visible, not hidden behind a button."),
                Card("Explicit service tests", actions),
                Card("Latest test result", _status),
                Card("Live input checks", passiveChecks),
                Card("Rendition — rendered now", rendition),
                Card("Unicode cell geometry — rendered now", unicode),
                Card("Graphics backend — rendered when supported", CreateGraphicsContent())
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

    /// <summary>Refreshes service, mode, and specimen availability after capability refinement.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void RefreshAvailability(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _bell.IsEnabled = application.Terminal.Bell.IsSupported;
        _title.IsEnabled = application.Terminal.IsTitleSupported;
        _notification.IsEnabled = application.Terminal.Notifications.IsSupported;
        _clipboard.IsEnabled = application.Terminal.Clipboard.IsSupported;

        var modes = application.TerminalDiagnostics.Modes;
        SetMode("Focus reporting", modes.FocusReportingActive);
        SetMode("Bracketed paste", modes.BracketedPasteActive);
        SetMode("Mouse", modes.MouseActive);
        SetMode("Kitty keyboard", modes.KittyKeyboardActive);
        SetMode("xterm modifyOtherKeys", modes.ModifyOtherKeysActive);

        _synchronizedOutputState.Content = SupportMarkup(application.Capabilities.SynchronizedOutput);
        _styledUnderlinesState.Content = SupportMarkup(application.Capabilities.StyledUnderlines);
        _underlineColorState.Content = SupportMarkup(application.Capabilities.UnderlineColor);
        _overlineState.Content = SupportMarkup(application.Capabilities.Overline);
        UpdateColorSpecimenState(application.Capabilities.ColorDepth);

        var graphicsSelection = (
            application.TerminalDiagnostics.GraphicsBackend,
            application.Capabilities.KittyGraphics.Authoritative,
            application.Capabilities.Sixel.Authoritative,
            application.Capabilities.ItermImages.Authoritative);

        if (_graphicsSelection is { } previousSelection && previousSelection != graphicsSelection)
        {
            // A refined capability set or backend is a new route, so the always-visible specimen
            // gets one fresh attempt. An unchanged failed route remains stable and cannot loop.
            _graphicsFallbackHandled = false;
        }

        _graphicsSelection = graphicsSelection;
        var hasGraphics = HasGraphicsSupport(application);

        if (_graphicsFallbackHandled)
        {
            _graphicsState.Content = "<error>× Fell back to cells</error>";
            _image.AlternateText = "[graphics fallback]";
            _image.Source = null;
        }
        else
        {
            _graphicsState.Content = hasGraphics
                ? $"<success>✓ {application.TerminalDiagnostics.GraphicsBackend}</success>"
                : "<d>— No authorized image protocol</d>";
            _image.AlternateText = hasGraphics ? "[graphics cell fallback]" : "[graphics unavailable]";
            _image.Source = hasGraphics ? _diagnosticImage : null;
        }
    }

    private Table CreateActionsTable()
    {
        var table = new Table
        {
            IsFocusable = false,
            IsTabStop = false,
            SelectionMode = TableSelectionMode.None,
            ShowGridLines = false,
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Test", 18));
        table.Columns.Add(TableColumn.Fixed("Action", 24));
        table.Columns.Add(TableColumn.Fill("What happens"));
        table.Rows.Add(ActionRow(
            "Clipboard",
            _clipboard,
            "Reads, writes a unique marker, compares it, then restores and verifies the previous text."));
        table.Rows.Add(ActionRow("Audible alert", _bell, "Requests the terminal bell; listen for or observe its configured alert."));
        table.Rows.Add(ActionRow("Window title", _title, "Changes the title to identify this test; the previous title cannot be read back."));
        table.Rows.Add(ActionRow("Desktop alert", _notification, "Requests a desktop notification through the authorized terminal service."));
        return table;
    }

    private Table CreatePassiveChecksTable()
    {
        var table = new Table
        {
            IsFocusable = false,
            IsTabStop = false,
            SelectionMode = TableSelectionMode.None,
            ShowGridLines = true,
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Input", 22));
        table.Columns.Add(TableColumn.Fixed("Runtime", 13));
        table.Columns.Add(TableColumn.Fill("Try it now"));
        AddPassiveRow(table, "Focus reporting", "Switch to another terminal window and back.");
        AddPassiveRow(table, "Bracketed paste", "Paste text containing spaces or line breaks.");
        AddPassiveRow(table, "Mouse", "Click, drag, and scroll; Input events shows cell and pixel coordinates.");
        AddPassiveRow(table, "Kitty keyboard", "Press modified keys, repeats, and releases.");
        AddPassiveRow(table, "xterm modifyOtherKeys", "Press modified keys and compare the decoded identity.");
        return table;
    }

    private Table CreateRenditionTable()
    {
        var table = new Table
        {
            IsFocusable = false,
            IsTabStop = false,
            SelectionMode = TableSelectionMode.None,
            ShowGridLines = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Feature", 20));
        table.Columns.Add(TableColumn.Fixed("Detected", 24));
        table.Columns.Add(TableColumn.Fixed("Live sample", 38));
        table.Columns.Add(TableColumn.Fill("Expected"));
        table.Rows.Add(SpecimenRow(
            "16-color palette",
            _paletteState,
            "<black>██</black><red>██</red><green>██</green><yellow>██</yellow><blue>██</blue><magenta>██</magenta><cyan>██</cyan><white>██</white> " +
                "<brightblack>██</brightblack><brightred>██</brightred><brightgreen>██</brightgreen><brightyellow>██</brightyellow><brightblue>██</brightblue><brightmagenta>██</brightmagenta><brightcyan>██</brightcyan><brightwhite>██</brightwhite>",
            _paletteExpected));
        table.Rows.Add(SpecimenRow(
            "RGB color",
            _rgbState,
            "<fg=#ff3b30>██</fg><fg=#ff9500>██</fg><fg=#ffcc00>██</fg><fg=#34c759>██</fg><fg=#00c7be>██</fg><fg=#007aff>██</fg><fg=#5856d6>██</fg><fg=#af52de>██</fg>",
            _rgbExpected));
        table.Rows.Add(SpecimenRow(
            "Synchronized output",
            _synchronizedOutputState,
            "Resize and switch tabs",
            "Frames remain whole without partial-frame tearing."));
        table.Rows.Add(SpecimenRow(
            "Underline styles",
            _styledUnderlinesState,
            "<u=straight>straight</u> <u=double>double</u> <u=curly>curly</u> <u=dotted>dotted</u> <u=dashed>dashed</u>",
            "Five visibly distinct underline shapes."));
        table.Rows.Add(SpecimenRow(
            "Underline color",
            _underlineColorState,
            "<u=curly><uc=brightyellow>curly yellow</uc></u>",
            "The curly underline is yellow."));
        table.Rows.Add(SpecimenRow(
            "Overline",
            _overlineState,
            "<overline>overline</overline>",
            "A line appears above the glyphs."));
        table.Rows.Add(SpecimenRow(
            "Core attributes",
            new Text("<success>✓ Built in</success>"),
            "<b>bold</b> <i>italic</i> <strike>strike</strike> <reverse>reverse</reverse>",
            "Weight, slant, strike, and reverse are visibly different."));
        return table;
    }

    private static Table CreateUnicodeTable()
    {
        var table = new Table
        {
            IsFocusable = false,
            IsTabStop = false,
            SelectionMode = TableSelectionMode.None,
            ShowGridLines = true,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Case", 18));
        table.Columns.Add(TableColumn.Fixed("Live cells", 32));
        table.Columns.Add(TableColumn.Fill("Expected"));
        table.Rows.Add(UnicodeRow("Cell guide", "|0|1|2|3|4|5|6|7|8|9|", "Every separator aligns."));
        table.Rows.Add(UnicodeRow("Combining mark", "|é|x|", "e + U+0301 occupies one cell."));
        table.Rows.Add(UnicodeRow("Wide CJK", "|界 |x|", "界 owns two cells; x remains aligned."));
        table.Rows.Add(UnicodeRow("Emoji ZWJ", "|👩‍💻 |x|", "One grapheme with terminal-dependent width."));
        table.Rows.Add(UnicodeRow("Variation selector", "|✈️ |x|", "Text plus VS16 remains one grapheme."));
        table.Rows.Add(UnicodeRow("Ambiguous width", "|·|Ω|—|", "Widths follow the detected profile policy."));
        return table;
    }

    private Stack CreateGraphicsContent() => new()
    {
        Spacing = 1,
        Children =
        {
            new Stack
            {
                Orientation = Orientation.Horizontal,
                Spacing = 1,
                Children =
                {
                    new Text("Detected backend:"),
                    _graphicsState
                }
            },
            _image,
            new Text("A cyan-and-coral checkerboard should fill this area. A labeled cell preview means the image path fell back.")
            {
                Overflow = Overflow.Wrap
            }
        }
    };

    private void AddPassiveRow(Table table, string label, string instruction)
    {
        var state = WaitingState();
        _modeStates.Add(label, state);
        table.Rows.Add(new TableRow([
            new Text(label),
            state,
            new Text(instruction) { Overflow = Overflow.Wrap }
        ]));
    }

    private static TableRow ActionRow(string label, Button action, string explanation) => new([
        new Text(label),
        action,
        new Text(explanation) { Overflow = Overflow.Wrap }
    ]);

    private static TableRow SpecimenRow(string label, Text state, string sample, string expected) => new([
        new Text(label),
        state,
        new Text(sample) { Overflow = Overflow.Ellipsis },
        new Text(expected) { Overflow = Overflow.Wrap }
    ]);

    private static TableRow SpecimenRow(string label, Text state, string sample, Text expected) => new([
        new Text(label),
        state,
        new Text(sample) { Overflow = Overflow.Ellipsis },
        expected
    ]);

    private static TableRow UnicodeRow(string label, string sample, string expected) => new([
        new Text(label),
        new Text(sample),
        new Text(expected) { Overflow = Overflow.Wrap }
    ]);

    private static GroupBox Card(string title, ControlBase content) => new()
    {
        HeaderText = title,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = content
    };

    private static Text WaitingState() => new("<d>Waiting…</d>");

    private void SetMode(string label, bool active) => _modeStates[label].Content = active
        ? "<success>● Active</success>"
        : "<warning>○ Inactive</warning>";

    private void UpdateColorSpecimenState(ColorDepth colorDepth)
    {
        switch (colorDepth)
        {
            case ColorDepth.Monochrome:
                _paletteState.Content = "<error>× Monochrome</error>";
                _paletteExpected.Content = "Color is intentionally not relied on; swatches may be indistinguishable.";
                _rgbState.Content = "<error>× Monochrome</error>";
                _rgbExpected.Content = "The ramp is rendered without relying on color.";
                break;

            case ColorDepth.Basic16:
                _paletteState.Content = "<success>✓ Basic 16</success>";
                _paletteExpected.Content = "Sixteen palette swatches should be distinct.";
                _rgbState.Content = "<warning>○ Projected to 16</warning>";
                _rgbExpected.Content = "The RGB ramp is projected into the 16-color palette; banding is expected.";
                break;

            case ColorDepth.Indexed256:
                _paletteState.Content = "<success>✓ Indexed 256</success>";
                _paletteExpected.Content = "Sixteen palette swatches should be distinct.";
                _rgbState.Content = "<warning>○ Projected to 256</warning>";
                _rgbExpected.Content = "The RGB ramp is quantized into the indexed palette.";
                break;

            case ColorDepth.TrueColor:
                _paletteState.Content = "<success>✓ True color</success>";
                _paletteExpected.Content = "Sixteen palette swatches should be distinct.";
                _rgbState.Content = "<success>✓ True color</success>";
                _rgbExpected.Content = "A smooth red-to-violet ramp without palette projection.";
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(colorDepth),
                    colorDepth,
                    "The terminal color depth is unknown.");
        }
    }

    private void RingBell()
    {
        if (_application is not { } application)
        {
            return;
        }

        application.Terminal.Bell.Ring();
        SetStatus("<info>Bell requested.</info> Listen for or observe the terminal's configured alert.");
    }

    private void TestTitle()
    {
        if (_application is not { } application)
        {
            return;
        }

        application.Terminal.SetTitle("SharpVision Terminal Debugger — title test");
        SetStatus(
            "<info>Title change requested.</info> The terminal title should now identify the SharpVision test; the previous title cannot be read back.");
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
        SetStatus("<info>Desktop notification requested.</info> Look for the terminal's notification surface.");
    }

    private void OnGraphicsDiagnostic(object? sender, GraphicsDiagnosticEventArgs eventArgs)
    {
        if (_graphicsFallbackHandled)
        {
            return;
        }

        // A retained Image records a placement on every frame. Once its selected protocol falls
        // back, replace it with stable cell content before publishing UI state so the diagnostic
        // cannot invalidate another frame and feed itself indefinitely.
        _graphicsFallbackHandled = true;
        _image.Source = null;
        _image.AlternateText = "[graphics fallback]";
        _graphicsState.Content = "<error>× Fell back to cells</error>";
        var detail = string.Join(", ", eventArgs.Placements.Select(static placement => placement.Reason));
        var backend = _application?.TerminalDiagnostics.GraphicsBackend.ToString() ?? "unknown";
        SetStatus(
            $"<error>{TextMarkup.Escape(backend)} graphics fell back to cells: {TextMarkup.Escape(detail)}.</error>");

        if (_application is { } application)
        {
            foreach (var protocol in GraphicsProtocols(application))
            {
                _capabilities.SetVerification(
                    protocol,
                    VerificationState.Failed,
                    $"The selected {backend} graphics path fell back to ordinary cells: {detail}.");
            }
        }
    }

    private void SetStatus(string value) => _status.Content = value;

    private static string SupportMarkup(Feature feature) => feature.State switch
    {
        CapabilitySupport.Supported when feature.Authoritative => "<success>✓ Supported</success>",
        CapabilitySupport.Supported => "<warning>○ Detected, not authorized</warning>",
        CapabilitySupport.Tentative => "<warning>○ Tentative</warning>",
        CapabilitySupport.Unsupported => "<error>× Unsupported</error>",
        CapabilitySupport.Unknown => "<d>— Unknown</d>",
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature.State, "The capability support state is unknown.")
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
