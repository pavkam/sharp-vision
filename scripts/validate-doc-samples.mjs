import { execFile } from "node:child_process";
import { mkdtemp, readdir, readFile, rm, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);

// ".claude" holds Claude Code's own worktrees, each a full checkout of this repo pinned at some
// older commit. Walking into one makes every doc validator re-check that snapshot's stale docs
// against today's API and fail, so a developer with a worktree open cannot run `make lint`.
const ignoredDirectories = new Set([
  ".claude",
  ".git",
  ".worktrees",
  "artifacts",
  "bin",
  "node_modules",
  "obj",
]);

const csharpFence = /^\s{0,3}```csharp\s*$/u;
const closingFence = /^\s{0,3}```\s*$/u;
const usingLine = /^\s*using\s+[\w.]+\s*;\s*$/u;
const typeDeclarationKeyword = /\b(?:class|struct|interface|enum|record)\b/u;
const memberModifierStart =
  /^(?:public\b|internal\b|private\b|protected\b|static\b|sealed\b|abstract\b|virtual\b|override\b|async\b|partial\b)/u;
const maxStubIterations = 8;

// A `dynamic` stub compiles for plain property/method access, but data-binding's
// `Bind`/`BindProperty`/`BindItems`/`BindSelection` extension methods take
// lambda or expression-tree parameters, and C# forbids passing a lambda to a
// dynamically dispatched call (CS1977) - and a dynamic *argument* makes the
// whole call dynamic even when the receiver is a concrete type. Names that
// participate in those calls need a real stub type instead. This table is the
// "naming convention" the harness enforces: an unresolved name not listed
// here falls back to `dynamic`, which is sufficient for everything else in
// the doc tree today.
const knownStubs = new Map([
  ["input", "internal readonly TextInput input = new TextInput();"],
  ["active", "internal readonly CheckBox active = new CheckBox();"],
  ["label", "internal readonly Text label = new Text();"],
  ["list", "internal readonly ListView list = new ListView();"],
  ["control", "internal readonly Button control = new Button();"],
  ["customer", "internal readonly __ExampleModel customer = new __ExampleModel();"],
  ["settings", "internal readonly __ExampleModel settings = new __ExampleModel();"],
  ["viewModel", "internal readonly __ExampleModel viewModel = new __ExampleModel();"],
  ["_status", "internal readonly Text _status = new Text();"],
  ["_list", "internal readonly ListView _list = new ListView();"],
  ["clock", "internal readonly Text clock = new Text();"],
]);

const exampleModelDeclaration = `
    internal sealed class __ExampleAddress
    {
        public string City { get; set; } = string.Empty;
    }

    internal sealed class __ExampleModel
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public __ExampleAddress Address { get; set; } = new __ExampleAddress();
        public IReadOnlyList<object> Results { get; set; } = [];
        public object? SelectedResult { get; set; }
    }
`;

// Fixed harness using list. Kept as small as the doc tree actually needs -
// a broad list risks CS0104 ambiguity between same-named types in different
// namespaces. Extend only when a real
// doc sample needs a namespace this list is missing.
const harnessUsings = [
  "System",
  "System.Collections.Generic",
  "System.Diagnostics.CodeAnalysis",
  "System.IO",
  "System.Linq",
  "System.Text",
  "System.Threading",
  "System.Threading.Tasks",
  "SharpVision",
  "SharpVision.Controls",
  "SharpVision.Controls.Charts",
  "SharpVision.Controls.Collections",
  "SharpVision.Controls.Display",
  "SharpVision.Controls.Input",
  "SharpVision.Controls.Layout",
  "SharpVision.Controls.Scrolling",
  "SharpVision.DataBinding",
  "SharpVision.Dialogs",
  "SharpVision.Fonts",
  "SharpVision.Input",
  "SharpVision.Layout",
  "SharpVision.Menus",
  "SharpVision.Navigation",
  "SharpVision.Popups",
  "SharpVision.Runtime",
  "SharpVision.Scrolling",
  "SharpVision.Styling",
  "SharpVision.Surfaces",
  "SharpVision.Terminal.Geometry",
  "SharpVision.Terminal.Protocols",
  "SharpVision.Terminal.Rendering",
  "SharpVision.Text",
  "SharpVision.Threading",
  "SharpVision.Windows",
];

