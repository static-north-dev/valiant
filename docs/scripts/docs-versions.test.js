const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const sections = ['validation', 'endpoints', 'mapping', 'settings'];
const sidebarIds = sections.map((section) => `${section}Sidebar`);

function createFixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'valiant-docs-versions-test-'));
  const docsRoot = path.join(root, 'docs');
  fs.mkdirSync(path.join(docsRoot, 'scripts'), {recursive: true});
  fs.copyFileSync(
    path.join(__dirname, 'docs-versions.js'),
    path.join(docsRoot, 'scripts', 'docs-versions.js'),
  );
  return {docsRoot, root};
}

function writeInventory(docsRoot, versions) {
  fs.writeFileSync(
    path.join(docsRoot, 'versions.json'),
    `${JSON.stringify(versions, null, 2)}\n`,
  );
}

function writeVersion(root, major, marker) {
  const docs = path.join(root, 'versioned_docs', `version-${major}`);
  const sidebars = path.join(
    root,
    'versioned_sidebars',
    `version-${major}-sidebars.json`,
  );
  for (const section of sections) {
    const sectionRoot = path.join(docs, section);
    fs.mkdirSync(sectionRoot, {recursive: true});
    fs.writeFileSync(path.join(sectionRoot, 'index.md'), `${marker}-${section}\n`);
  }

  fs.mkdirSync(path.dirname(sidebars), {recursive: true});
  fs.writeFileSync(
    sidebars,
    `${JSON.stringify(Object.fromEntries(sidebarIds.map((id) => [id, []])), null, 2)}\n`,
  );
}

function run(docsRoot, ...args) {
  return spawnSync(
    process.execPath,
    [path.join(docsRoot, 'scripts', 'docs-versions.js'), ...args],
    {encoding: 'utf8'},
  );
}

function snapshotTree(root) {
  function visit(current, relative = '') {
    if (!fs.existsSync(current)) {
      return [];
    }

    return fs.readdirSync(current, {withFileTypes: true})
      .flatMap((entry) => {
        const entryPath = path.join(current, entry.name);
        const entryRelative = path.join(relative, entry.name);
        if (entry.isDirectory()) {
          return [`directory:${entryRelative}`, ...visit(entryPath, entryRelative)];
        }

        return [`file:${entryRelative}:${fs.readFileSync(entryPath, 'utf8')}`];
      });
  }

  return visit(root).sort();
}

test('imports a new publication-wide major with all four sections', () => {
  const fixture = createFixture();
  try {
    writeInventory(fixture.docsRoot, ['1']);
    writeVersion(fixture.docsRoot, '1', 'existing');
    const artifact = path.join(fixture.root, 'artifact');
    writeVersion(artifact, '2', 'released');

    const result = run(fixture.docsRoot, 'import', '2', artifact);

    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(
      JSON.parse(fs.readFileSync(path.join(fixture.docsRoot, 'versions.json'))),
      ['2', '1'],
    );
    for (const section of sections) {
      assert.match(
        fs.readFileSync(
          path.join(
            fixture.docsRoot,
            'versioned_docs',
            'version-2',
            section,
            'index.md',
          ),
          'utf8',
        ),
        new RegExp(`^released-${section}`),
      );
    }
  } finally {
    fs.rmSync(fixture.root, {force: true, recursive: true});
  }
});

