const {spawnSync} = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const docsRoot = path.resolve(__dirname, '..');
const versionsFile = 'versions.json';
const versionedDocs = 'versioned_docs';
const versionedSidebars = 'versioned_sidebars';
const sections = ['validation', 'endpoints', 'mapping', 'settings'];
const sidebarIds = sections.map((section) => `${section}Sidebar`);

function fail(message) {
  throw new Error(message);
}

function requireMajor(value) {
  if (!/^(0|[1-9][0-9]*)$/.test(value ?? '')) {
    fail(`Documentation version '${value ?? ''}' must be a SemVer major without a leading zero.`);
  }

  return value;
}

function resolveAt(root, relativePath) {
  return path.join(root, relativePath);
}

function resolve(relativePath) {
  return resolveAt(docsRoot, relativePath);
}

function readVersions(allowMissing = false) {
  const versionsPath = resolve(versionsFile);
  if (!fs.existsSync(versionsPath)) {
    if (allowMissing) {
      return [];
    }

    fail(`Documentation is missing ${versionsFile}.`);
  }

  const versions = JSON.parse(fs.readFileSync(versionsPath, 'utf8'));
  if (!Array.isArray(versions) || versions.some((version) => typeof version !== 'string')) {
    fail(`${versionsFile} must contain an array of version strings.`);
  }

  if (new Set(versions).size !== versions.length) {
    fail(`${versionsFile} must not contain duplicate versions.`);
  }

  return versions;
}

function writeVersions(versions) {
  fs.writeFileSync(
    resolve(versionsFile),
    `${JSON.stringify(versions, null, 2)}\n`,
  );
}

function versionPaths(major, root = docsRoot) {
  return {
    docs: resolveAt(root, path.join(versionedDocs, `version-${major}`)),
    sidebars: resolveAt(
      root,
      path.join(versionedSidebars, `version-${major}-sidebars.json`),
    ),
  };
}

function validateSnapshot(paths, major, context) {
  if (!fs.existsSync(paths.docs) || !fs.statSync(paths.docs).isDirectory()) {
    fail(`${context} is missing versioned docs for major ${major}.`);
  }

  for (const section of sections) {
    const sectionIndex = path.join(paths.docs, section, 'index.md');
    if (!fs.existsSync(sectionIndex) || !fs.statSync(sectionIndex).isFile()) {
      fail(`${context} is missing ${section}/index.md for major ${major}.`);
    }
  }

  if (!fs.existsSync(paths.sidebars) || !fs.statSync(paths.sidebars).isFile()) {
    fail(`${context} is missing the versioned sidebar for major ${major}.`);
  }

  const sidebars = JSON.parse(fs.readFileSync(paths.sidebars, 'utf8'));
  for (const sidebarId of sidebarIds) {
    if (!Object.prototype.hasOwnProperty.call(sidebars, sidebarId)) {
      fail(`${context} sidebar for major ${major} is missing ${sidebarId}.`);
    }
  }
}

function validateInventory(requiredMajor) {
  const versions = readVersions();
  const descendingVersions = [...versions].sort(
    (left, right) => Number(right) - Number(left),
  );
  if (JSON.stringify(versions) !== JSON.stringify(descendingVersions)) {
    fail(`${versionsFile} must list majors from newest to oldest.`);
  }

  for (const version of versions) {
    requireMajor(version);
    validateSnapshot(versionPaths(version), version, 'Documentation');
  }

  if (requiredMajor !== undefined && !versions.includes(requiredMajor)) {
    fail(`Documentation does not contain major ${requiredMajor}.`);
  }

  console.log(`Validated documentation majors: ${versions.join(', ')}.`);
  return versions;
}

function versioningPaths() {
  return [versionsFile, versionedDocs, versionedSidebars];
}

function backupVersioningState() {
  const backupRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'valiant-docs-versions-'));
  for (const relativePath of versioningPaths()) {
    const source = resolve(relativePath);
    if (fs.existsSync(source)) {
      fs.cpSync(source, path.join(backupRoot, relativePath), {recursive: true});
    }
  }

  return backupRoot;
}

function restoreVersioningState(backupRoot) {
  for (const relativePath of versioningPaths()) {
    fs.rmSync(resolve(relativePath), {force: true, recursive: true});
    const backup = path.join(backupRoot, relativePath);
    if (fs.existsSync(backup)) {
      fs.cpSync(backup, resolve(relativePath), {recursive: true});
    }
  }
}

function runVersionCommand(major) {
  const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const result = spawnSync(
    npm,
    ['run', 'docusaurus', '--', 'docs:version', major],
    {cwd: docsRoot, stdio: 'inherit'},
  );

  if (result.status !== 0) {
    fail(`Documentation versioning failed with exit code ${result.status ?? 'unknown'}.`);
  }
}

