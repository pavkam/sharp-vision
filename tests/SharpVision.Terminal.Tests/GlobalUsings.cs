// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Buffers;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Runtime.InteropServices;
global using System.Runtime.Versioning;
global using System.Text;
global using System.Threading.Channels;

global using Microsoft.Win32.SafeHandles;

global using SharpVision.Terminal.Clipboard;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Rendering;
global using SharpVision.Terminal.Tests.Support;
global using SharpVision.Terminal.Transport;
global using SharpVision.Terminal.Unicode;

global using Shouldly;

global using CapabilitySupport = SharpVision.Terminal.Capabilities.Support;
global using CellMetrics = SharpVision.Terminal.Geometry.Metrics;
global using Encoder = SharpVision.Terminal.Rendering.Encoder;
global using FrameEncoder = SharpVision.Terminal.Rendering.Encoder;
global using GraphicsImage = SharpVision.Terminal.Graphics.Image;
global using InputDecoder = SharpVision.Terminal.Input.Decoder;
global using InputText = SharpVision.Terminal.Input.Text;
global using RenderingMetrics = SharpVision.Terminal.Rendering.Metrics;
global using RenderMetrics = SharpVision.Terminal.Rendering.Metrics;
global using RuntimeOptions = SharpVision.Terminal.Runtime.Options;
global using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;
