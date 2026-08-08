// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Buffers;
global using System.Buffers.Text;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Diagnostics.Contracts;
global using System.Globalization;
global using System.Runtime.CompilerServices;
global using System.Runtime.ExceptionServices;
global using System.Runtime.InteropServices;
global using System.Runtime.Versioning;
global using System.Text;

global using SharpVision.Terminal;
global using SharpVision.Terminal.Abstractions;
global using SharpVision.Terminal.Capabilities;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Input;
global using SharpVision.Terminal.Multiplexing;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Terminfo;
global using SharpVision.Terminal.Unicode;

global using PublicAPI = JetBrains.Annotations.PublicAPIAttribute;