function snapshotMajor(major) {
  const previousVersions = readVersions(true);
  const refreshing = previousVersions.includes(major);
  if (!refreshing && previousVersions.some((version) => Number(version) >= Number(major))) {
    fail(`New documentation major ${major} must be newer than every committed major.`);
  }

  const backupRoot = backupVersioningState();
  try {
    const paths = versionPaths(major);
    fs.rmSync(paths.docs, {force: true, recursive: true});
    fs.rmSync(paths.sidebars, {force: true});

    if (previousVersions.length === 0) {
      fs.rmSync(resolve(versionsFile), {force: true});
    } else {
      writeVersions(previousVersions.filter((version) => version !== major));
    }

    runVersionCommand(major);

    const finalVersions = refreshing ? previousVersions : [major, ...previousVersions];
    writeVersions(finalVersions);

    validateInventory(major);
    console.log(`${refreshing ? 'Refreshed' : 'Created'} documentation major ${major}.`);
  } catch (error) {
    restoreVersioningState(backupRoot);
    throw error;
  } finally {
    fs.rmSync(backupRoot, {force: true, recursive: true});
  }
}

function assertSafeSnapshotArtifact(sourceRoot, major) {
  if (
    !fs.existsSync(sourceRoot) ||
    fs.lstatSync(sourceRoot).isSymbolicLink() ||
    !fs.statSync(sourceRoot).isDirectory()
  ) {
    fail(`Documentation snapshot artifact '${sourceRoot}' is not a directory.`);
  }

  const allowedContainers = new Set([versionedDocs, versionedSidebars]);
  const allowedTargets = [
    path.join(versionedDocs, `version-${major}`),
    path.join(versionedSidebars, `version-${major}-sidebars.json`),
  ];

  function inspect(currentPath, relativePath = '') {
    for (const entry of fs.readdirSync(currentPath, {withFileTypes: true})) {
      const entryRelativePath = path.join(relativePath, entry.name);
      const entryPath = path.join(currentPath, entry.name);
      const entryStats = fs.lstatSync(entryPath);
      if (entryStats.isSymbolicLink()) {
        fail(`Documentation snapshot artifacts must not contain symlinks: ${entryRelativePath}.`);
      }

      const allowed =
        allowedContainers.has(entryRelativePath) ||
        allowedTargets.some(
          (target) =>
            entryRelativePath === target ||
            entryRelativePath.startsWith(`${target}${path.sep}`),
        );
      if (!allowed) {
        fail(`Unexpected documentation snapshot artifact path: ${entryRelativePath}.`);
      }

      if (entryStats.isDirectory()) {
        inspect(entryPath, entryRelativePath);
      }
    }
  }

  inspect(sourceRoot);
  validateSnapshot(versionPaths(major, sourceRoot), major, 'Documentation snapshot');
}

function importMajor(major, sourceRootValue) {
  if (!sourceRootValue) {
    fail('A documentation snapshot artifact path is required.');
  }

  const sourceRoot = path.resolve(sourceRootValue);
  assertSafeSnapshotArtifact(sourceRoot, major);

  const previousVersions = readVersions(true);
  const finalVersions = [...new Set([major, ...previousVersions])].sort(
    (left, right) => Number(right) - Number(left),
  );
  const backupRoot = backupVersioningState();
  try {
    const sourcePaths = versionPaths(major, sourceRoot);
    const destinationPaths = versionPaths(major);
    fs.rmSync(destinationPaths.docs, {force: true, recursive: true});
    fs.rmSync(destinationPaths.sidebars, {force: true});
    fs.mkdirSync(path.dirname(destinationPaths.docs), {recursive: true});
    fs.mkdirSync(path.dirname(destinationPaths.sidebars), {recursive: true});
    fs.cpSync(sourcePaths.docs, destinationPaths.docs, {recursive: true});
    fs.copyFileSync(sourcePaths.sidebars, destinationPaths.sidebars);
    writeVersions(finalVersions);

    validateInventory(major);
    console.log(`Imported documentation major ${major}.`);
  } catch (error) {
    restoreVersioningState(backupRoot);
    throw error;
  } finally {
    fs.rmSync(backupRoot, {force: true, recursive: true});
  }
}

function main() {
  const [command, majorValue, sourceRoot] = process.argv.slice(2);
  const major = requireMajor(majorValue);

  if (command === 'snapshot') {
    snapshotMajor(major);
    return;
  }

  if (command === 'validate') {
    validateInventory(major);
    return;
  }

  if (command === 'import') {
    importMajor(major, sourceRoot);
    return;
  }

  fail('Usage: docs-versions.js <snapshot|validate|import> <major> [snapshot-artifact-path].');
}

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
}