// Every real consumer project in this repo (examples/Showcase, TextEditor,
// Snake) declares this exact alias, since SharpVision.Controls.Display.Canvas
// (a control) and SharpVision.Terminal.Rendering.TerminalCanvas (the render surface
// OnRenderContent receives) share a bare name once both namespaces are in
// scope. The harness needs the same alias for the same reason.
const harnessAliases = ["TerminalCanvas = SharpVision.Terminal.Rendering.TerminalCanvas"];

/**
 * Extracts every fenced ```csharp block from Markdown content.
 * @param {string} content
 * @returns {{code: string, startLine: number}[]}
 */
export function extractCSharpBlocks(content) {
  const lines = content.split("\n");
  const blocks = [];
  let index = 0;

  while (index < lines.length) {
    if (!csharpFence.test(lines[index])) {
      index++;
      continue;
    }

    const startLine = index + 1;
    const bodyLines = [];
    index++;

    while (index < lines.length && !closingFence.test(lines[index])) {
      bodyLines.push(lines[index]);
      index++;
    }

    blocks.push({ code: bodyLines.join("\n"), startLine });
    index++;
  }

  return blocks;
}

/**
 * Strips `using X;` lines from a snippet - the harness supplies the using
 * list; a snippet never declares its own.
 * @param {string} code
 * @returns {string}
 */
export function stripUsingLines(code) {
  return code
    .split("\n")
    .filter((line) => !usingLine.test(line))
    .join("\n");
}

/**
 * Scans a using-stripped snippet body line by line, locating the first
 * non-blank/non-comment line and the first line (if any) that looks like a
 * type declaration signature. Shared by `classifyBlock` and
 * `splitProgramBlock` so they never disagree about where the type
 * declaration starts.
 * @param {string} body
 * @returns {{lines: string[], firstSignificantIndex: number, typeLineIndex: number}}
 */
function scanBlockLines(body) {
  const lines = body.split("\n");
  let firstSignificantIndex = -1;
  let typeLineIndex = -1;

  for (let index = 0; index < lines.length; index++) {
    const trimmed = lines[index].trim();

    if (trimmed.length === 0 || trimmed.startsWith("//")) {
      continue;
    }

    if (firstSignificantIndex === -1) {
      firstSignificantIndex = index;
    }

    if (typeDeclarationKeyword.test(trimmed)) {
      typeLineIndex = index;
      break;
    }
  }

  return { lines, firstSignificantIndex, typeLineIndex };
}

/**
 * Classifies a using-stripped snippet body into one of four shapes:
 * - "declaration": a complete top-level type - the first significant line
 *   already is the `class`/`struct`/`record`/`interface`/`enum` signature.
 *   Compiled as-is at namespace scope.
 * - "program": a real `Program.cs`-shaped sample - one or more executable
 *   top-level statements followed by a trailing type declaration (e.g. a
 *   walkthrough's `var status = await ...; internal sealed class Foo {}`).
 *   The statements must compile as this file's single top-level-statements
 *   entry point, not as namespace-scoped members.
 * - "member": a signature-shaped fragment (starts with an access or method
 *   modifier) that is not a type declaration - a method or property meant
 *   to be read as "add this to your own control/screen subclass". Compiled
 *   as a member of the shared harness class, which derives from `Screen` so
 *   `override`/protected-member snippets resolve.
 * - "fragment": everything else - ordinary statements, compiled as an
 *   instance method body on the same shared harness class, so implicit-this
 *   calls to inherited members (`AddHandler`, `SetAppearance`, ...) resolve
 *   the same way they would inside a real control.
 * @param {string} body
 * @returns {"declaration" | "program" | "member" | "fragment"}
 */
