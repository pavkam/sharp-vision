import assert from "node:assert/strict";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import { validateExtern } from "./validate-extern.mjs";

const createRepository = async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "sharpvision-extern-"));

    await mkdir(path.join(root, "extern", "figlet"), { recursive: true });
    await mkdir(path.join(root, "extern", "unicode", "17.0.0"), {
        recursive: true,
    });
    await writeFile(path.join(root, "extern", "README.md"), "# External\n");
    await writeFile(
        path.join(root, "extern", "figlet", "README.md"),
        "# FIGlet\n",
    );
    await writeFile(
        path.join(root, "extern", "figlet", "NOTICE.md"),
        "Notice\n",
    );
    await writeFile(
        path.join(root, "extern", "figlet", "fonts.zip"),
        "archive",
    );
    await writeFile(
        path.join(root, "extern", "unicode", "README.md"),
        "# Unicode\n",
    );
    await writeFile(
        path.join(root, "extern", "unicode", "LICENSE.txt"),
        "License\n",
    );
    await writeFile(
        path.join(root, "extern", "unicode", "17.0.0", "UnicodeData.txt"),
        "0041;LATIN CAPITAL LETTER A\n",
    );

    return root;
};

test("validateExtern accepts documented resources under extern", async () => {
    const root = await createRepository();

    try {
        await assert.doesNotReject(validateExtern(root));
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

test("validateExtern rejects the legacy data directory", async () => {
    const root = await createRepository();

    try {
        await mkdir(path.join(root, "data"));
        await assert.rejects(validateExtern(root), /legacy data directory/u);
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

test("validateExtern rejects resource payloads outside extern", async () => {
    const root = await createRepository();

    try {
        await mkdir(path.join(root, "src"));
        await writeFile(path.join(root, "src", "fonts.zip"), "archive");
        await assert.rejects(validateExtern(root), /outside extern/u);
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

test("validateExtern accepts the audited embedded FIGlet archive", async () => {
    const root = await createRepository();

    try {
        const resources = path.join(
            root,
            "src",
            "SharpVision",
            "Fonts",
            "Resources",
        );
        await mkdir(resources, { recursive: true });
        await writeFile(path.join(resources, "fonts.zip"), "archive");
        await assert.doesNotReject(validateExtern(root));
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

test("validateExtern rejects a package without provenance", async () => {
    const root = await createRepository();

    try {
        await rm(path.join(root, "extern", "figlet", "README.md"));
        await assert.rejects(validateExtern(root), /README/u);
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

test("validateExtern rejects a package without licensing material", async () => {
    const root = await createRepository();

    try {
        await rm(path.join(root, "extern", "figlet", "NOTICE.md"));
        await assert.rejects(validateExtern(root), /license or notice/u);
    } finally {
        await rm(root, { recursive: true, force: true });
  }
});

test("validateExtern rejects stale resource references with either slash style", async () => {
  const root = await createRepository();

  try {
    await mkdir(path.join(root, "tests"));
    await writeFile(
      path.join(root, "tests", "Tests.csproj"),
      '<None Include="..\\data\\unicode\\17.0.0\\UnicodeData.txt" />\n',
    );
    await assert.rejects(validateExtern(root), /legacy resource path/u);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
