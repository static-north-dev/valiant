const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const {readCurrentMajor} = require('./current-major');

function withConfig(value, action) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'valiant-docs-major-'));
  const configPath = path.join(root, 'current-major.json');
  try {
    fs.writeFileSync(configPath, value);
    action(configPath);
  } finally {
    fs.rmSync(root, {force: true, recursive: true});
  }
}

test('reads a tracked SemVer major string', () => {
  for (const major of ['0', '1', '12']) {
    withConfig(JSON.stringify({major}), (configPath) => {
      assert.equal(readCurrentMajor(configPath), major);
    });
  }
});

test('rejects malformed or ambiguous documentation majors', () => {
  for (const config of [
    {},
    {major: 1},
    {major: ''},
    {major: '01'},
    {major: '-1'},
    {major: '1.0'},
  ]) {
    withConfig(JSON.stringify(config), (configPath) => {
      assert.throws(
        () => readCurrentMajor(configPath),
        /must contain a SemVer major string without a leading zero/,
      );
    });
  }
});

test('rejects missing and malformed current-major files', () => {
  assert.throws(
    () => readCurrentMajor('/path/that/does/not/exist/current-major.json'),
    /Could not read documentation major/,
  );
  withConfig('{', (configPath) => {
    assert.throws(
      () => readCurrentMajor(configPath),
      /Could not read documentation major/,
    );
  });
});
