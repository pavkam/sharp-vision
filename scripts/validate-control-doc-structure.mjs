#!/usr/bin/env node

import { readFile, readdir } from "node:fs/promises";
import { basename, join, relative, sep } from "node:path";
import { pathToFileURL } from "node:url";

// A page migrated to the hardened contract but whose primary type still awaits the InputBase
// rework keeps a temporary, explicit exemption here instead of failing every run. This set shrinks
// to empty as the input-base rework lands and each page migrates in turn - it is reviewed by hand,
// never derived, so a page can only leave it once its own conversion actually happened. The five
// pages that were exempted for the InputBase rework have all migrated, so the set is empty; it
// stays declared for the next rework that needs the same deferred-migration mechanism.
export const PENDING_MIGRATION = new Set([]);

// A sentinel documented-type value meaning "resolve the page's own filename normally for display,
// but never run the Tier B member-existence check against it." `ContextMenu` documents a standalone
// `IDisposable` coordinator, not a `ControlBase` in the retained tree: its Inheritance diagram uses
// composition and annotation syntax only, so Tier A already skips it with no edges to check: this
// sentinel gives Tier B the same treatment deliberately, rather than accidentally, by omission.
export const SKIP_TIER_B = Symbol("skip-tier-b");

// Kebab-case file slug to the exact snapshot type key (`Name#arity`) the page documents. Only
// needed where the naive per-segment capitalization, at the default arity of zero, does not spell
// the real declaration to check members against.
//
// `control` and `composite-control` each document a non-generic authoring role whose own slug's
// default PascalCase conversion names a different (or, since the typed-style-slot refactor removed
// `Control<TStyle>`/`CompositeControl<TStyle>` entirely, no longer existing) type: the pages
// describe `ControlBase` and `CompositeControlBase` directly, with a typed style now composed via
// `IStyled<TStyle>` rather than inherited from a generic specialization - see
// docs/concepts/styling.md. `pressable` documents two `InputBase` capabilities (`EnableCaption`,
// `EnableCommand`) rather than a type of its own - the caption/command role `PressableBase` used to
// own before it was folded into `InputBase` alongside the rest of that base's opt-in capabilities -
// so its API table is checked against `InputBase#0` the same as `input-base.md`'s own table.
const documentedTypeOverrides = new Map([
    ["list-view", "ListView#0"],
    ["status-bar", "StatusBar#0"],
    ["control", "ControlBase#0"],
    ["composite-control", "CompositeControlBase#0"],
    ["content-control", "ContentControl#0"],
    ["headered-content-control", "HeaderedContentControl#0"],
    ["items-control", "ItemsControl#0"],
    ["container", "Container#0"],
    ["context-menu", SKIP_TIER_B],
    ["pressable", "InputBase#0"],
]);

const expectedApiHeader = ["Member", "Type", "Default", "Description"];

/**
 * Reports whether a table's header row is exactly the canonical `Member | Type | Default |
 * Description` shape, after the whitespace normalization `parseMarkdownTables` already applies.
 *
 * @param {string[]} headerCells A table's header cells.
 * @returns {boolean} Whether the header matches exactly, in order.
 */
function isCanonicalApiHeader(headerCells) {
    return (
        headerCells.length === expectedApiHeader.length &&
        headerCells.every((cell, index) => cell === expectedApiHeader[index])
    );
}

const typeDeclarationPattern =
    /^ {4}(?:public|internal|protected|private)\s+(?:abstract\s+|sealed\s+|static\s+|readonly\s+|partial\s+|new\s+|virtual\s+)*(?:class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)(<[^>]*>)?\s*(?::\s*(.+))?$/;

const inlineBodyPattern = /\s*\{\s*\}\s*$/u;

const fencePattern = /^\s{0,3}(`{3,}|~{3,})\s*(\S*)/;
const fenceClosePattern = /^\s{0,3}(`{3,}|~{3,})\s*$/;
const tableDelimiterPattern = /^\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?$/;

/**
 * Converts a kebab-case documentation slug to its default PascalCase type name, one capitalized
 * segment per hyphen-separated piece (`list-view` -> `ListView`, `check-box` -> `CheckBox`).
 *
 * @param {string} slug The file's basename without extension.
 * @returns {string} The default PascalCase name.
 */
export function slugToPascalCase(slug) {
    return slug
        .split("-")
        .map((part) =>
            part.length === 0 ? part : part[0].toUpperCase() + part.slice(1),
        )
        .join("");
}

/**
 * Derives the snapshot type key (`Name#arity`) a control documentation page documents, from its
 * file slug: the explicit override map wins, otherwise the default PascalCase conversion applies at
 * arity zero, correct for every concrete, non-generic control.
 *
 * @param {string} slug The file's basename without extension.
 * @returns {string | typeof SKIP_TIER_B} The declared type's snapshot key, or the Tier B skip
 * sentinel.
 */
