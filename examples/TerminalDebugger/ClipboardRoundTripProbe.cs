// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Runs one explicit, restore-aware terminal clipboard round trip.</summary>
internal sealed class ClipboardRoundTripProbe: IDisposable
{
    private readonly Action<string> _status;
    private readonly Action<TerminalProtocol, VerificationState, string> _verify;
    private Application? _application;
    private ClipboardProbeStage _stage;
    private string? _marker;
    private string? _original;

    /// <summary>Initializes one clipboard probe.</summary>
    /// <param name="status">The non-null status publisher.</param>
    /// <param name="verify">The non-null capability-verification publisher.</param>
    internal ClipboardRoundTripProbe(
        Action<string> status,
        Action<TerminalProtocol, VerificationState, string> verify)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(verify);
        _status = status;
        _verify = verify;
    }

    /// <summary>Attaches to one running application's clipboard completion stream.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void Attach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null)
        {
            throw new InvalidOperationException("The clipboard probe is already attached.");
        }

        _application = application;
        application.Terminal.Clipboard.KittyClipboardReplyReceived += OnReply;
    }

    /// <summary>Starts a probe by reading current text before any mutation.</summary>
    internal void Start()
    {
        if (_application is not { } application)
        {
            throw new InvalidOperationException("The clipboard probe is not attached.");
        }

        if (_stage != ClipboardProbeStage.Idle)
        {
            _status("<warning>A clipboard probe is already running.</warning>");
            return;
        }

        if (!application.Terminal.Clipboard.IsSupported)
        {
            _status("<error>Clipboard service is not authoritatively supported.</error>");
            return;
        }

        _original = null;
        _marker = $"SharpVision TerminalDebugger {Guid.NewGuid():N}";
        _stage = ClipboardProbeStage.ReadingOriginal;
        _status("<info>Reading current clipboard text before the test…</info>");
        application.Terminal.Clipboard.Request();
    }

    private void OnReply(object? sender, KittyClipboardReplyEventArgs eventArgs)
    {
        if (_application is not { } application || _stage == ClipboardProbeStage.Idle)
        {
            return;
        }

        switch (_stage)
        {
            case ClipboardProbeStage.ReadingOriginal:
                if (!TryReadText(eventArgs, out _original))
                {
                    Fail("Could not read existing clipboard text, so the debugger refused to overwrite it.");
                    return;
                }

                application.Terminal.Clipboard.Write(_marker);

                if (application.Capabilities.KittyClipboard.Authoritative)
                {
                    _stage = ClipboardProbeStage.WritingMarker;
                    _status("<info>Test marker sent through Kitty clipboard; waiting for acknowledgement…</info>");
                }
                else
                {
                    _stage = ClipboardProbeStage.ReadingMarker;
                    _status("<info>Test marker sent through OSC 52; reading it back…</info>");
                    application.Terminal.Clipboard.Request();
                }

                break;

            case ClipboardProbeStage.WritingMarker:
                if (!eventArgs.IsSucceeded)
                {
                    Fail("The terminal rejected or timed out the Kitty clipboard write.");
                    return;
                }

                _stage = ClipboardProbeStage.ReadingMarker;
                _status("<info>Kitty write acknowledged; reading the marker back…</info>");
                application.Terminal.Clipboard.Request();
                break;

            case ClipboardProbeStage.ReadingMarker:
                var protocol = ActiveProtocol(application);

                if (!TryReadText(eventArgs, out var actual))
                {
                    Verify(protocol, VerificationState.Failed, "The clipboard read-back returned no plain-text payload.");
                    Restore(application, "Clipboard read-back returned no plain-text payload.");
                    return;
                }

                var passed = string.Equals(actual, _marker, StringComparison.Ordinal);
                Verify(
                    protocol,
                    passed ? VerificationState.Passed : VerificationState.Failed,
                    passed
                        ? "A unique marker was written and read back exactly."
                        : "Clipboard read-back did not match the unique marker.");
                Restore(
                    application,
                    passed
                        ? "<success>Clipboard round trip passed.</success>"
                        : "<error>Clipboard round trip returned different text.</error>");
                break;

            case ClipboardProbeStage.Restoring:
                _stage = ClipboardProbeStage.Idle;
                break;

            case ClipboardProbeStage.Idle:
            default:
                throw new InvalidOperationException($"The clipboard probe stage '{_stage}' is unknown.");
        }
    }

    private void Restore(Application application, string result)
    {
        if (_original is null)
        {
            _stage = ClipboardProbeStage.Idle;
            _status(result);
            return;
        }

        application.Terminal.Clipboard.Write(_original);
        _status($"{result} <d>Previous clipboard text was restored.</d>");
        _stage = application.Capabilities.KittyClipboard.Authoritative
            ? ClipboardProbeStage.Restoring
            : ClipboardProbeStage.Idle;
    }

    private void Fail(string detail)
    {
        if (_application is { } application)
        {
            Verify(ActiveProtocol(application), VerificationState.Failed, detail);
        }

        _stage = ClipboardProbeStage.Idle;
        _status($"<error>{TextMarkup.Escape(detail)}</error>");
    }

    private void Verify(TerminalProtocol protocol, VerificationState state, string detail) =>
        _verify(protocol, state, detail);

    private static TerminalProtocol ActiveProtocol(Application application) =>
        application.Capabilities.KittyClipboard.Authoritative
            ? TerminalProtocol.KittyClipboard
            : TerminalProtocol.Osc52;

    private static bool TryReadText(KittyClipboardReplyEventArgs eventArgs, out string? text)
    {
        if (eventArgs.Text is { } osc52)
        {
            text = Encoding.UTF8.GetString(osc52.Span);
            return true;
        }

        if (eventArgs.KittyResult is { } result)
        {
            var item = result.Items.FirstOrDefault(
                static candidate => candidate.Mime.Equals("text/plain", StringComparison.OrdinalIgnoreCase));

            if (item is not null)
            {
                text = Encoding.UTF8.GetString(item.Data.Span);
                return true;
            }
        }

        text = null;
        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_application is { } application)
        {
            application.Terminal.Clipboard.KittyClipboardReplyReceived -= OnReply;
            _application = null;
        }

        _stage = ClipboardProbeStage.Idle;
        _marker = null;
        _original = null;
    }
}