test('refreshes an older major atomically, removes stale files, and is idempotent', () => {
  const fixture = createFixture();
  try {
    writeInventory(fixture.docsRoot, ['2', '1']);
    writeVersion(fixture.docsRoot, '2', 'newest');
    writeVersion(fixture.docsRoot, '1', 'old');
    fs.writeFileSync(
      path.join(
        fixture.docsRoot,
        'versioned_docs',
        'version-1',
        'validation',
        'stale.md',
      ),
      'stale\n',
    );
    const artifact = path.join(fixture.root, 'artifact');
    writeVersion(artifact, '1', 'maintenance');

    const first = run(fixture.docsRoot, 'import', '1', artifact);
    assert.equal(first.status, 0, first.stderr);
    assert.equal(
      fs.existsSync(
        path.join(
          fixture.docsRoot,
          'versioned_docs',
          'version-1',
          'validation',
          'stale.md',
        ),
      ),
      false,
    );
    const afterFirstImport = snapshotTree(fixture.docsRoot);

    const second = run(fixture.docsRoot, 'import', '1', artifact);
    assert.equal(second.status, 0, second.stderr);
    assert.deepEqual(snapshotTree(fixture.docsRoot), afterFirstImport);
    assert.deepEqual(
      JSON.parse(fs.readFileSync(path.join(fixture.docsRoot, 'versions.json'))),
      ['2', '1'],
    );
  } finally {
    fs.rmSync(fixture.root, {force: true, recursive: true});
  }
});

test('rejects incomplete, malformed, unexpected, and symlinked artifacts', async (t) => {
  async function assertRejected(configureArtifact, expectedMessage) {
    const fixture = createFixture();
    try {
      writeInventory(fixture.docsRoot, ['1']);
      writeVersion(fixture.docsRoot, '1', 'existing');
      const before = snapshotTree(fixture.docsRoot);
      const artifact = path.join(fixture.root, 'artifact');
      writeVersion(artifact, '1', 'released');
      configureArtifact(artifact);

      const result = run(fixture.docsRoot, 'import', '1', artifact);
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, expectedMessage);
      assert.deepEqual(snapshotTree(fixture.docsRoot), before);
    } finally {
      fs.rmSync(fixture.root, {force: true, recursive: true});
    }
  }

  await t.test('missing section index', () =>
    assertRejected(
      (artifact) =>
        fs.rmSync(
          path.join(
            artifact,
            'versioned_docs',
            'version-1',
            'settings',
            'index.md',
          ),
        ),
      /missing settings\/index\.md/,
    ));

  await t.test('missing sidebar key', () =>
    assertRejected(
      (artifact) =>
        fs.writeFileSync(
          path.join(
            artifact,
            'versioned_sidebars',
            'version-1-sidebars.json',
          ),
          `${JSON.stringify({validationSidebar: []})}\n`,
        ),
      /missing endpointsSidebar/,
    ));

  await t.test('malformed sidebar', () =>
    assertRejected(
      (artifact) =>
        fs.writeFileSync(
          path.join(
            artifact,
            'versioned_sidebars',
            'version-1-sidebars.json',
          ),
          '{',
        ),
      /JSON/,
    ));

  await t.test('unexpected path', () =>
    assertRejected(
      (artifact) => fs.writeFileSync(path.join(artifact, 'versions.json'), '["1"]\n'),
      /Unexpected documentation snapshot artifact path/,
    ));

  await t.test('nested symlink', () =>
    assertRejected(
      (artifact) =>
        fs.symlinkSync(
          path.join(
            artifact,
            'versioned_docs',
            'version-1',
            'validation',
            'index.md',
          ),
          path.join(
            artifact,
            'versioned_docs',
            'version-1',
            'validation',
            'linked.md',
          ),
        ),
      /must not contain symlinks/,
    ));
});

test('rolls back the unified target when validation fails after copying', () => {
  const fixture = createFixture();
  try {
    writeInventory(fixture.docsRoot, ['2', '1']);
    writeVersion(fixture.docsRoot, '2', 'newest');
    writeVersion(fixture.docsRoot, '1', 'old');
    fs.rmSync(
      path.join(
        fixture.docsRoot,
        'versioned_docs',
        'version-2',
        'mapping',
        'index.md',
      ),
    );
    const before = snapshotTree(fixture.docsRoot);
    const artifact = path.join(fixture.root, 'artifact');
    writeVersion(artifact, '1', 'replacement');

    const result = run(fixture.docsRoot, 'import', '1', artifact);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /missing mapping\/index\.md for major 2/);
    assert.deepEqual(snapshotTree(fixture.docsRoot), before);
  } finally {
    fs.rmSync(fixture.root, {force: true, recursive: true});
  }
});
