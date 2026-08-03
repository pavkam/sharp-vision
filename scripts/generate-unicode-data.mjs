import { createHash } from "node:crypto";
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const sourceDirectory = path.join(root, "extern", "unicode", "17.0.0");
const outputDirectory = path.join(
  root,
  "src",
  "SharpVision.Terminal",
  "Unicode",
);
const obsoleteOutputPaths = [
  path.join(outputDirectory, "Data.g.cs"),
  path.join(outputDirectory, "PropertyRange.cs"),
];
const baseUrl = "https://www.unicode.org/Public/17.0.0/ucd";
const sources = [
  {
    name: "DerivedCoreProperties.txt",
    url: `${baseUrl}/DerivedCoreProperties.txt`,
    sha256: "24c7fed1195c482faaefd5c1e7eb821c5ee1fb6de07ecdbaa64b56a99da22c08",
    versionMarker: "17.0.0",
  },
  {
    name: "EastAsianWidth.txt",
    url: `${baseUrl}/EastAsianWidth.txt`,
    sha256: "ea7ce50f3444a050333448dffef1cadd9325af55cbb764b4a2280faf52170a33",
    versionMarker: "17.0.0",
  },
  {
    name: "emoji-data.txt",
    url: `${baseUrl}/emoji/emoji-data.txt`,
    sha256: "2cb2bb9455cda83e8481541ecf5b6dfda66a3bb89efa3fa7c5297eccf607b72b",
    versionMarker: "Version: 17.0",
  },
  {
    name: "GraphemeBreakProperty.txt",
    url: `${baseUrl}/auxiliary/GraphemeBreakProperty.txt`,
    sha256: "d6b51d1d2ae5c33b451b7ed994b48f1f4dc62b2272a5831e7fd418514a6bae89",
    versionMarker: "17.0.0",
  },
  {
    name: "GraphemeBreakTest.txt",
    url: `${baseUrl}/auxiliary/GraphemeBreakTest.txt`,
    sha256: "e2d134d2c52919bace503ebb6a551c1855fe1a1faec18478c78fff254a1793ec",
    versionMarker: "17.0.0",
  },
  {
    name: "ReadMe.txt",
    url: `${baseUrl}/ReadMe.txt`,
    sha256: "9fe1a90bd32659d7953616283dc2bffaa165518aae9ace026040c42c559ba606",
    versionMarker: "17.0.0",
  },
  {
    name: "UnicodeData.txt",
    url: `${baseUrl}/UnicodeData.txt`,
    sha256: "2e1efc1dcb59c575eedf5ccae60f95229f706ee6d031835247d843c11d96470c",
  },
];

const graphemeNames = new Map([
  ["Prepend", "Prepend"],
  ["CR", "Cr"],
  ["LF", "Lf"],
  ["Control", "Control"],
  ["Extend", "Extend"],
  ["Regional_Indicator", "RegionalIndicator"],
  ["SpacingMark", "SpacingMark"],
  ["L", "L"],
  ["V", "V"],
  ["T", "T"],
  ["LV", "Lv"],
  ["LVT", "Lvt"],
  ["ZWJ", "Zwj"],
]);
const indicNames = new Map([
  ["Linker", "Linker"],
  ["Consonant", "Consonant"],
  ["Extend", "Extend"],
]);
const widthNames = new Map([
  ["A", "Ambiguous"],
  ["F", "Fullwidth"],
  ["H", "Halfwidth"],
  ["N", "Neutral"],
  ["Na", "Narrow"],
  ["W", "Wide"],
]);
const graphemeValues = [
  "Other",
  "Prepend",
  "Cr",
  "Lf",
  "Control",
  "Extend",
  "RegionalIndicator",
  "SpacingMark",
  "L",
  "V",
  "T",
  "Lv",
  "Lvt",
  "Zwj",
];
const indicValues = ["None", "Linker", "Consonant", "Extend"];
const widthValues = ["Neutral", "Ambiguous", "Fullwidth", "Halfwidth", "Narrow", "Wide"];

function digest(value) {
  return createHash("sha256").update(value).digest("hex");
}

function parseRange(value) {
  const [start, end = start] = value.trim().split("..");
  return {
    start: Number.parseInt(start, 16),
    end: Number.parseInt(end, 16),
  };
}

