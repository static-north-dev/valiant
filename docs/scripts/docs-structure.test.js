const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const docsRoot = path.resolve(__dirname, '..');
const sections = ['validation', 'endpoints', 'mapping', 'settings'];

test('one docs plugin owns all four release sections', () => {
  const config = fs.readFileSync(path.join(docsRoot, 'docusaurus.config.js'), 'utf8');
  assert.match(config, /path: 'content'/);
  assert.match(config, /routeBasePath: '\/'/);
  assert.doesNotMatch(config, /@docusaurus\/plugin-content-docs/);
  assert.equal(config.match(/type: 'docsVersionDropdown'/g)?.length, 1);
  assert.match(config, /className: 'navbar-version-selector'/);

  for (const section of sections) {
    const indexPath = path.join(docsRoot, 'content', section, 'index.md');
    assert.equal(fs.existsSync(indexPath), true, `Missing ${section} index.`);
    assert.doesNotMatch(fs.readFileSync(indexPath, 'utf8'), /^slug: \/$/m);
  }
});

test('the shared version selector remains a dropdown with one released major', () => {
  const component = fs.readFileSync(
    path.join(
      docsRoot,
      'src',
      'theme',
      'NavbarItem',
      'DocsVersionDropdownNavbarItem',
      'index.js',
    ),
    'utf8',
  );

  assert.match(component, /theme-classic 3\.10\.2/);
  assert.match(component, /<DropdownNavbarItem/);
  assert.doesNotMatch(component, /<DefaultNavbarItem/);
});

test('one sidebar file contains all four section sidebars', () => {
  const sidebars = fs.readFileSync(path.join(docsRoot, 'sidebars.js'), 'utf8');
  for (const section of sections) {
    assert.match(sidebars, new RegExp(`${section}Sidebar:`));
    assert.match(sidebars, new RegExp(`'${section}/index'`));
  }
});

test('obsolete per-section versioning state is absent', () => {
  for (const section of ['endpoints', 'mapping', 'settings']) {
    for (const suffix of ['versions.json', 'versioned_docs', 'versioned_sidebars']) {
      assert.equal(
        fs.existsSync(path.join(docsRoot, `${section}_${suffix}`)),
        false,
        `Obsolete ${section}_${suffix} still exists.`,
      );
    }
  }
});