export function classifyBlock(body) {
  const { lines, firstSignificantIndex, typeLineIndex } = scanBlockLines(body);

  if (firstSignificantIndex === -1) {
    return "fragment";
  }

  if (typeLineIndex === firstSignificantIndex) {
    return "declaration";
  }

  if (typeLineIndex !== -1) {
    return "program";
  }

  return memberModifierStart.test(lines[firstSignificantIndex].trim()) ? "member" : "fragment";
}

/**
 * Splits a "program"-classified block into its leading top-level statements
 * and its trailing type declaration(s).
 * @param {string} body
 * @returns {{statements: string, declaration: string}}
 */
export function splitProgramBlock(body) {
  const { lines, typeLineIndex } = scanBlockLines(body);

  return {
    statements: lines.slice(0, typeLineIndex).join("\n"),
    declaration: lines.slice(typeLineIndex).join("\n"),
  };
}

async function collectMarkdownFiles(root) {
  const results = [];

  async function walk(directory) {
    const entries = await readdir(directory, { withFileTypes: true });

    for (const entry of entries) {
      if (ignoredDirectories.has(entry.name)) {
        continue;
      }

      const path = join(directory, entry.name);

      if (entry.isDirectory()) {
        await walk(path);
      } else if (entry.isFile() && entry.name.endsWith(".md")) {
        results.push(path);
      }
    }
  }

  await walk(root);
  return results.sort();
}

function sanitizeIdentifier(value) {
  const sanitized = value.replace(/[^A-Za-z0-9_]/gu, "_");
  return /^[A-Za-z_]/u.test(sanitized) ? sanitized : `_${sanitized}`;
}

/**
 * Renders one markdown file's blocks into a single compilable C# source.
 * Declaration-tier blocks become top-level types. Member- and fragment-tier
 * blocks all become members of one shared instance class deriving from
 * `Screen`, so `override`/protected-member snippets and implicit-`this`
 * fragments (`AddHandler(...)`, `SetAppearance(...)`) resolve the same way
 * they would inside a real control, without per-block guessing about which
 * base type a given snippet needs. A "program"-tier block contributes its
 * leading statements as this file's single top-level-statements entry point
 * (emitted before the namespace) and its trailing type declaration alongside
 * the other declaration-tier blocks.
 * @param {string} fileLabel
 * @param {{code: string, startLine: number}[]} blocks
 * @param {Set<string>} stubNames
 * @param {Set<string>} stubTypeNames
 * @returns {{source: string, lineMap: Map<number, number>}}
 */
