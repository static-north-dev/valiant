const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const docsRoot = path.resolve(__dirname, '..');
const repositoryRoot = path.resolve(docsRoot, '..');
const config = require(path.join(docsRoot, 'docusaurus.config.js'));
const footer = config.themeConfig.footer;

test('footer links the text-only Static North attribution', () => {
  assert.equal(footer.style, 'light');
  assert.equal(footer.logo, undefined);
  assert.equal(
    footer.copyright,
    'Valiant is an open-source project from <a class="static-north-footer-link" href="https://staticnorth.dev/">Static North</a>.',
  );
  assert.equal(
    fs.existsSync(
      path.join(docsRoot, 'static', 'img', 'static-north-logo-horizontal.svg'),
    ),
    false,
  );
});

test('footer styling preserves text contrast and keyboard focus', () => {
  const css = fs.readFileSync(
    path.join(docsRoot, 'src', 'css', 'custom.css'),
    'utf8',
  );

  assert.match(
    css,
    /\.footer \.static-north-footer-link:focus-visible[\s\S]*outline: 2px solid/,
  );
  assert.doesNotMatch(css, /static-north-footer-logo|footer__logo/);
});

test('Docusaurus override preserves its MIT attribution', () => {
  const override = fs.readFileSync(
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
  const notices = fs.readFileSync(
    path.join(repositoryRoot, 'THIRD_PARTY_NOTICES.md'),
    'utf8',
  );

  assert.match(override, /Copyright \(c\) Facebook, Inc\. and its affiliates\./);
  assert.match(override, /THIRD_PARTY_NOTICES\.md/);
  assert.match(notices, /@docusaurus\/theme-classic` 3\.10\.2/);
  assert.match(notices, /MIT License/);
  assert.match(notices, /Copyright \(c\) Facebook, Inc\. and its affiliates\./);
});
