const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const homepage = fs.readFileSync(
  path.resolve(__dirname, '..', 'src', 'pages', 'index.js'),
  'utf8',
);

test('homepage presents the complete source-generator family', () => {
  for (const marker of [
    '[ValiantValidator]',
    '[ValiantEndpoint]',
    '[ValiantMapper]',
    '[ValiantSettings]',
  ]) {
    assert.match(homepage, new RegExp(marker.replaceAll('[', '\\[').replaceAll(']', '\\]')));
  }

  assert.match(homepage, /incremental source generation/);
  assert.match(homepage, /outputs across the Valiant family/);
  assert.doesNotMatch(homepage, /UserMapper|MapperDefinition/);
});

test('homepage actions are module-neutral', () => {
  assert.match(homepage, /href="#modules"[\s\S]*Explore the generators/);
  assert.match(homepage, /href="#approach"[\s\S]*Why source generation/);
  assert.doesNotMatch(homepage, /Build your first validator/);
});