export function renderCompilationUnit(fileLabel, blocks, stubNames, stubTypeNames) {
  const namespaceName = `Docs.${sanitizeIdentifier(fileLabel)}`;
  const declarationBlocks = [];
  const memberBlocks = [];
  const fragmentBlocks = [];
  const programStatementBlocks = [];

  for (const block of blocks) {
    const stripped = stripUsingLines(block.code);
    const kind = classifyBlock(stripped);

    if (kind === "declaration") {
      declarationBlocks.push({ code: stripped, startLine: block.startLine });
    } else if (kind === "member") {
      memberBlocks.push({ code: stripped, startLine: block.startLine });
    } else if (kind === "program") {
      const { statements, declaration } = splitProgramBlock(stripped);
      declarationBlocks.push({ code: declaration, startLine: block.startLine });
      programStatementBlocks.push({ code: statements, startLine: block.startLine });
    } else {
      fragmentBlocks.push({ code: stripped, startLine: block.startLine });
    }
  }

  const outputLines = [];
  const lineMap = new Map();

  function emit(text, sourceStartLine) {
    const textLines = text.split("\n");

    for (let offset = 0; offset < textLines.length; offset++) {
      outputLines.push(textLines[offset]);

      if (sourceStartLine !== undefined) {
        lineMap.set(outputLines.length, sourceStartLine + offset);
      }
    }
  }

  emit("// Auto-generated by scripts/validate-doc-samples.mjs - DO NOT EDIT.");

  for (const namespaceUsing of harnessUsings) {
    emit(`using ${namespaceUsing};`);
  }

  for (const alias of harnessAliases) {
    emit(`using ${alias};`);
  }

  emit("");
  emit(`namespace ${namespaceName}`);
  emit("{");

  for (const declaration of declarationBlocks) {
    emit(declaration.code, declaration.startLine);
    emit("");
  }

  // A "program" block's leading statements can't compile as literal
  // top-level statements here - csc rejects those outside /target:exe
  // (CS8805), and only one file per compilation may have them anyway. Each
  // gets its own static wrapper method instead, matching what top-level
  // statements desugar to.
  for (let index = 0; index < programStatementBlocks.length; index++) {
    const program = programStatementBlocks[index];
    const returnsValue = /\breturn\s+\S/u.test(program.code);
    const taskType = returnsValue
      ? "global::System.Threading.Tasks.Task<int>"
      : "global::System.Threading.Tasks.Task";

    emit(`    internal static class __Program_${index}`);
    emit("    {");
    emit(`        internal static async ${taskType} Main()`);
    emit("        {");
    emit(program.code, program.startLine);
    emit("        }");
    emit("    }");
    emit("");
  }

  emit(exampleModelDeclaration);

  // Undeclared types referenced via `new X()` (CS0246) get a minimal Screen
  // subclass so they satisfy the common `ConsoleApplication.RunAsync(Screen)`
  // / `CreateBuilder(Screen)` shape - the only pattern observed today.
  for (const typeName of [...stubTypeNames].sort()) {
    emit(`    internal sealed class ${sanitizeIdentifier(typeName)} : Screen { }`);
  }

  emit("    internal sealed class __Harness : Screen");
  emit("    {");

  for (const name of [...stubNames].sort()) {
    const known = knownStubs.get(name);
    emit(`        ${known ?? `internal dynamic ${sanitizeIdentifier(name)} = default!;`}`);
  }

  for (const member of memberBlocks) {
    emit(member.code, member.startLine);
    emit("");
  }

  for (let index = 0; index < fragmentBlocks.length; index++) {
    const fragment = fragmentBlocks[index];
    emit(`        internal async global::System.Threading.Tasks.Task Fragment_${index}()`);
    emit("        {");
    emit(fragment.code, fragment.startLine);
    emit("        }");
    emit("");
  }

  emit("    }");
  emit("}");

  return { source: outputLines.join("\n"), lineMap };
}

function parseUndeclaredNames(compilerOutput) {
  const names = new Set();
  const pattern = /error CS0103: The name '([^']+)' does not exist in the current context/gu;
  let match;

  while ((match = pattern.exec(compilerOutput)) !== null) {
    names.add(match[1]);
  }

  return names;
}

function parseUndeclaredTypeNames(compilerOutput) {
  const names = new Set();
  const pattern =
    /error CS0246: The type or namespace name '([^']+)' could not be found/gu;
  let match;

  while ((match = pattern.exec(compilerOutput)) !== null) {
    names.add(match[1]);
  }

  return names;
}

function translateDiagnostics(compilerOutput, sourcePath, lineMap, fileLabel) {
  const pattern = new RegExp(
    `${sourcePath.replace(/[.*+?^${}()|[\]\\]/gu, "\\$&")}\\((\\d+),\\d+\\): (error [^\\r\\n]+)`,
    "gu",
  );
  const translated = [];
  let match;

  while ((match = pattern.exec(compilerOutput)) !== null) {
    const generatedLine = Number(match[1]);
    const sourceLine = lineMap.get(generatedLine);
    const location = sourceLine === undefined ? `${fileLabel} (generated)` : `${fileLabel}:${sourceLine}`;
    translated.push(`${location} ${match[2]}`);
  }

  return translated.length > 0 ? translated : [compilerOutput.trim()];
}

