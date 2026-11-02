import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';

export function validateAgentArtifacts(outDir) {
  const read = (url) => fs.readFileSync(path.join(outDir, url), 'utf8');
  const json = (url) => JSON.parse(read(url));
  const required = [
    '/_worker.js', '/_worker.js.LEGAL.txt', '/_headers', '/_routes.json', '/agents/index.html',
    '/markdown/index.md', '/markdown/agents.md',
    '/.well-known/ard.json', '/.well-known/ai-catalog.json',
    '/.well-known/api-catalog', '/.well-known/poppy.json',
    '/.well-known/mcp/server-card.json', '/mcp/server-card',
    '/.well-known/agent-skills/index.json', '/openapi.json',
  ];
  for (const file of required) {
    assert.ok(read(file).length > 0, `Missing or empty agent artifact: ${file}`);
  }
  const headers = read('/_headers');
  assert.match(headers, /Content-Security-Policy: [^\n]*frame-ancestors 'none'/);
  assert.match(headers, /X-Frame-Options: DENY/);
  const index = json('/agents/documents.json');
  assert.ok(index.versions.some((version) => version.name === index.defaultVersion && version.isDefault));
  const paths = new Set();
  for (const document of index.documents) {
    assert.ok(!paths.has(document.path), `Duplicate published page path: ${document.path}`);
    paths.add(document.path);
    assert.ok(index.versions.some((version) => version.name === document.version));
    assert.equal(read(document.markdownUrl), document.content, `Markdown/index mismatch: ${document.path}`);
    assert.ok(document.content.startsWith('---\n'));
    const route = document.path.replace(/\/$/, '');
    assert.ok(fs.existsSync(path.join(outDir, route, 'index.html')) || fs.existsSync(path.join(outDir, `${route}.html`)), `Missing HTML page for ${document.path}`);
  }
  assert.ok(paths.size > 0);
  assert.ok(read('/llms.txt').includes(`Default version: ${index.defaultVersion}.`));
  assert.ok(read('/llms-full.txt').includes(`Documentation version: ${index.defaultVersion}.`));
  for (const module of ['validation', 'endpoints', 'mapping', 'settings']) {
    assert.ok(read(`/llms-${module}.txt`).includes(`Documentation version: ${index.defaultVersion}.`));
  }
  const skillIndex = json('/.well-known/agent-skills/index.json');
  assert.equal(skillIndex.$schema, 'https://schemas.agentskills.io/discovery/0.2.0/schema.json');
  assert.equal(skillIndex.skills.length, 4);
  for (const skill of skillIndex.skills) {
    assert.equal(skill.type, 'skill-md');
    const digest = `sha256:${createHash('sha256').update(read(skill.url)).digest('hex')}`;
    assert.equal(skill.digest, digest, `Incorrect skill digest for ${skill.name}`);
  }
  const openapi = json('/openapi.json');
  assert.equal(openapi.openapi, '3.1.0');
  const siteUrl = openapi.servers[0].url;
  const checkTarget = (url) => {
    const target = new URL(url, siteUrl);
    assert.equal(target.origin, new URL(siteUrl).origin);
    if (target.pathname === '/mcp' || Object.hasOwn(openapi.paths, target.pathname)) {
      return;
    }
    assert.ok(fs.existsSync(path.join(outDir, target.pathname)) || fs.existsSync(path.join(outDir, target.pathname, 'index.html')), `Missing discovery target: ${url}`);
  };
  const catalog = json('/.well-known/ard.json');
  assert.deepEqual(json('/.well-known/ai-catalog.json'), catalog);
  for (const entry of catalog.entries) {
    assert.match(entry.identifier, /^urn:air:[a-zA-Z0-9.-]+(:[a-zA-Z0-9._-]+)+$/);
    assert.ok(entry.displayName && entry.type);
    assert.ok(entry.url && !entry.data);
    assert.ok(entry.representativeQueries.length >= 2);
    checkTarget(entry.url);
  }
  const card = json('/mcp/server-card');
  assert.deepEqual(json('/.well-known/mcp/server-card.json'), card);
  assert.match(card.name, /^[a-zA-Z0-9.-]+\/[a-zA-Z0-9._-]+$/);
  assert.equal(card.remotes[0].type, 'streamable-http');
  checkTarget(card.remotes[0].url);
  for (const linkset of json('/.well-known/api-catalog').linkset) {
    checkTarget(linkset.anchor);
    for (const relation of ['service-desc', 'service-doc', 'status']) {
      for (const link of linkset[relation]) {
        checkTarget(link.href);
      }
    }
  }
  const poppy = json('/.well-known/poppy.json');
  assert.equal(poppy.protocol_version, '0.3');
  assert.equal(poppy.organization.domain, new URL(siteUrl).hostname);
  assert.deepEqual(poppy.web, {});
  assert.equal(poppy.auth, undefined);
  for (const match of read('/agents/index.html').matchAll(/href="(\/[^"#]*)"/g)) {
    checkTarget(match[1]);
  }
  return {documents: index.documents.length, versions: index.versions.length, skills: skillIndex.skills.length};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const outDir = path.resolve(process.argv[2] ?? path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'build'));
  const result = validateAgentArtifacts(outDir);
  console.log(`Validated agent artifacts: ${result.documents} documents, ${result.versions} versions, ${result.skills} skills.`);
}
