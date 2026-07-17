// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Buffers;
global using System.Collections;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Text;
global using System.Text.Json;

global using SharpVision.Controls;
global using SharpVision.Input;
global using SharpVision.Layout;
global using SharpVision.Styling;
global using SharpVision.Terminal.Clipboard;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Unicode;
global using SharpVision.Threading;

global using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
global using KeyAction = SharpVision.Terminal.Input.Action;
global using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
global using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
global using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;
global using TerminalStyle = SharpVision.Terminal.Rendering.CellStyle;
global using VisualState = SharpVision.Styling.VisualState;