async function resolveDotnetInstallation() {
  const configuredRoot = process.env.DOTNET_ROOT;
  const configuredExecutable = configuredRoot === undefined
    ? undefined
    : join(configuredRoot, "dotnet");
  const dotnetPath = configuredExecutable !== undefined && existsSync(configuredExecutable)
    ? configuredExecutable
    : "dotnet";
  const { stdout: versionOutput } = await execFileAsync(dotnetPath, ["--version"], {
    cwd: process.cwd(),
  });
  const version = versionOutput.trim();

  if (configuredRoot !== undefined) {
    return { dotnetPath, dotnetRoot: configuredRoot, version };
  }

  const { stdout: sdkOutput } = await execFileAsync(dotnetPath, ["--list-sdks"], {
    cwd: process.cwd(),
  });
  const sdkEntry = sdkOutput
    .split(/\r?\n/gu)
    .map((line) => /^(\S+)\s+\[(.+)\]\s*$/u.exec(line))
    .find((match) => match?.[1] === version);

  if (sdkEntry === undefined) {
    throw new Error(`The active .NET SDK ${version} was not present in dotnet --list-sdks.`);
  }

  return { dotnetPath, dotnetRoot: dirname(sdkEntry[2]), version };
}

async function findReferenceAssemblies(dotnetRoot) {
  const refPacksRoot = join(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");

  if (!existsSync(refPacksRoot)) {
    throw new Error(`Reference assembly pack not found under ${refPacksRoot}.`);
  }

  const versions = (await readdir(refPacksRoot)).filter((name) => name.startsWith("10."));

  if (versions.length === 0) {
    throw new Error(`No net10.0 reference pack found under ${refPacksRoot}.`);
  }

  const latest = versions.sort((left, right) => left.localeCompare(right, "en", { numeric: true })).at(-1);
  const refDirectory = join(refPacksRoot, latest, "ref", "net10.0");
  const files = await readdir(refDirectory);
  return files.filter((name) => name.endsWith(".dll")).map((name) => join(refDirectory, name));
}

async function findCscPath(dotnetRoot, pinnedVersion) {
  const sdkRoot = join(dotnetRoot, "sdk");

  if (!existsSync(sdkRoot)) {
    throw new Error(`.NET SDK directory not found under ${sdkRoot}.`);
  }

  // global.json pins the exact SDK version this repo builds with. A naive
  // string sort of directory names does not order version numbers correctly
  // (e.g. "9.0.312" sorts after "10.0.203"), which previously picked an
  // older SDK's csc.dll that predates C# 14 support entirely.
  const pinnedCandidate = join(sdkRoot, pinnedVersion, "Roslyn", "bincore", "csc.dll");

  if (existsSync(pinnedCandidate)) {
    return pinnedCandidate;
  }

  throw new Error(`csc.dll not found for the pinned SDK version ${pinnedVersion} under ${sdkRoot}.`);
}

async function compileUnit(cscPath, dotnetPath, referenceAssemblies, sharpVisionAssemblies, sourcePath) {
  const args = [
    cscPath,
    "/nologo",
    "/target:library",
    "/langversion:14.0",
    "/nullable:enable",
    `/out:${sourcePath}.dll`,
    ...referenceAssemblies.map((path) => `/reference:${path}`),
    ...sharpVisionAssemblies.map((path) => `/reference:${path}`),
    sourcePath,
  ];

  try {
    await execFileAsync(dotnetPath, args, { maxBuffer: 32 * 1024 * 1024 });
    return { success: true, output: "" };
  } catch (error) {
    return { success: false, output: `${error.stdout ?? ""}${error.stderr ?? ""}` };
  }
}

/**
 * Compiles every ```csharp block in the Markdown tree rooted at `root`,
 * one shared compilation per file so declaration blocks stay visible to
 * later fragments in the same document. Fragment blocks that reference an
 * undeclared simple name are retried with that name stubbed as a dynamic
 * field, up to `maxStubIterations` times, before being reported as a
 * genuine failure.
 * @param {string} root
 * @param {string[]} sharpVisionAssemblies
 * @returns {Promise<string[]>}
 */
export async function validateDocSamples(root, sharpVisionAssemblies) {
  const files = await collectMarkdownFiles(root);
  const errors = [];
  const { dotnetPath, dotnetRoot, version } = await resolveDotnetInstallation();
  const referenceAssemblies = await findReferenceAssemblies(dotnetRoot);
  const cscPath = await findCscPath(dotnetRoot, version);
  const workingDirectory = await mkdtemp(join(tmpdir(), "sharpvision-docs-samples-"));
  let totalBlocks = 0;

  try {
    for (const file of files) {
      const content = await readFile(file, "utf8");
      const blocks = extractCSharpBlocks(content);

      if (blocks.length === 0) {
        continue;
      }

      totalBlocks += blocks.length;
      const fileLabel = relative(root, file);
      const stubNames = new Set();
      const stubTypeNames = new Set();
      let lastOutput = "";
      let compiled = false;

      for (let attempt = 0; attempt <= maxStubIterations; attempt++) {
        const { source, lineMap } = renderCompilationUnit(fileLabel, blocks, stubNames, stubTypeNames);
        const sourcePath = join(workingDirectory, `${sanitizeIdentifier(fileLabel)}.cs`);
        await writeFile(sourcePath, source, "utf8");

        const result = await compileUnit(
          cscPath,
          dotnetPath,
          referenceAssemblies,
          sharpVisionAssemblies,
          sourcePath,
        );

        if (result.success) {
          compiled = true;
          break;
        }

        lastOutput = result.output;
        const undeclaredNames = parseUndeclaredNames(result.output);
        const undeclaredTypes = parseUndeclaredTypeNames(result.output);
        const newNames = [...undeclaredNames].filter((name) => !stubNames.has(name));
        const newTypeNames = [...undeclaredTypes].filter((name) => !stubTypeNames.has(name));

        if (newNames.length === 0 && newTypeNames.length === 0) {
          errors.push(...translateDiagnostics(result.output, sourcePath, lineMap, fileLabel));
          break;
        }

        for (const name of newNames) {
          stubNames.add(name);
        }

        for (const name of newTypeNames) {
          stubTypeNames.add(name);
        }
      }

      if (!compiled && lastOutput.length > 0 && !errors.some((error) => error.startsWith(fileLabel))) {
        // Exhausted the stub-iteration budget without success and without
        // having already recorded a translated diagnostic above.
        errors.push(`${fileLabel} failed to compile after ${maxStubIterations} stub iterations.`);
      }
    }
  } finally {
    await rm(workingDirectory, { recursive: true, force: true });
  }

  return { errors, totalBlocks };
}

async function main() {
  const sharpVisionRoot = resolve(process.cwd(), "src", "SharpVision", "bin", "Release", "net10.0");
  const figletFontsRoot = resolve(
    process.cwd(),
    "src",
    "SharpVision.FigletFonts",
    "bin",
    "Release",
    "net10.0",
  );
  const sharpVisionAssemblies = [
    join(sharpVisionRoot, "SharpVision.dll"),
    join(sharpVisionRoot, "SharpVision.Terminal.dll"),
    join(figletFontsRoot, "SharpVision.FigletFonts.dll"),
  ];

  for (const assembly of sharpVisionAssemblies) {
    if (!existsSync(assembly)) {
      console.error(
        `${assembly} is missing. Build SharpVision and SharpVision.FigletFonts in Release before running this check.`,
      );
      process.exitCode = 1;
      return;
    }
  }

  const { errors, totalBlocks } = await validateDocSamples(process.cwd(), sharpVisionAssemblies);

  if (errors.length === 0) {
    console.log(`All ${totalBlocks} documentation C# samples compile.`);
    return;
  }

  for (const error of errors) {
    console.error(error);
  }

  process.exitCode = 1;
}

const invokedPath = process.argv[1] === undefined
  ? undefined
  : pathToFileURL(resolve(process.argv[1])).href;

if (invokedPath === import.meta.url) {
  await main();
}
