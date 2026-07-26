const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const stylesheet = fs.readFileSync(
  path.resolve(__dirname, '..', 'src', 'css', 'custom.css'),
  'utf8',
);

test('navbar blur does not constrain the fixed mobile navigation drawer', () => {
  const navbarRule = stylesheet.match(/\.navbar \{([\s\S]*?)\}/)?.[1];

  assert.ok(navbarRule);
  assert.doesNotMatch(navbarRule, /backdrop-filter/);
  assert.match(
    stylesheet,
    /@media \(min-width: 997px\) \{\s*\.navbar \{\s*backdrop-filter: blur\(14px\);\s*\}\s*\}/,
  );
});
