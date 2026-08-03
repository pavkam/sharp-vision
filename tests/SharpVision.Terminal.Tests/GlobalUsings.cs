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

global using SharpVision.Terminal.Abstractions;
global using SharpVision.Terminal.Capabilities;
global using SharpVision.Terminal.Clipboard;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Graphics.Backends;
global using SharpVision.Terminal.Input;
global using SharpVision.Terminal.Multiplexing;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Rendering;
global using SharpVision.Terminal.Runtime;
global using SharpVision.Terminal.Terminfo;
global using SharpVision.Terminal.Terminfo.Ncurses;
global using SharpVision.Terminal.Terminfo.Windows;
global using SharpVision.Terminal.Tests.Performance;
global using SharpVision.Terminal.Tests.Support;
global using SharpVision.Terminal.Unicode;
global using SharpVision.Terminal.Xterm;

global using Shouldly;

global using CapabilitySupport = SharpVision.Terminal.Capabilities.CapabilitySupport;
global using GraphicsImage = SharpVision.Terminal.Graphics.ImageSource;
