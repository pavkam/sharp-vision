// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Terminal.Capabilities;

/// <summary>Encodes implemented output protocols and posts them through the ordered write path.</summary>
internal sealed class TerminalServices: ITerminalServices, IBell, IClipboard
{
    private readonly Application _application;
    private readonly Lock _programGate = new();
    private Interpreter _interpreter = new(Limits.Default);
    private TerminalProfile? _programProfile;

    public TerminalServices(Application application)
    {
        Debug.Assert(application is not null, "The owning application must be provided.");
        _application = application;
    }

    /// <inheritdoc/>
    public IBell Bell => this;

    /// <inheritdoc/>
    public Description Description => _application.TerminalProfile.Description;

    /// <inheritdoc/>
    public IClipboard Clipboard => this;

    /// <inheritdoc/>
    public bool IsTitleSupported =>
        _application.TerminalProfile.IsAnsiCompatibility ||
        (Description.Origin == DescriptionOrigin.BuiltIn &&
         string.Equals(Description.Name, "windows-vt", StringComparison.Ordinal)) ||
        _application.TerminalProfile.Programs.HasZeroParameterPair("TS", "fsl");

    /// <inheritdoc/>
    bool IClipboard.IsSupported =>
        _application.Capabilities.Osc52.IsSupported || _application.Capabilities.KittyClipboard.IsSupported;

    /// <inheritdoc/>
    bool IBell.IsSupported => _application.TerminalProfile.Programs.Has("bel");

    /// <inheritdoc/>
    public void Ring()
    {
        var destination = new ArrayBufferWriter<byte>();

        lock (_programGate)
        {
            var profile = _application.TerminalProfile;
            PrepareInterpreter(profile);

            if (!profile.Programs.TryWrite("bel", [], _interpreter, destination))
            {
                return;
            }
        }

        _application.PostOutOfBand(destination.WrittenMemory);
    }

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (!IsTitleSupported)
        {
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(title);
        var destination = new ArrayBufferWriter<byte>(byteCount + 8);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));

        try
        {
            var written = Encoding.UTF8.GetBytes(title, rented);
            Osc.Title(new Writer(destination), rented.AsSpan(0, written));

            if (!_application.TerminalProfile.IsAnsiCompatibility &&
                !(Description.Origin == DescriptionOrigin.BuiltIn &&
                  string.Equals(Description.Name, "windows-vt", StringComparison.Ordinal)))
            {
                WriteDescribedTitle(rented.AsSpan(0, written));
            }
            else
            {
                _application.PostOutOfBand(destination.WrittenMemory);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private void WriteDescribedTitle(ReadOnlySpan<byte> title)
    {
        ReadOnlyMemory<byte> prefix;
        ReadOnlyMemory<byte> suffix;

        lock (_programGate)
        {
            var profile = _application.TerminalProfile;
            PrepareInterpreter(profile);

            if (!profile.Programs.TryExpandPair(
                    "TS",
                    "fsl",
                    _interpreter,
                    out prefix,
                    out suffix))
            {
                return;
            }
        }

        var destination = new ArrayBufferWriter<byte>(prefix.Length + title.Length + suffix.Length);
        destination.Write(prefix.Span);
        destination.Write(title);
        destination.Write(suffix.Span);
        _application.PostOutOfBand(destination.WrittenMemory);
    }

    private void PrepareInterpreter(TerminalProfile profile)
    {
        if (_programProfile is null || !_programProfile.IsRenderingEquivalentTo(profile))
        {
            _interpreter = new Interpreter(Limits.Default);
            _programProfile = profile;
        }
    }

    /// <inheritdoc/>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard)
    {
        if (!((IClipboard) this).IsSupported)
        {
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(text);
        var destination = new ArrayBufferWriter<byte>(byteCount + 16);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));

        try
        {
            var written = Encoding.UTF8.GetBytes(text, rented);
            Osc52.Write(new Writer(destination), selection, rented.AsSpan(0, written));
            _application.PostOutOfBand(destination.WrittenMemory);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    public void Request(Selection selection = Selection.Clipboard)
    {
        if (!((IClipboard) this).IsSupported)
        {
            return;
        }

        var destination = new ArrayBufferWriter<byte>(8);
        Osc52.Query(new Writer(destination), selection);
        _application.PostOutOfBand(destination.WrittenMemory);
    }
}
