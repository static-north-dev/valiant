const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const docsRoot = path.resolve(__dirname, '..');
const config = require(path.join(docsRoot, 'docusaurus.config.js'));

test('the Valiant mark brands the navbar and favicon', () => {
  const logo = {
    alt: 'Valiant logo',
    src: 'img/valiant-logo.png',
  };

  assert.equal(config.favicon, logo.src);
  assert.deepEqual(config.themeConfig.navbar.logo, logo);
  assert.equal(config.themeConfig.navbar.title, 'Valiant');
  assert.equal(config.themeConfig.navbar.items.length, 5);
  assert.ok(fs.statSync(path.join(docsRoot, 'static', logo.src)).size > 0);
});
