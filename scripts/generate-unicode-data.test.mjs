import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

test("UnicodeData_WhenGenerated_UsesStaticMetadataBackedPrimitiveSpans", async () => {
  const source = await readFile(
    new URL("../src/SharpVision.Terminal/Unicode/UnicodeData.cs", import.meta.url),
    "utf8",
  );

  assert.doesNotMatch(source, /static readonly PropertyRange\[\]/u);
  assert.doesNotMatch(source, /static UnicodeData\s*\(/u);
  assert.match(source, /private const string GraphemeBreakStartsData/u);
  assert.match(source, /private static ReadOnlySpan<int> GraphemeBreakStarts/u);
  assert.match(source, /private static ReadOnlySpan<int> AssignedValues/u);
});
