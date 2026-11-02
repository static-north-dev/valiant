const assert = require('node:assert/strict');
const childProcess = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const {pathToFileURL} = require('node:url');

const docsRoot = path.resolve(__dirname, '..');
const staticRoot = path.join(docsRoot, 'static');
const generatorPath = path.join(__dirname, 'generate-llms.mjs');

function readStatic(name) {
  return fs.readFileSync(path.join(staticRoot, name), 'utf8');
}

test('llms manifest follows the curated root-file contract', () => {
  const manifest = readStatic('llms.txt');

  assert.match(manifest, /^# StaticNorth\.Valiant\n\n> .+\n/m);
  assert.match(manifest, /## Documentation[\s\S]*https:\/\/valiant\.staticnorth\.dev\/validation\//);
  assert.match(manifest, /## Packages and source[\s\S]*https:\/\/github\.com\/static-north-dev\/valiant/);
  assert.match(manifest, /## Optional[\s\S]*https:\/\/valiant\.staticnorth\.dev\/llms-full\.txt/);

  for (const url of manifest.matchAll(/\((https?:\/\/[^)]+)\)/g)) {
    assert.ok(url[1].startsWith('https://'), `manifest URL must use HTTPS: ${url[1]}`);
  }
});

test('llms full context is generated from current sidebar documentation only', async () => {
  const module = await import(pathToFileURL(generatorPath));
  const fullContext = readStatic('llms-full.txt');

  assert.equal(fullContext, module.generate());
  assert.match(fullContext, /^# StaticNorth\.Valiant — Full Documentation\n\n> .+/);
  assert.match(fullContext, /current documentation only/);
  assert.match(fullContext, /## Validation\n[\s\S]*### Validation you can step through/);
  assert.match(fullContext, /## Endpoints\n[\s\S]*### Minimal APIs, organized by feature/);
  assert.match(fullContext, /## Mapping\n[\s\S]*### Mapping that stays visible/);
  assert.match(fullContext, /## Settings\n[\s\S]*### Settings with a generated contract/);
  assert.match(fullContext, /using StaticNorth\.Valiant\.Validation;/);
  assert.match(fullContext, /https:\/\/github\.com\/static-north-dev\/valiant/);
  assert.doesNotMatch(fullContext, /https:\/\/github\.com\/ignitea-dev\/valiant/);
  assert.doesNotMatch(fullContext, /versioned_docs/);
  assert.doesNotMatch(fullContext, /https:\/\/valiant\.dev\/[^)\s]+\.md(?:[#)]|$)/);
});

test('llms generator check detects the committed current context', () => {
  const result = childProcess.spawnSync(process.execPath, [generatorPath, '--check'], {
    cwd: docsRoot,
    encoding: 'utf8',
  });

  assert.equal(result.status, 0, result.stderr);
});

test('robots allows crawling and points to the generated sitemap', () => {
  assert.equal(
    readStatic('robots.txt'),
    'User-agent: *\nContent-Signal: search=yes, ai-input=yes\nAllow: /\n\nSitemap: https://valiant.staticnorth.dev/sitemap.xml\nAgentmap: https://valiant.staticnorth.dev/.well-known/ard.json\n',
  );
});