function parseProperties(content, select) {
  const ranges = [];

  for (const line of content.split(/\r?\n/u)) {
    const value = line.split("#", 1)[0].trim();

    if (value.length === 0) {
      continue;
    }

    const fields = value.split(";").map((item) => item.trim());
    const property = select(fields);

    if (property === undefined) {
      continue;
    }

    ranges.push({ ...parseRange(fields[0]), property });
  }

  ranges.sort((left, right) => left.start - right.start || left.end - right.end);

  const merged = [];

  for (const range of ranges) {
    const previous = merged.at(-1);

    if (
      previous !== undefined &&
      previous.property === range.property &&
      previous.end + 1 === range.start
    ) {
      previous.end = range.end;
    } else {
      merged.push({ ...range });
    }
  }

  return merged;
}

function parseCanonicalBases(content) {
  const decompositions = new Map();

  for (const line of content.split(/\r?\n/u)) {
    const fields = line.split(";");

    if (fields.length < 6 || fields[5].length === 0 || fields[5].startsWith("<")) {
      continue;
    }

    const scalar = Number.parseInt(fields[0], 16);
    const first = Number.parseInt(fields[5].split(" ", 1)[0], 16);
    decompositions.set(scalar, first);
  }

  const resolve = (scalar) => {
    const next = decompositions.get(scalar);
    return next === undefined ? scalar : resolve(next);
  };

  return [...decompositions.keys()]
    .sort((left, right) => left - right)
    .map((scalar) => ({ start: scalar, end: scalar, property: resolve(scalar) }));
}

function parseAssigned(content) {
  const ranges = [];
  let pendingStart;

  for (const line of content.split(/\r?\n/u)) {
    const fields = line.split(";");

    if (fields.length < 3) {
      continue;
    }

    const scalar = Number.parseInt(fields[0], 16);
    const name = fields[1];

    if (name.endsWith(", First>")) {
      pendingStart = scalar;
      continue;
    }

    if (name.endsWith(", Last>")) {
      if (pendingStart === undefined) {
        throw new Error(`UnicodeData range ending at ${fields[0]} has no start.`);
      }

      ranges.push({ start: pendingStart, end: scalar, property: true });
      pendingStart = undefined;
      continue;
    }

    ranges.push({ start: scalar, end: scalar, property: true });
  }

  if (pendingStart !== undefined) {
    throw new Error("UnicodeData ends inside a First/Last range.");
  }

  const merged = [];

  for (const range of ranges) {
    const previous = merged.at(-1);

    if (previous !== undefined && previous.end + 1 === range.start) {
      previous.end = range.end;
    } else {
      merged.push({ ...range });
    }
  }

  return merged;
}

function validateRanges(name, ranges) {
  if (ranges.length === 0) {
    throw new Error(`${name} must contain at least one generated range.`);
  }

  for (let index = 0; index < ranges.length; index += 1) {
    const range = ranges[index];

    if (range.start < 0 || range.end < range.start || range.end > 0x10ffff) {
      throw new Error(`${name} contains an invalid Unicode scalar range.`);
    }

    if (index > 0 && ranges[index - 1].end >= range.start) {
      throw new Error(`${name} ranges must be strictly ordered and non-overlapping.`);
    }
  }
}

function formatSpan(name, values) {
  const encoded = values
    .flatMap((value) => {
      return [value & 0xffff, value >>> 16];
    })
    .map((value) => `\\u${value.toString(16).toUpperCase().padStart(4, "0")}`)
    .join("");

  return [
    `    private const string ${name}Data = "${encoded}";`,
    `    private static ReadOnlySpan<int> ${name} => MemoryMarshal.Cast<char, int>(${name}Data);`,
  ].join("\n");
}

function formatTable(name, ranges, valueOf) {
  return [
    formatSpan(`${name}Starts`, ranges.map((range) => range.start)),
    formatSpan(`${name}Ends`, ranges.map((range) => range.end)),
    formatSpan(`${name}Values`, ranges.map((range) => valueOf(range.property))),
  ].join("\n\n");
}

