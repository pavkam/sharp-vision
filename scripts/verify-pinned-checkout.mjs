import { execFile } from "node:child_process";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);

/**
 * Verifies a source checkout names the expected commit and contains exactly its committed tree.
 *
 * @param {string} root the source checkout root.
 * @param {string} expectedCommit the complete commit identifier required at HEAD.
 * @returns {Promise<void>} a promise that resolves only for the clean pinned checkout.
 */
export const verifyPinnedCheckout = async (root, expectedCommit) => {
  const { stdout: head } = await execFileAsync("git", ["-C", root, "rev-parse", "HEAD"]);

  if (head.trim() !== expectedCommit) {
    throw new Error(`Source checkout ${root} is not pinned to ${expectedCommit}.`);
  }

  const { stdout: status } = await execFileAsync("git", [
    "-C",
    root,
    "status",
    "--porcelain=v1",
    "--untracked-files=all",
  ]);

  if (status.length > 0) {
    throw new Error(`Source checkout ${root} working tree is not clean.`);
  }
};
