const assert = require('node:assert/strict');
const path = require('node:path');
const test = require('node:test');

const {formatVersionLabel} = require(
  path.resolve(
    __dirname,
    '..',
    'src',
    'theme',
    'NavbarItem',
    'DocsVersionDropdownNavbarItem',
    'versionLabels.cjs',
  ),
);

test('numeric documentation majors receive an explicit version label', () => {
  assert.equal(formatVersionLabel({name: '1', label: '1'}), 'Version 1');
  assert.equal(formatVersionLabel({name: '12', label: '12'}), 'Version 12');
});

test('the development documentation version is labeled Next', () => {
  assert.equal(
    formatVersionLabel({name: 'current', label: 'current'}),
    'Next',
  );
});

test('configured labels remain authoritative', () => {
  assert.equal(
    formatVersionLabel({
      name: '1',
      label: 'Legacy',
      hasCustomLabel: true,
    }),
    'Legacy',
  );
});