function generate(files) {
  const grapheme = parseProperties(
    files.get("GraphemeBreakProperty.txt"),
    (fields) => graphemeNames.get(fields[1]),
  );
  const indic = parseProperties(
    files.get("DerivedCoreProperties.txt"),
    (fields) => fields[1] === "InCB" ? indicNames.get(fields[2]) : undefined,
  );
  const width = parseProperties(
    files.get("EastAsianWidth.txt"),
    (fields) => widthNames.get(fields[1]),
  );
  const emoji = parseProperties(
    files.get("emoji-data.txt"),
    (fields) => fields[1] === "Emoji_Presentation" ? true : undefined,
  );
  const pictographic = parseProperties(
    files.get("emoji-data.txt"),
    (fields) => fields[1] === "Extended_Pictographic" ? true : undefined,
  );
  const canonicalBases = parseCanonicalBases(files.get("UnicodeData.txt"));
  const assigned = parseAssigned(files.get("UnicodeData.txt"));

  for (const [name, ranges] of [
    ["GraphemeBreak", grapheme],
    ["IndicConjunct", indic],
    ["EastAsianWidth", width],
    ["EmojiPresentation", emoji],
    ["ExtendedPictographic", pictographic],
    ["CanonicalBase", canonicalBases],
    ["Assigned", assigned],
  ]) {
    validateRanges(name, ranges);
  }

  const header = `// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// <auto-generated />
// Unicode 17.0.0; generated by scripts/generate-unicode-data.mjs.

namespace SharpVision.Terminal.Unicode;
`;

  const outputs = new Map();
  outputs.set("GraphemeBreak.cs", `${header}
/// <summary>Identifies a Unicode grapheme-cluster break property.</summary>
internal enum GraphemeBreak
{
    /// <summary>No explicit break property applies.</summary>
    Other,

    /// <summary>A prepended mark.</summary>
    Prepend,

    /// <summary>A carriage return.</summary>
    Cr,

    /// <summary>A line feed.</summary>
    Lf,

    /// <summary>A control or format scalar.</summary>
    Control,

    /// <summary>An extending scalar.</summary>
    Extend,

    /// <summary>A regional indicator.</summary>
    RegionalIndicator,

    /// <summary>A spacing combining mark.</summary>
    SpacingMark,

    /// <summary>A Hangul leading consonant.</summary>
    L,

    /// <summary>A Hangul vowel.</summary>
    V,

    /// <summary>A Hangul trailing consonant.</summary>
    T,

    /// <summary>A Hangul leading-vowel syllable.</summary>
    Lv,

    /// <summary>A Hangul leading-vowel-trailing syllable.</summary>
    Lvt,

    /// <summary>A zero-width joiner.</summary>
    Zwj
}
`);

  outputs.set("IndicConjunct.cs", `${header}
/// <summary>Identifies an Indic conjunct-break property.</summary>
internal enum IndicConjunct
{
    /// <summary>No Indic conjunct property applies.</summary>
    None,

    /// <summary>A conjunct linker.</summary>
    Linker,

    /// <summary>A conjunct consonant.</summary>
    Consonant,

    /// <summary>A conjunct extender.</summary>
    Extend
}
`);

  outputs.set("EastAsianWidth.cs", `${header}
/// <summary>Identifies a Unicode East Asian Width property.</summary>
internal enum EastAsianWidth
{
    /// <summary>A neutral-width scalar.</summary>
    Neutral,

    /// <summary>An ambiguous-width scalar.</summary>
    Ambiguous,

    /// <summary>A fullwidth scalar.</summary>
    Fullwidth,

    /// <summary>A halfwidth scalar.</summary>
    Halfwidth,

    /// <summary>A narrow scalar.</summary>
    Narrow,

    /// <summary>A wide scalar.</summary>
    Wide
}
`);

  outputs.set("UnicodeData.cs", `${header}
/// <summary>Provides allocation-free lookup over pinned Unicode 17 tables.</summary>
internal static class UnicodeData
{
    // Generated integers are packed into UTF-16 metadata and viewed as little-endian int spans.
    // This representation is intentionally limited to the little-endian .NET targets SharpVision supports.
${formatTable("GraphemeBreak", grapheme, (value) => graphemeValues.indexOf(value))}

${formatTable("IndicConjunct", indic, (value) => indicValues.indexOf(value))}

${formatTable("EastAsianWidth", width, (value) => widthValues.indexOf(value))}

${formatTable("EmojiPresentation", emoji, () => 1)}

${formatTable("ExtendedPictographic", pictographic, () => 1)}

${formatTable("CanonicalBase", canonicalBases, (value) => value)}

${formatTable("Assigned", assigned, () => 1)}

    extension(int scalar)
    {
        /// <summary>Gets the grapheme-break property for one valid scalar value.</summary>
        public GraphemeBreak GetGraphemeBreak() =>
            (GraphemeBreak) Find(GraphemeBreakStarts, GraphemeBreakEnds, GraphemeBreakValues, scalar, (int) GraphemeBreak.Other);

        /// <summary>Gets the Indic conjunct property for one valid scalar value.</summary>
        public IndicConjunct GetIndicConjunct() =>
            (IndicConjunct) Find(IndicConjunctStarts, IndicConjunctEnds, IndicConjunctValues, scalar, (int) IndicConjunct.None);

        /// <summary>Gets the East Asian Width property for one valid scalar value.</summary>
        public EastAsianWidth GetEastAsianWidth() =>
            (EastAsianWidth) Find(EastAsianWidthStarts, EastAsianWidthEnds, EastAsianWidthValues, scalar, (int) EastAsianWidth.Neutral);

        /// <summary>Gets whether one valid scalar has default emoji presentation.</summary>
        public bool IsEmojiPresentation() =>
            Find(EmojiPresentationStarts, EmojiPresentationEnds, EmojiPresentationValues, scalar, 0) != 0;

        /// <summary>Gets whether one valid scalar has the extended-pictographic property.</summary>
        public bool IsExtendedPictographic() =>
            Find(ExtendedPictographicStarts, ExtendedPictographicEnds, ExtendedPictographicValues, scalar, 0) != 0;

        /// <summary>Gets the recursively decomposed first scalar without allocating normalization storage.</summary>
        public int GetCanonicalBase() =>
            Find(CanonicalBaseStarts, CanonicalBaseEnds, CanonicalBaseValues, scalar, scalar);

        /// <summary>Gets whether a scalar is assigned in the pinned Unicode version.</summary>
        public bool IsAssigned() => Find(AssignedStarts, AssignedEnds, AssignedValues, scalar, 0) != 0;
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static int Find(
        ReadOnlySpan<int> starts,
        ReadOnlySpan<int> ends,
        ReadOnlySpan<int> values,
        int scalar,
        int fallback)
    {
        var low = 0;
        var high = starts.Length - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (scalar < starts[middle])
            {
                high = middle - 1;
            }
            else if (scalar > ends[middle])
            {
                low = middle + 1;
            }
            else
            {
                return values[middle];
            }
        }

        return fallback;
    }
}
`);

  return outputs;
}