export function deriveDocumentedType(slug) {
    return documentedTypeOverrides.has(slug)
        ? documentedTypeOverrides.get(slug)
        : `${slugToPascalCase(slug)}#0`;
}

/**
 * Splits a comma-separated span at top-level commas only, treating `<...>` as one non-splitting
 * unit so a nested generic argument list's own commas never break the split.
 *
 * @param {string} text The span to split, for example a base-type list or a generic argument list.
 * @returns {string[]} Trimmed, non-empty segments in order.
 */
export function splitTopLevelCommas(text) {
    const parts = [];
    let depth = 0;
    let current = "";

    for (const char of text) {
        if (char === "<") {
            depth++;
        } else if (char === ">") {
            depth--;
        }

        if (char === "," && depth === 0) {
            parts.push(current.trim());
            current = "";
        } else {
            current += char;
        }
    }

    if (current.trim() !== "") {
        parts.push(current.trim());
    }

    return parts;
}

/**
 * Resolves one type reference - a compatibility-snapshot base-list entry or a mermaid diagram node
 * - to its simple name and generic arity, tolerating namespace qualification and mermaid's `~T~`
 * generic spelling.
 *
 * @param {string} token The raw reference text.
 * @returns {{ simpleName: string, arity: number, key: string } | undefined} The resolved reference,
 * or `undefined` when the token is not a recognizable type reference.
 */
export function parseTypeReference(token) {
    const cleaned = token.trim().replaceAll(/~([^~]*)~/gu, "<$1>");
    const match = cleaned.match(/^([A-Za-z_][\w.]*)(?:<(.*)>)?$/u);

    if (!match) {
        return undefined;
    }

    const simpleName = match[1].split(".").at(-1);
    const arity =
        match[2] === undefined ? 0 : splitTopLevelCommas(match[2]).length;

    return { simpleName, arity, key: `${simpleName}#${arity}` };
}

/**
 * Parses every top-level type declaration out of the public API compatibility snapshot: every
 * `namespace { ... }` block holds only flat, 4-space-indented `class`/`struct`/`interface`/`enum`
 * declarations, one level deep, with 8-space-indented members and a 4-space `}` closing the type.
 * That fixed shape means indentation alone disambiguates a type line from a member line or a
 * generic `where` constraint line, with no brace-depth tracking required.
 *
 * A member-less type is emitted by the snapshot writer with its whole body inline, as
 * `public abstract class Node : Base { }`. That trailing `{ }` is stripped before the declaration is
 * matched, for two reasons: it would otherwise be captured as part of the base list and defeat the
 * base-type parse, and the type has no member block to scan, so scanning forward would consume every
 * following sibling declaration as if it were a member of this one.
 *
 * @param {string} snapshotText The full contents of `SharpVision.verified.txt`.
 * @returns {Map<string, { name: string, arity: number, base: string | null, members: Set<string> }>}
 * Type key (`Name#arity`) to its declaration.
 */
