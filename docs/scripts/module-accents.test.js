const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const docsRoot = path.resolve(__dirname, '..');
const stylesheet = fs.readFileSync(path.join(docsRoot, 'src', 'css', 'custom.css'), 'utf8');

const modules = [
  {
    section: 'validation',
    lightAccent: '#2878d0',
    lightText: '#1f67b4',
    darkAccent: '#73b7ff',
    nestedPage: 'validation/quick-start/index.html',
  },
  {
    section: 'endpoints',
    lightAccent: '#bd6b18',
    lightText: '#9a520e',
    darkAccent: '#ffb35d',
    nestedPage: 'endpoints/guides/request-binding/index.html',
  },
  {
    section: 'mapping',
    lightAccent: '#7556d8',
    lightText: '#6443c2',
    darkAccent: '#b49aff',
    nestedPage: 'mapping/guides/explicit-mapping/index.html',
  },
  {
    section: 'settings',
    lightAccent: '#17866b',
    lightText: '#126f59',
    darkAccent: '#5ad6b5',
    nestedPage: 'settings/guides/schema-generation/index.html',
  },
];

function luminance(hex) {
  const channels = hex.slice(1).match(/../g).map((channel) => Number.parseInt(channel, 16) / 255);
  const [red, green, blue] = channels.map((channel) => (
    channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  ));

  return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
}

function contrast(foreground, background) {
  const [light, dark] = [luminance(foreground), luminance(background)].sort((left, right) => right - left);
  return (light + 0.05) / (dark + 0.05);
}

test('each module has scoped accessible accents in both themes', () => {
  for (const module of modules) {
    assert.match(stylesheet, new RegExp(`html\\[class\\*='docs-doc-id-${module.section}/'\\]`));
    assert.match(stylesheet, new RegExp(`html\\[data-theme='dark'\\]\\[class\\*='docs-doc-id-${module.section}/'\\]`));
    assert.match(stylesheet, new RegExp(`--valiant-module-accent: ${module.lightAccent};`));
    assert.match(stylesheet, new RegExp(`--valiant-module-accent-text: ${module.lightText};`));
    assert.match(stylesheet, new RegExp(`--valiant-module-accent: ${module.darkAccent};`));
    assert.ok(contrast(module.lightText, '#ffffff') >= 4.5, `${module.section} light accent text must meet AA contrast`);
    assert.ok(contrast(module.darkAccent, '#0d1522') >= 4.5, `${module.section} dark accent must meet AA contrast`);
  }

  assert.match(stylesheet, /\) \.main-wrapper \{\n  --ifm-color-primary: var\(--valiant-module-accent-text\);/);
  assert.match(stylesheet, /\.theme-doc-markdown :not\(pre\) > code/);
  assert.match(stylesheet, /\.theme-doc-markdown h1::after/);
});

test('built nested module pages expose the route classes used for accent scoping', {
  skip: !fs.existsSync(path.join(docsRoot, 'build')),
}, () => {
  for (const module of modules) {
    const page = fs.readFileSync(path.join(docsRoot, 'build', module.nestedPage), 'utf8');
    assert.match(page, new RegExp(`docs-doc-id-${module.section}/`));
  }

  const homepage = fs.readFileSync(path.join(docsRoot, 'build', 'index.html'), 'utf8');
  assert.doesNotMatch(homepage, /docs-doc-id-(?:validation|endpoints|mapping|settings)\//);
});
