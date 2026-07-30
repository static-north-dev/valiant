const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const repositoryRoot = path.resolve(__dirname, '..', '..');
const documentationRoots = [
  path.join(repositoryRoot, 'README.md'),
  path.join(repositoryRoot, 'docs', 'content'),
  path.join(repositoryRoot, 'docs', 'static'),
  path.join(repositoryRoot, 'docs', 'versioned_docs'),
  path.join(repositoryRoot, 'endpoints', 'README.md'),
  path.join(repositoryRoot, 'endpoints', 'docs'),
  path.join(repositoryRoot, 'endpoints', 'samples', 'README.md'),
  path.join(repositoryRoot, 'mapping', 'README.md'),
  path.join(repositoryRoot, 'mapping', 'samples', 'README.md'),
  path.join(repositoryRoot, 'settings', 'README.md'),
  path.join(repositoryRoot, 'settings', 'samples', 'README.md'),
  path.join(repositoryRoot, 'validation', 'README.md'),
  path.join(repositoryRoot, 'validation', 'samples', 'README.md'),
];

function documentationFiles(target) {
  const stat = fs.statSync(target);
  if (stat.isFile()) {
    return [target];
  }

  return fs.readdirSync(target, {withFileTypes: true}).flatMap((entry) => {
    const entryPath = path.join(target, entry.name);
    if (entry.isDirectory()) {
      return documentationFiles(entryPath);
    }

    return /\.(?:md|mdx|txt)$/i.test(entry.name) ? [entryPath] : [];
  });
}

test('user documentation presents only the StaticNorth.Valiant package family', () => {
  const obsoleteIdentity =
    /(?<!StaticNorth\.)\bValiant\.(?:\*|Validation|Endpoints|Mapping|Settings)(?:\.[A-Za-z_][A-Za-z0-9_]*)*/;
  const obsoleteMigrationCopy =
    /\b(?:breaking migration from Valiant|package migration|using Valiant(?:\.|\b))/i;

  for (const filePath of documentationRoots.flatMap(documentationFiles)) {
    const contents = fs.readFileSync(filePath, 'utf8');
    const relativePath = path.relative(repositoryRoot, filePath);

    assert.doesNotMatch(contents, obsoleteIdentity, relativePath);
    assert.doesNotMatch(contents, obsoleteMigrationCopy, relativePath);
  }
});