async function loadSources() {
  const files = new Map();

  for (const source of sources) {
    const sourcePath = path.join(sourceDirectory, source.name);
    const content = await readFile(sourcePath, "utf8");

    if (
      (source.versionMarker !== undefined &&
        !content.includes(source.versionMarker)) ||
      digest(content) !== source.sha256
    ) {
      throw new Error(`${source.name} does not match the pinned Unicode 17 source.`);
    }

    files.set(source.name, content);
  }

  return files;
}

async function refresh() {
  await mkdir(sourceDirectory, { recursive: true });

  for (const source of sources) {
    const response = await fetch(source.url);

    if (!response.ok) {
      throw new Error(`Failed to fetch ${source.url}: ${response.status}.`);
    }

    const content = await response.text();

    if (digest(content) !== source.sha256) {
      throw new Error(`${source.name} did not match its pinned SHA-256.`);
    }

    await writeFile(path.join(sourceDirectory, source.name), content, "utf8");
  }
}

async function main() {
  const check = process.argv.includes("--check");

  if (process.argv.includes("--refresh")) {
    await refresh();
  }

  const outputs = generate(await loadSources());

  if (check) {
    for (const obsoleteOutputPath of obsoleteOutputPaths) {
      const obsolete = await readFile(obsoleteOutputPath, "utf8").catch(() => "");

      if (obsolete.length !== 0) {
        throw new Error("Unicode generated output is stale; run npm run generate:unicode.");
      }
    }

    for (const [name, output] of outputs) {
      const outputPath = path.join(outputDirectory, name);
      const current = await readFile(outputPath, "utf8").catch(() => "");

      if (current !== output) {
        throw new Error(`${name} is stale; run npm run generate:unicode.`);
      }
    }

    process.stdout.write("Unicode generated output is current.\n");
  } else {
    await mkdir(outputDirectory, { recursive: true });

    for (const obsoleteOutputPath of obsoleteOutputPaths) {
      await rm(obsoleteOutputPath, { force: true });
    }

    for (const [name, output] of outputs) {
      const outputPath = path.join(outputDirectory, name);
      await writeFile(outputPath, output, "utf8");
      process.stdout.write(`Generated ${path.relative(root, outputPath)}.\n`);
    }
  }
}

if (
  process.argv[1] !== undefined &&
  fileURLToPath(import.meta.url) === path.resolve(process.argv[1])
) {
  await main();
}
