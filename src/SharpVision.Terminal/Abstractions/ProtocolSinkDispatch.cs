// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>
/// Dispatches each vendor-specific protocol reply to its optional extension interface on
/// <see cref="IProtocolSink"/> when implemented, adapting to the base numeric response or a
/// synthetic unsupported diagnostic otherwise. This preserves, at the single call site each
/// reply is decoded from, the guarantee that a sink implementing only the required members of
/// <see cref="IProtocolSink"/> still observes every vendor reply in some adapted form.
/// </summary>
internal static class ProtocolSinkDispatch
{
    extension(IProtocolSink sink)
    {
        /// <summary>Dispatches one validated palette or dynamic-color response.</summary>
        public void Dispatch(in PaletteResponse value)
        {
            if (sink is IPaletteResponseSink typed)
            {
                typed.Response(in value);
                return;
            }

            var values = value.Kind == ResponseKind.PaletteColor
                ? new[] { value.Index.GetValueOrDefault(), value.Red, value.Green, value.Blue }
                : [value.Red, value.Green, value.Blue];
            var response = new XtermCapabilitiesResponse(value.Kind, values);
            sink.Response(in response);
        }

        /// <summary>Dispatches one validated pixel or cell metrics response.</summary>
        public void Dispatch(in MetricsResponse value)
        {
            if (sink is IMetricsResponseSink typed)
            {
                typed.Response(in value);
                return;
            }

            int[] values = [value.Size.Width, value.Size.Height];
            var response = new XtermCapabilitiesResponse(value.Kind, values);
            sink.Response(in response);
        }

        /// <summary>Dispatches one validated bounded DECRQSS response.</summary>
        public void Dispatch(in StatusResponse value)
        {
            if (sink is IStatusResponseSink typed)
            {
                typed.Response(in value);
                return;
            }

            var diagnostic = new Diagnostic(
                DiagnosticCode.Unsupported,
                SequenceKind.Dcs,
                offset: 0,
                discardedBytes: 0);
            sink.Input(in diagnostic);
        }

        /// <summary>Dispatches one validated bounded XTGETTCAP response.</summary>
        public void Dispatch(CapabilityResponse value)
        {
            if (sink is ICapabilityResponseSink typed)
            {
                typed.Response(value);
                return;
            }

            var diagnostic = new Diagnostic(
                DiagnosticCode.Unsupported,
                SequenceKind.Dcs,
                offset: 0,
                discardedBytes: 0);
            sink.Input(in diagnostic);
        }

        /// <summary>Dispatches one bounded Kitty graphics APC response.</summary>
        public void Dispatch(Kitty.Graphics.KittyGraphicsResponse value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (sink is IKittyGraphicsResponseSink typed)
            {
                typed.Response(value);
                return;
            }

            var diagnostic = value.Diagnostic ?? new Diagnostic(
                DiagnosticCode.Unsupported,
                SequenceKind.Apc,
                offset: 0,
                discardedBytes: 0);
            sink.Input(in diagnostic);
        }

        /// <summary>Dispatches one validated iTerm2 OSC 1337 Capabilities feature-reporting reply.</summary>
        public void Dispatch(ItermCapabilitiesResponse value)
        {
            if (sink is IItermCapabilitiesResponseSink typed)
            {
                typed.Response(value);
                return;
            }

            var diagnostic = new Diagnostic(
                DiagnosticCode.Unsupported,
                SequenceKind.Osc,
                offset: 0,
                discardedBytes: 0);
            sink.Input(in diagnostic);
        }

        /// <summary>Dispatches one decoded OSC 52 clipboard reply.</summary>
        public void Dispatch(in Clipboard.ClipboardReply value)
        {
            if (sink is IClipboardReplySink typed)
            {
                typed.Response(in value);
                return;
            }

            var diagnostic = new Diagnostic(
                DiagnosticCode.Unsupported,
                SequenceKind.Osc,
                offset: 0,
                discardedBytes: 0);
            sink.Input(in diagnostic);
        }

        /// <summary>Dispatches one decoded Kitty OSC 5522 clipboard packet.</summary>
        public void Dispatch(Kitty.Clipboard.KittyClipboardPacket value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (sink is IKittyClipboardPacketSink typed)
            {
                typed.Response(value);
                return;
            }

            var diagnostic = value.Diagnostic ?? new Diagnostic(
                DiagnosticCode.Unsupported,
                SequenceKind.Osc,
                offset: 0,
                discardedBytes: 0);
            sink.Input(in diagnostic);
        }
    }
}
