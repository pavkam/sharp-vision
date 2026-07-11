import { createHash } from "node:crypto";
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const sourceDirectory = path.join(root, "data", "unicode", "17.0.0");
const outputDirectory = path.join(
  root,
  "src",
  "SharpVision.Terminal",
  "Unicode",
);
const legacyOutputPath = path.join(outputDirectory, "Data.g.cs");
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

function formatRange(range, enumName) {
  const property = enumName === undefined
    ? "1"
    : `(int)${enumName}.${range.property}`;
  return `        new(0x${range.start.toString(16).toUpperCase()}, 0x${range.end.toString(16).toUpperCase()}, ${property}),`;
}

function formatArray(fieldName, ranges, enumName) {
  return [
    `    private static readonly PropertyRange[] ${fieldName} =`,
    "    [",
    ...ranges.map((range) => formatRange(range, enumName)),
    "    ];",
  ].join("\n");
}

function formatValueArray(fieldName, ranges) {
  return [
    `    private static readonly PropertyRange[] ${fieldName} =`,
    "    [",
    ...ranges.map(
      (range) =>
        `        new(0x${range.start.toString(16).toUpperCase()}, 0x${range.end.toString(16).toUpperCase()}, 0x${range.property.toString(16).toUpperCase()}),`,
    ),
    "    ];",
  ].join("\n");
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

  const header = `// <auto-generated />
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
    Zwj,
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
    Extend,
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
    Wide,
}
`);

  outputs.set("PropertyRange.cs", `${header}
/// <summary>Stores one inclusive scalar range and its generated property value.</summary>
/// <param name="Start">The first Unicode scalar.</param>
/// <param name="End">The last Unicode scalar.</param>
/// <param name="Value">The generated enum or Boolean value.</param>
internal readonly record struct PropertyRange(int Start, int End, int Value);
`);

  outputs.set("Data.cs", `${header}
/// <summary>Provides allocation-free lookup over pinned Unicode 17 tables.</summary>
internal static class Data
{
${formatArray("_graphemeBreakRanges", grapheme, "GraphemeBreak")}

${formatArray("_indicConjunctRanges", indic, "IndicConjunct")}

${formatArray("_eastAsianWidthRanges", width, "EastAsianWidth")}

${formatArray("_emojiPresentationRanges", emoji)}

${formatArray("_extendedPictographicRanges", pictographic)}

${formatValueArray("_canonicalBaseRanges", canonicalBases)}

${formatArray("_assignedRanges", assigned)}

    /// <summary>Gets generated grapheme-break ranges for invariant validation.</summary>
    internal static ReadOnlySpan<PropertyRange> GraphemeBreakRanges => _graphemeBreakRanges;

    /// <summary>Gets generated Indic conjunct ranges for invariant validation.</summary>
    internal static ReadOnlySpan<PropertyRange> IndicConjunctRanges => _indicConjunctRanges;

    /// <summary>Gets generated East Asian Width ranges for invariant validation.</summary>
    internal static ReadOnlySpan<PropertyRange> EastAsianWidthRanges => _eastAsianWidthRanges;

    /// <summary>Gets generated emoji-presentation ranges for invariant validation.</summary>
    internal static ReadOnlySpan<PropertyRange> EmojiPresentationRanges => _emojiPresentationRanges;

    /// <summary>Gets generated extended-pictographic ranges for invariant validation.</summary>
    internal static ReadOnlySpan<PropertyRange> ExtendedPictographicRanges => _extendedPictographicRanges;

    /// <summary>Gets generated canonical-decomposition bases for invariant validation.</summary>
    internal static ReadOnlySpan<PropertyRange> CanonicalBaseRanges => _canonicalBaseRanges;

    /// <summary>Gets generated assigned-scalar ranges for invariant validation.</summary>
    internal static ReadOnlySpan<PropertyRange> AssignedRanges => _assignedRanges;

    /// <summary>Gets the grapheme-break property for one valid scalar value.</summary>
    internal static GraphemeBreak GetGraphemeBreak(int scalar) =>
        (GraphemeBreak)Find(_graphemeBreakRanges, scalar, (int)GraphemeBreak.Other);

    /// <summary>Gets the Indic conjunct property for one valid scalar value.</summary>
    internal static IndicConjunct GetIndicConjunct(int scalar) =>
        (IndicConjunct)Find(_indicConjunctRanges, scalar, (int)IndicConjunct.None);

    /// <summary>Gets the East Asian Width property for one valid scalar value.</summary>
    internal static EastAsianWidth GetEastAsianWidth(int scalar) =>
        (EastAsianWidth)Find(_eastAsianWidthRanges, scalar, (int)EastAsianWidth.Neutral);

    /// <summary>Gets whether one valid scalar has default emoji presentation.</summary>
    internal static bool IsEmojiPresentation(int scalar) =>
        Find(_emojiPresentationRanges, scalar, 0) != 0;

    /// <summary>Gets whether one valid scalar has the extended-pictographic property.</summary>
    internal static bool IsExtendedPictographic(int scalar) =>
        Find(_extendedPictographicRanges, scalar, 0) != 0;

    /// <summary>Gets the recursively decomposed first scalar without allocating normalization storage.</summary>
    internal static int GetCanonicalBase(int scalar) =>
        Find(_canonicalBaseRanges, scalar, scalar);

    /// <summary>Gets whether a scalar is assigned in the pinned Unicode version.</summary>
    internal static bool IsAssigned(int scalar) => Find(_assignedRanges, scalar, 0) != 0;

    private static int Find(PropertyRange[] ranges, int scalar, int fallback)
    {
        var low = 0;
        var high = ranges.Length - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = ranges[middle];

            if (scalar < range.Start)
            {
                high = middle - 1;
            }
            else if (scalar > range.End)
            {
                low = middle + 1;
            }
            else
            {
                return range.Value;
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

const check = process.argv.includes("--check");

if (process.argv.includes("--refresh")) {
  await refresh();
}

const outputs = generate(await loadSources());

if (check) {
  const legacy = await readFile(legacyOutputPath, "utf8").catch(() => "");

  if (legacy.length !== 0) {
    throw new Error("Unicode generated output is stale; run npm run generate:unicode.");
  }

  for (const [name, output] of outputs) {
    const outputPath = path.join(outputDirectory, name);
    const current = await readFile(outputPath, "utf8").catch(() => "");

    if (current !== output) {
      throw new Error("Unicode generated output is stale; run npm run generate:unicode.");
    }
  }

  process.stdout.write("Unicode generated output is current.\n");
} else {
  await mkdir(outputDirectory, { recursive: true });
  await rm(legacyOutputPath, { force: true });

  for (const [name, output] of outputs) {
    const outputPath = path.join(outputDirectory, name);
    await writeFile(outputPath, output, "utf8");
    process.stdout.write(`Generated ${path.relative(root, outputPath)}.\n`);
  }
}
