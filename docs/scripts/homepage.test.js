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

  assert.match(homepage, /MapperDefinition<Order, OrderDto>/);
  assert.match(homepage, /return new OrderDto\(source.Id, source.Total\)/);
});

test('homepage actions are module-neutral', () => {
  assert.match(homepage, /href="#modules"[\s\S]*Explore the generators/);
  assert.match(homepage, /to="\/validation\/reference\/packages"[\s\S]*Packages and compatibility/);
  assert.match(homepage, /to="\/endpoints\/guides\/validated-api"/);
  assert.doesNotMatch(homepage, /Build your first validator/);
});
