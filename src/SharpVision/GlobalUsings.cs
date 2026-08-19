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
global using SharpVision.Scrolling;
global using SharpVision.Styling;
global using SharpVision.Terminal.Abstractions;
global using SharpVision.Terminal.Capabilities;
global using SharpVision.Terminal.Clipboard;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Rendering;
global using SharpVision.Terminal.Terminfo;
global using SharpVision.Terminal.Unicode;
global using SharpVision.Terminal.Xterm;
global using SharpVision.Threading;

global using BackgroundMode = SharpVision.Terminal.Rendering.BackgroundMode;
global using KeyAction = SharpVision.Terminal.Input.KeyAction;
global using NotifyPropertyChangedInvocator = JetBrains.Annotations.NotifyPropertyChangedInvocatorAttribute;
global using PublicAPI = JetBrains.Annotations.PublicAPIAttribute;
global using Pure = JetBrains.Annotations.PureAttribute;
global using TerminalStyle = SharpVision.Terminal.Rendering.CellStyle;
global using VisualState = SharpVision.Styling.VisualState;
