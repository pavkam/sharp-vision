// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Terminal.Capabilities;

/// <summary>Encodes implemented output protocols and posts them through the ordered write path.</summary>
internal sealed class TerminalServices: ITerminalServices, IBell, IClipboard
{
    private readonly Application _application;
    private readonly Lock _programGate = new();
    private ProgramExpander? _expander;

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
    public bool IsTitleSupported
    {
        get
        {
            if (UsesAnsiTitle)
            {
                return true;
            }

            lock (_programGate)
            {
                return Expander().HasPair("TS", "fsl");
            }
        }
    }

    /// <inheritdoc/>
    bool IClipboard.IsSupported =>
        _application.Capabilities.Osc52.IsSupported || _application.Capabilities.KittyClipboard.IsSupported;

    /// <inheritdoc/>
    bool IBell.IsSupported
    {
        get
        {
            lock (_programGate)
            {
                return Expander().Has("bel");
            }
        }
    }

    /// <inheritdoc/>
    public void Ring()
    {
        var destination = new ArrayBufferWriter<byte>();

        lock (_programGate)
        {
            if (!Expander().TryWrite("bel", [], destination))
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

            if (!UsesAnsiTitle)
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
            if (!Expander().TryExpandPair("TS", "fsl", out prefix, out suffix))
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

    private ProgramExpander Expander()
    {
        // The expander owns bounded interpreter state, so it is rebuilt only when the active
        // profile would actually produce different output. Capability refinement replaces the
        // profile object on every negotiation step without touching the compiled description.
        Debug.Assert(_programGate.IsHeldByCurrentThread, "Program expansion is serialized by its owner.");
        var profile = _application.TerminalProfile;

        if (_expander is null || !_expander.AppliesTo(profile))
        {
            _expander = profile.CreateProgramExpander();
        }

        return _expander;
    }

    private bool UsesAnsiTitle =>
        _application.TerminalProfile.IsAnsiCompatibility ||
        (Description.Origin == DescriptionOrigin.BuiltIn &&
         string.Equals(Description.Name, "windows-vt", StringComparison.Ordinal));

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