export function parseSnapshotTypes(snapshotText) {
    const lines = snapshotText.split(/\r?\n/u);
    const map = new Map();
    let index = 0;

    while (index < lines.length) {
        const inlineBody = inlineBodyPattern.test(lines[index]);
        const declarationLine = inlineBody
            ? lines[index].replace(inlineBodyPattern, "")
            : lines[index];
        const declarationMatch = declarationLine.match(typeDeclarationPattern);

        if (!declarationMatch) {
            index++;
            continue;
        }

        const [, name, generics, baseList] = declarationMatch;
        const arity =
            generics === undefined
                ? 0
                : splitTopLevelCommas(generics.slice(1, -1)).length;
        const key = `${name}#${arity}`;

        let base = null;

        if (baseList !== undefined) {
            const baseTokens = splitTopLevelCommas(baseList);

            if (baseTokens.length > 0) {
                const reference = parseTypeReference(baseTokens[0]);
                base = reference === undefined ? null : reference.key;
            }
        }

        const members = new Set();
        let cursor = index + 1;

        while (!inlineBody && cursor < lines.length) {
            if (/^ {4}\}\s*$/u.test(lines[cursor])) {
                cursor++;
                break;
            }

            const cut = lines[cursor].search(/[({;]/u);
            const head =
                cut === -1 ? lines[cursor] : lines[cursor].slice(0, cut + 1);
            const memberMatch = head.match(
                /([A-Za-z_][A-Za-z0-9_]*)\s*[({;]$/u,
            );

            if (memberMatch) {
                members.add(memberMatch[1]);
            }

            cursor++;
        }

        map.set(key, { name, arity, base, members });
        index = cursor;
    }

    return map;
}

/**
 * Walks one type's base chain through the snapshot map, collecting every declared member along the
 * way. The walk stops at the first key the map does not contain - an interface, an external BCL
 * type, or simply "no base" - which is the correct and only termination condition, since the map
 * only ever contains this repository's own declared types.
 *
 * @param {string} startKey The starting type key (`Name#arity`).
 * @param {Map<string, { base: string | null, members: Set<string> }>} snapshotMap The parsed
 * snapshot.
 * @returns {Set<string>} Every member name declared by the type or any ancestor in the chain.
 */
export function ancestorMemberSet(startKey, snapshotMap) {
    const members = new Set();
    const visited = new Set();
    let currentKey = startKey;

    while (
        currentKey !== null &&
        currentKey !== undefined &&
        snapshotMap.has(currentKey) &&
        !visited.has(currentKey)
    ) {
        visited.add(currentKey);
        const entry = snapshotMap.get(currentKey);

        for (const member of entry.members) {
            members.add(member);
        }

        currentKey = entry.base;
    }

    return members;
}

/**
 * Extracts H2 headings in document order, skipping any line inside a fenced code block so a
 * heading-like line inside a `csharp` example never counts as a real section boundary.
 *
 * @param {string[]} lines The document, split into lines.
 * @returns {{ text: string, line: number }[]} Each H2's text and zero-based line index.
 */
export function extractH2Headings(lines) {
    const headings = [];
    let fenceMarker = null;

    for (const [index, line] of lines.entries()) {
        const fenceMatch = line.match(fencePattern);

        if (fenceMatch) {
            const marker = fenceMatch[1][0];

            if (fenceMarker === null) {
                fenceMarker = marker;
            } else if (marker === fenceMarker) {
                fenceMarker = null;
            }

            continue;
        }

        if (fenceMarker !== null) {
            continue;
        }

        const headingMatch = line.match(/^##\s+(.+?)\s*$/u);

        if (headingMatch) {
            headings.push({ text: headingMatch[1], line: index });
        }
    }

    return headings;
}

/**
 * Extracts every H2 and H3 heading in document order, fence-aware like `extractH2Headings`. Used to
 * find the subject type of a secondary API table - a page's H2/H3 vocabulary (`## TabItem`, `###
 * StatusBarItem`) is the only reliable signal for which declared type a table below it documents.
 *
 * @param {string[]} lines The document, split into lines.
 * @returns {{ text: string, line: number, level: 2 | 3 }[]} Each heading's text, line, and level.
 */
export function extractSubsectionHeadings(lines) {
    const headings = [];
    let fenceMarker = null;

    for (const [index, line] of lines.entries()) {
        const fenceMatch = line.match(fencePattern);

        if (fenceMatch) {
            const marker = fenceMatch[1][0];

            if (fenceMarker === null) {
                fenceMarker = marker;
            } else if (marker === fenceMarker) {
                fenceMarker = null;
            }

            continue;
        }

        if (fenceMarker !== null) {
            continue;
        }

        const headingMatch = line.match(/^(#{2,3})(?!#)\s+(.+?)\s*$/u);

        if (headingMatch) {
            headings.push({
                text: headingMatch[2],
                line: index,
                level: headingMatch[1].length,
            });
        }
    }

    return headings;
}

/**
 * Finds the closest heading strictly before a given line - the heading that introduces whatever
 * table or content sits at that line, regardless of H2/H3 level.
 *
 * @param {{ text: string, line: number }[]} headings Headings in document order.
 * @param {number} lineIndex The line to search before.
 * @returns {{ text: string, line: number } | undefined} The nearest preceding heading, if any.
 */
export function findNearestHeadingBefore(headings, lineIndex) {
    let nearest;

    for (const heading of headings) {
        if (heading.line >= lineIndex) {
            break;
        }

        nearest = heading;
    }

    return nearest;
}

/**
 * Validates the required control-page H2 spine: `Overview`, `Inheritance`, `API`, any number of
 * optional topic sections, `Example`, then `Expected behavior`, in that exact order.
 *
 * @param {{ text: string }[]} headings The page's H2 headings, in document order.
 * @returns {string | null} A violation description, or `null` when the spine is correct.
 */
export function validateSectionSpine(headings) {
    const texts = headings.map((heading) => heading.text);

    if (texts.length < 5) {
        return (
            `H2 spine is incomplete (found: ${texts.length === 0 ? "none" : texts.join(", ")}); ` +
            "expected Overview, Inheritance, API, ..., Example, Expected behavior"
        );
    }

    const problems = [];

    if (texts[0] !== "Overview") {
        problems.push(`first H2 is "${texts[0]}", expected "Overview"`);
    }

    if (texts[1] !== "Inheritance") {
        problems.push(`second H2 is "${texts[1]}", expected "Inheritance"`);
    }

    if (texts[2] !== "API") {
        problems.push(`third H2 is "${texts[2]}", expected "API"`);
    }

    if (texts.at(-1) !== "Expected behavior") {
        problems.push(
            `last H2 is "${texts.at(-1)}", expected "Expected behavior"`,
        );
    }

    if (texts.at(-2) !== "Example") {
        problems.push(
            `H2 before "Expected behavior" is "${texts.at(-2)}", expected "Example"`,
        );
    }

    return problems.length === 0
        ? null
        : `H2 spine violation: ${problems.join("; ")} (full spine: ${texts.join(" > ")})`;
}

/**
 * Slices the lines belonging to one named H2 section, from immediately after its heading up to
 * (excluding) the next H2 heading or the end of the document.
 *
 * @param {string[]} lines The full document, split into lines.
 * @param {{ text: string, line: number }[]} headings The document's H2 headings.
 * @param {string} name The exact section heading text to slice.
 * @returns {string[] | undefined} The section's lines, or `undefined` when the heading is absent.
 */
export function sliceSection(lines, headings, name) {
    const index = headings.findIndex((heading) => heading.text === name);

    if (index === -1) {
        return undefined;
    }

    const start = headings[index].line + 1;
    const end =
        index + 1 < headings.length ? headings[index + 1].line : lines.length;

    return lines.slice(start, end);
}

/**
 * Finds the first fenced code block in a run of lines, tolerating leading prose before it.
 *
 * @param {string[]} lines The lines to scan.
 * @returns {{ lang: string, body: string[] } | undefined} The fence's info string and body lines,
 * or `undefined` when no fence exists.
 */
export function firstFence(lines) {
    for (const [index, line] of lines.entries()) {
        const openMatch = line.match(fencePattern);

        if (!openMatch) {
            continue;
        }

        const marker = openMatch[1][0];
        const lang = openMatch[2] ?? "";
        const body = [];
        let cursor = index + 1;

        while (cursor < lines.length) {
            const closeMatch = lines[cursor].match(fenceClosePattern);

            if (closeMatch && closeMatch[1][0] === marker) {
                break;
            }

            body.push(lines[cursor]);
            cursor++;
        }

        return { lang, body };
    }

    return undefined;
}

/**
 * Validates `## Inheritance`: its first fenced code block must be a `mermaid` fence whose body
 * contains `classDiagram`. Leading prose before the fence is allowed; anything else - another kind
 * of fence, or no fence at all - is a violation.
 *
 * @param {string[]} lines The full document, split into lines.
 * @param {{ text: string, line: number }[]} headings The document's H2 headings.
 * @returns {{ error?: string, mermaidBody?: string }} The violation, or the extracted diagram body.
 */
export function validateInheritanceSection(lines, headings) {
    const section = sliceSection(lines, headings, "Inheritance");

    if (section === undefined) {
        return { error: "missing ## Inheritance section" };
    }

    const fence = firstFence(section);

    if (fence === undefined) {
        return {
            error: "## Inheritance has no fenced code block; expected a mermaid classDiagram",
        };
    }

    if (
        fence.lang.trim() !== "mermaid" ||
        !fence.body.some((line) => line.includes("classDiagram"))
    ) {
        return {
            error:
                "## Inheritance's first fenced block is not a mermaid classDiagram " +
                `(found language "${fence.lang.trim()}")`,
        };
    }

    return { mermaidBody: fence.body.join("\n") };
}

/**
 * Splits one Markdown table row into trimmed cells, dropping the leading and trailing pipe.
 *
 * @param {string} row The trimmed row text, starting and ending with `|`.
 * @returns {string[]} The row's cells.
 */
function splitTableRow(row) {
    let inner = row;

    if (inner.startsWith("|")) {
        inner = inner.slice(1);
    }

    if (inner.endsWith("|")) {
        inner = inner.slice(0, -1);
    }

    return inner.split("|").map((cell) => cell.trim());
}

/**
 * Finds every Markdown table in a run of lines, skipping fenced code blocks so a code sample's
 * pipe-heavy content never registers as a table.
 *
 * @param {string[]} lines The lines to scan.
 * @returns {{ headerCells: string[], dataRows: string[][], startIndex: number }[]} Every table
 * found, in document order.
 */
export function parseMarkdownTables(lines) {
    const tables = [];
    let fenceMarker = null;
    let index = 0;

    while (index < lines.length) {
        const line = lines[index];
        const fenceMatch = line.match(fencePattern);

        if (fenceMatch) {
            const marker = fenceMatch[1][0];

            if (fenceMarker === null) {
                fenceMarker = marker;
            } else if (marker === fenceMarker) {
                fenceMarker = null;
            }

            index++;
            continue;
        }

        if (fenceMarker !== null) {
            index++;
            continue;
        }

        const trimmed = line.trim();

        if (
            trimmed.startsWith("|") &&
            trimmed.endsWith("|") &&
            index + 1 < lines.length
        ) {
            const nextTrimmed = lines[index + 1].trim();

            if (tableDelimiterPattern.test(nextTrimmed)) {
                const headerCells = splitTableRow(trimmed);
                const dataRows = [];
                let cursor = index + 2;

                while (
                    cursor < lines.length &&
                    lines[cursor].trim().startsWith("|")
                ) {
                    dataRows.push(splitTableRow(lines[cursor].trim()));
                    cursor++;
                }

                tables.push({ headerCells, dataRows, startIndex: index });
                index = cursor;
                continue;
            }
        }

        index++;
    }

    return tables;
}

/**
 * Extracts every mermaid `A <|-- B` inheritance edge from a diagram body.
 *
 * @param {string} mermaidBody The diagram's fenced body text.
 * @returns {{ parent: string, child: string }[]} Every inheritance edge, in document order.
 */
export function extractInheritanceEdges(mermaidBody) {
    const edges = [];

    for (const rawLine of mermaidBody.split("\n")) {
        const match = rawLine
            .trim()
            .match(/^(\S+)\s*<\|--\s*(\S+?)(?:\s*:.*)?$/u);

        if (match) {
            edges.push({ parent: match[1], child: match[2] });
        }
    }

    return edges;
}

/**
 * Describes a snapshot type key back into readable diagram-style text, for error messages.
 *
 * @param {string | null} key A type key (`Name#arity`), or `null` for "no class base".
 * @returns {string} A readable description.
 */
function describeKey(key) {
    if (key === null || key === undefined) {
        return "no class base";
    }

    const [name, arity] = key.split("#");
    return arity === "0" ? name : `${name}<...>`;
}

/**
 * Tier A: cross-checks every mermaid inheritance edge whose parent and child both resolve to a
 * declared type in the compatibility snapshot against that type's actual immediate base. An edge
 * whose child (or parent) is not in the snapshot - an internal type, an interface realization, a
 * composition edge, or a data type - is skipped, not an error: this check only catches a stale
 * edge between two types the snapshot can actually verify.
 *
 * @param {string} mermaidBody The page's Inheritance diagram body.
 * @param {Map<string, { base: string | null }>} snapshotMap The parsed snapshot.
 * @returns {{ errors: string[], checked: number, skipped: number }} Violations plus edge counts.
 */
export function validateTierA(mermaidBody, snapshotMap) {
    const edges = extractInheritanceEdges(mermaidBody);
    const errors = [];
    let checked = 0;
    let skipped = 0;

    for (const edge of edges) {
        const parentRef = parseTypeReference(edge.parent);
        const childRef = parseTypeReference(edge.child);

        if (
            parentRef === undefined ||
            childRef === undefined ||
            !snapshotMap.has(parentRef.key) ||
            !snapshotMap.has(childRef.key)
        ) {
            skipped++;
            continue;
        }

        checked++;
        const childEntry = snapshotMap.get(childRef.key);

        if (childEntry.base !== parentRef.key) {
            errors.push(
                `Inheritance edge "${edge.parent} <|-- ${edge.child}" does not match the compatibility ` +
                    `snapshot; ${childRef.simpleName}'s actual base is ${describeKey(childEntry.base)}`,
            );
        }
    }

    return { errors, checked, skipped };
}

/**
 * Extracts every checkable member-reference token from one API-table `Member` cell. A token is
 * checkable when the entire cell, once its backtick-quoted spans are removed, contains nothing but
 * commas and whitespace - a grouped cell like `` `Width`, `Height` `` yields one token per span. The
 * exact leading pattern `Inherited ` is also stripped before that residue check, so
 * `` Inherited `Text` `` is checkable too - an inherited member is still asserted to exist in the
 * same ancestor chain the rest of the table checks against, which is exactly what the chain walk
 * proves. Any other residue - `Inherited` without a following backtick span, or unrelated prose like
 * `Inherited `Text`` prefixed further - is left un-checkable and the cell is skipped. A trailing
 * `(params)` is stripped from a method or constructor span; an `Owner.Property` span (used by
 * attached-property rows) is returned as an attached-property reference instead of a plain member
 * name.
 *
 * @param {string} cell The raw `Member` cell text.
 * @returns {({ name: string } | { owner: string, property: string })[]} The cell's checkable
 * tokens; empty when the cell is not entirely backtick-quoted identifiers (optionally prefixed by
 * the literal `Inherited `).
 */
export function extractCheckableTokens(cell) {
    const trimmed = cell.trim();

    if (trimmed === "" || trimmed === "—" || trimmed === "-") {
        return [];
    }

    const inheritedMatch = trimmed.match(/^Inherited\s+(.+)$/u);
    const candidate = inheritedMatch ? inheritedMatch[1] : trimmed;

    const spans = [...candidate.matchAll(/`([^`]*)`/gu)];

    if (spans.length === 0) {
        return [];
    }

    const residue = candidate
        .replaceAll(/`[^`]*`/gu, "")
        .replaceAll(/[,\s]/gu, "");

    if (residue !== "") {
        return [];
    }

    const tokens = [];

    for (const span of spans) {
        const content = span[1];
        const attachedMatch = content.match(
            /^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)$/u,
        );

        if (attachedMatch) {
            tokens.push({
                owner: attachedMatch[1],
                property: attachedMatch[2],
            });
            continue;
        }

        const plainMatch = content.match(
            /^([A-Za-z_][A-Za-z0-9_]*)(?:\(.*\))?$/u,
        );

        if (plainMatch) {
            tokens.push({ name: plainMatch[1] });
        }
    }

    return tokens;
}

/**
 * Finds the single paragraph immediately preceding a table - the contiguous run of non-blank lines
 * directly above it, stopping at the first blank line or heading. Real pages consistently introduce
 * a secondary table's subject type by backtick-quoted name at the start of this exact paragraph (for
 * example `` `SeparatorStyle`, reached through `Style`/`ActualStyle`, owns... `` right before
 * Separator's `## Glyphs` table, or `` `CalendarBlockedDateCollection` owns normalized... `` right
 * before Calendar's `## Blocked dates` table) even when the section heading itself is a descriptive
 * title rather than the type's name.
 *
 * @param {string[]} lines The full document, split into lines.
 * @param {number} tableStartIndex The table's own start line.
 * @returns {string[]} The paragraph's lines, oldest first; empty when the table has no leading prose.
 */
function precedingParagraph(lines, tableStartIndex) {
    let index = tableStartIndex - 1;

    while (index >= 0 && lines[index].trim() === "") {
        index--;
    }

    const paragraph = [];

    while (
        index >= 0 &&
        lines[index].trim() !== "" &&
        !/^#{1,6}\s/u.test(lines[index])
    ) {
        paragraph.unshift(lines[index]);
        index--;
    }

    return paragraph;
}

/**
 * Finds the first backtick-quoted span in a paragraph and returns its leading identifier - the type
 * name a span like `` `SeparatorStyle` `` or `` `CalendarStyle : InputStyle` `` introduces, ignoring
 * any base-type or interface annotation after a colon.
 *
 * @param {string[]} paragraph Paragraph lines, in order.
 * @returns {string | undefined} The leading identifier of the first backtick span, if any.
 */
function firstBacktickIdentifier(paragraph) {
    const span = paragraph.join(" ").match(/`([^`]*)`/u);

    if (span === null) {
        return undefined;
    }

    return span[1].match(/^([A-Za-z_][A-Za-z0-9_]*)/u)?.[1];
}

/**
 * Resolves one table's subject type key: the nearest preceding H2/H3 heading wins when its own text
 * names a snapshot type; otherwise the type named at the start of the table's immediately preceding
 * paragraph wins when that resolves too; otherwise the page's primary documented type is the subject,
 * exactly as an ordinary `## API` table (whose nearest heading is literally `API`, never a type) or an
 * `### Attached properties` table (which carries no leading prose at all in every real page) already
 * resolve.
 *
 * @param {string[]} lines The full document, split into lines.
 * @param {{ text: string, line: number }[]} headings The document's H2/H3 headings.
 * @param {{ startIndex: number }} table The table being resolved.
 * @param {string} primaryKey The page's primary documented type's snapshot key.
 * @param {Map<string, unknown>} snapshotMap The parsed snapshot.
 * @returns {string} The resolved subject type key.
 */
export function resolveTableSubjectKey(
    lines,
    headings,
    table,
    primaryKey,
    snapshotMap,
) {
    const heading = findNearestHeadingBefore(headings, table.startIndex);
    const headingKey =
        heading === undefined
            ? undefined
            : `${heading.text.replaceAll("`", "").trim()}#0`;

    if (headingKey !== undefined && snapshotMap.has(headingKey)) {
        return headingKey;
    }

    const paragraphIdentifier = firstBacktickIdentifier(
        precedingParagraph(lines, table.startIndex),
    );
    const paragraphKey =
        paragraphIdentifier === undefined
            ? undefined
            : `${paragraphIdentifier}#0`;

    if (paragraphKey !== undefined && snapshotMap.has(paragraphKey)) {
        return paragraphKey;
    }

    return primaryKey;
}

/**
 * Tier B: scans every table anywhere in the page whose header row is exactly the canonical `Member |
 * Type | Default | Description` shape - the main `## API` table, any `### Attached properties`
 * sub-table, and any secondary-type table under its own heading (`## TabItem`, `### StatusBarItem`,
 * `## TreeViewItem`) - and asserts every checkable `Member` cell names a member that actually exists.
 *
 * Each table's subject type is resolved independently by `resolveTableSubjectKey`: the nearest H2/H3
 * heading strictly above the table wins when it names a snapshot type (this is how a secondary-type
 * table under `## TabItem` or `### StatusBarItem` is checked against its own type instead of the
 * page's primary type); otherwise the type named at the start of the table's immediately preceding
 * paragraph wins, when the section instead uses a descriptive heading like `## Glyphs` or `## Blocked
 * dates`; otherwise the page's primary documented type is the subject - the ordinary case for the
 * main `## API` table and for an `### Attached properties` table. A plain member is checked against
 * the subject type's ancestor chain; an `Owner.Property` attached-property token is always checked
 * against the named owner's chain instead, regardless of the table's own subject.
 *
 * @param {string[]} lines The full document, split into lines.
 * @param {{ text: string, line: number }[]} headings The document's H2/H3 headings.
 * @param {string} primaryKey The page's primary documented type's snapshot key (`Name#arity`).
 * @param {Map<string, { base: string | null, members: Set<string> }>} snapshotMap The parsed
 * snapshot.
 * @returns {{ errors: string[], checked: number, skipped: number }} Violations plus token counts.
 */
export function validateTierB(lines, headings, primaryKey, snapshotMap) {
    const errors = [];
    let checked = 0;
    let skipped = 0;

    const canonicalTables = parseMarkdownTables(lines).filter((table) =>
        isCanonicalApiHeader(table.headerCells),
    );

    if (canonicalTables.length === 0) {
        return { errors, checked, skipped };
    }

    if (!snapshotMap.has(primaryKey)) {
        errors.push(
            `documented type "${describeKey(primaryKey)}" was not found in the compatibility snapshot`,
        );
        return { errors, checked, skipped };
    }

    const memberCache = new Map();
    const membersOf = (key) => {
        if (!memberCache.has(key)) {
            memberCache.set(key, ancestorMemberSet(key, snapshotMap));
        }

        return memberCache.get(key);
    };

    for (const table of canonicalTables) {
        const subjectKey = resolveTableSubjectKey(
            lines,
            headings,
            table,
            primaryKey,
            snapshotMap,
        );
        const subjectMembers = membersOf(subjectKey);

        for (const row of table.dataRows) {
            const tokens = extractCheckableTokens(row[0] ?? "");

            if (tokens.length === 0) {
                skipped++;
                continue;
            }

            for (const token of tokens) {
                checked++;

                if ("owner" in token) {
                    const ownerKey = `${token.owner}#0`;

                    if (!snapshotMap.has(ownerKey)) {
                        errors.push(
                            `attached property owner \`${token.owner}\` was not found in the compatibility snapshot`,
                        );
                        continue;
                    }

                    const ownerMembers = membersOf(ownerKey);

                    if (
                        !ownerMembers.has(`Get${token.property}`) &&
                        !ownerMembers.has(`Set${token.property}`)
                    ) {
                        errors.push(
                            `attached property \`${token.owner}.${token.property}\` has neither ` +
                                `Get${token.property} nor Set${token.property} on ${token.owner} in the ` +
                                "compatibility snapshot",
                        );
                    }
                } else if (!subjectMembers.has(token.name)) {
                    errors.push(
                        `member \`${token.name}\` was not found on ${describeKey(subjectKey)} or its ` +
                            "ancestors in the compatibility snapshot",
                    );
                }
            }
        }
    }

    return { errors, checked, skipped };
}

/**
 * Recursively collects every Markdown file under one root.
 *
 * @param {string} root The directory to walk.
 * @returns {Promise<string[]>} Absolute paths to every `.md` file.
 */
async function findMarkdownFiles(root) {
    const entries = await readdir(root, { withFileTypes: true });
    const files = [];

    for (const entry of entries) {
        const entryPath = join(root, entry.name);

        if (entry.isDirectory()) {
            files.push(...(await findMarkdownFiles(entryPath)));
        } else if (entry.isFile() && entry.name.endsWith(".md")) {
            files.push(entryPath);
        }
    }

    return files;
}

/**
 * Validates every non-index `docs/controls/**` page against the control-page contract: the H2
 * spine, the Inheritance mermaid diagram, the API table's canonical header, and the Tier A/Tier B
 * cross-checks against the public API compatibility snapshot. Pages are enumerated live from the
 * filesystem, never from a hardcoded list, so a newly added page is validated automatically.
 *
 * @param {string} root The repository root.
 * @returns {Promise<{
 *   errors: string[],
 *   warnings: string[],
 *   stats: {
 *     typesMapped: number,
 *     pagesChecked: number,
 *     pagesExempt: number,
 *     tierAChecked: number,
 *     tierASkipped: number,
 *     tierBChecked: number,
 *     tierBSkipped: number,
 *   },
 * }>} Every violation, every pending-migration warning, and summary counts.
 */
export async function validateControlDocStructure(root) {
    const snapshotsRoot = join(
        root,
        "tests",
        "SharpVision.Compatibility.Tests",
        "Snapshots",
    );
    const snapshotTexts = [
        await readFile(join(snapshotsRoot, "SharpVision.verified.txt"), "utf8"),
    ];

    try {
        snapshotTexts.push(
            await readFile(
                join(snapshotsRoot, "SharpVision.Document.verified.txt"),
                "utf8",
            ),
        );
    } catch (error) {
        if (error.code !== "ENOENT") {
            throw error;
        }
    }

    try {
        snapshotTexts.push(
            await readFile(
                join(snapshotsRoot, "SharpVision.SyntaxHighlighting.verified.txt"),
                "utf8",
            ),
        );
    } catch (error) {
        if (error.code !== "ENOENT") {
            throw error;
        }
    }

    const snapshotMap = new Map();

    for (const snapshotText of snapshotTexts) {
        for (const [key, value] of parseSnapshotTypes(snapshotText)) {
            snapshotMap.set(key, value);
        }
    }

    const controlsRoot = join(root, "docs", "controls");
    const docFiles = (await findMarkdownFiles(controlsRoot)).sort();

    const errors = [];
    const warnings = [];
    const stats = {
        typesMapped: snapshotMap.size,
        pagesChecked: 0,
        pagesExempt: 0,
        tierAChecked: 0,
        tierASkipped: 0,
        tierBChecked: 0,
        tierBSkipped: 0,
    };

    for (const docPath of docFiles) {
        if (basename(docPath) === "index.md") {
            continue;
        }

        const relativePath = relative(root, docPath).split(sep).join("/");

        if (PENDING_MIGRATION.has(relativePath)) {
            stats.pagesExempt++;
            warnings.push(
                `${relativePath}: PENDING_MIGRATION exemption - control-doc-structure checks skipped ` +
                    "pending its own migration",
            );
            continue;
        }

        stats.pagesChecked++;
        const text = await readFile(docPath, "utf8");
        const lines = text.split(/\r?\n/u);
        const headings = extractH2Headings(lines);

        const spineError = validateSectionSpine(headings);

        if (spineError !== null) {
            errors.push(`${relativePath}: ${spineError}`);
        }

        const inheritanceResult = validateInheritanceSection(lines, headings);

        if (inheritanceResult.error !== undefined) {
            errors.push(`${relativePath}: ${inheritanceResult.error}`);
        } else {
            const tierA = validateTierA(
                inheritanceResult.mermaidBody,
                snapshotMap,
            );

            for (const error of tierA.errors) {
                errors.push(`${relativePath}: ${error}`);
            }

            stats.tierAChecked += tierA.checked;
            stats.tierASkipped += tierA.skipped;
        }

        const apiSection = sliceSection(lines, headings, "API");
        const apiTables =
            apiSection === undefined ? [] : parseMarkdownTables(apiSection);

        if (apiTables.length === 0) {
            errors.push(`${relativePath}: ## API has no Markdown table`);
        } else if (!isCanonicalApiHeader(apiTables[0].headerCells)) {
            errors.push(
                `${relativePath}: ## API table header is "| ${apiTables[0].headerCells.join(" | ")} |", ` +
                    `expected "| ${expectedApiHeader.join(" | ")} |"`,
            );
        }

        const documentedKey = deriveDocumentedType(basename(docPath, ".md"));

        if (documentedKey !== SKIP_TIER_B) {
            const subsectionHeadings = extractSubsectionHeadings(lines);
            const tierB = validateTierB(
                lines,
                subsectionHeadings,
                documentedKey,
                snapshotMap,
            );

            for (const error of tierB.errors) {
                errors.push(`${relativePath}: ${error}`);
            }

            stats.tierBChecked += tierB.checked;
            stats.tierBSkipped += tierB.skipped;
        }
    }

    return { errors, warnings, stats };
}

async function main() {
    const root = join(import.meta.dirname, "..");
    const { errors, warnings, stats } = await validateControlDocStructure(root);

    for (const warning of warnings) {
        console.log(`PENDING_MIGRATION: ${warning}`);
    }

    if (errors.length === 0) {
        console.log(
            `Control-page contract satisfied for ${stats.pagesChecked} page(s) ` +
                `(${stats.pagesExempt} pending-migration exemption(s)). ` +
                `Tier A checked ${stats.tierAChecked} edge(s), skipped ${stats.tierASkipped}. ` +
                `Tier B checked ${stats.tierBChecked} member(s), skipped ${stats.tierBSkipped}.`,
        );
        return;
    }

    for (const error of errors) {
        console.error(error);
    }

    process.exitCode = 1;
}

const invokedPath =
    process.argv[1] === undefined
        ? undefined
        : pathToFileURL(process.argv[1]).href;

if (invokedPath === import.meta.url) {
    await main();
}
