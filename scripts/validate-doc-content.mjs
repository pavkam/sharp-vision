const issueIdentifierPattern = /(^|[^\p{L}\p{N}_])(#\d+)\b/gu;
const issueUrlPattern = /https:\/\/github\.com\/[^/\s)]+\/[^/\s)]+\/issues\/\d+\b/giu;

export function findGitHubIssueIdentifiers(markdown) {
  const errors = [];
  const lines = markdown.split(/\r?\n/u);
  let fence = null;

  for (const [index, line] of lines.entries()) {
    const marker = line.match(/^\s{0,3}(`{3,}|~{3,})/u)?.[1];

    if (marker !== undefined) {
      if (fence === null) {
        fence = marker[0];
      } else if (marker[0] === fence) {
        fence = null;
      }

      continue;
    }

    if (fence !== null) {
      continue;
    }

    const prose = line.replace(/`+[^`]*`+/gu, "");
    const lineNumber = index + 1;

    if (issueUrlPattern.test(prose)) {
      errors.push(`line ${lineNumber}: GitHub issue URL`);
    }

    issueUrlPattern.lastIndex = 0;

    for (const match of prose.matchAll(issueIdentifierPattern)) {
      errors.push(`line ${lineNumber}: GitHub issue identifier ${match[2]}`);
    }
  }

  return errors;
}
