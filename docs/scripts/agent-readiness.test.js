const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {after, before, test} = require('node:test');

const docsRoot = path.resolve(__dirname, '..');
const load = (file) => import(pathToFileURL(path.join(docsRoot, file)));
let fixture;
let index;
let worker;
let env;
const assetRequests = [];

before(async () => {
  fixture = await fs.mkdtemp(path.join(os.tmpdir(), 'valiant-agent-docs-'));
  await fs.mkdir(path.join(fixture, 'static'));
  await fs.copyFile(path.join(docsRoot, 'static/llms.txt'), path.join(fixture, 'static/llms.txt'));
  await fs.copyFile(path.join(docsRoot, 'agent-access.md'), path.join(fixture, 'agent-access.md'));
  await fs.cp(path.join(docsRoot, 'agent-skills'), path.join(fixture, 'agent-skills'), {recursive: true});
  const versions = [];
  for (const name of ['1', '2', 'current']) {
    const docs = [];
    for (const module of ['validation', 'endpoints', 'mapping', 'settings']) {
      const prefix = name === '2' ? '' : name === 'current' ? '/next' : '/1';
      const source = `source-${name}/${module}/index.md`;
      await fs.mkdir(path.dirname(path.join(fixture, source)), {recursive: true});
      const body = `---\ntitle: ${module}\n---\n\n# ${module}\n\nUnique documentation ${name}. Validator composition for ${module}.\n`;
      await fs.writeFile(path.join(fixture, source), body);
      docs.push({id: `${module}/index`, source: `@site/${source}`, title: module, description: `${module} guide`, permalink: `${prefix}/${module}/`});
    }
    const quick = `source-${name}/validation/quick-start.md`;
    await fs.writeFile(path.join(fixture, quick), '# Quick start\n\n## Install\n\n```csharp\ncontext.Property(x => x.Name).Required();\n```\n');
    docs.push({id: 'validation/quick-start', source: `@site/${quick}`, title: 'Quick start', description: 'Install validation', permalink: `${name === '2' ? '' : name === 'current' ? '/next' : '/1'}/validation/quick-start`});
    await fs.appendFile(path.join(fixture, `source-${name}/validation/index.md`), '\n[Quick start](./quick-start.md#install)\n\n````markdown\n[Literal](./missing.md)\n```\n````\n');
    docs.push({id: 'validation/draft', source: '@site/absent-draft.md', draft: true});
    docs.push({id: 'validation/unlisted', source: '@site/absent-unlisted.md', unlisted: true});
    versions.push({versionName: name, label: name, path: name === '2' ? '/' : `/${name}`, isLast: name === '2', docs});
  }
  const {buildAgentArtifacts} = await load('scripts/build-agent-artifacts.mjs');
  const result = await buildAgentArtifacts({siteDir: fixture, outDir: path.join(fixture, 'build'), versions, siteUrl: 'https://valiant.staticnorth.dev', sourceRevision: 'test-revision'});
  index = result.index;
  for (const document of index.documents) {
    const file = path.join(fixture, 'build', document.path, 'index.html');
    await fs.mkdir(path.dirname(file), {recursive: true});
    await fs.writeFile(file, '<html>Documentation</html>');
  }
  await fs.writeFile(path.join(fixture, 'build/agents/index.html'), '<html><a href="/llms.txt">Documentation</a></html>');
  const {createWorker} = await load('src/agents/worker.mjs');
  worker = createWorker({markdownRoutes: result.markdownRoutes});
  env = {ASSETS: {fetch: async (request) => {
    const pathname = new URL(request.url).pathname;
    assetRequests.push(pathname);
    try {
      const content = await fs.readFile(path.join(fixture, 'build', pathname));
      return new Response(request.method === 'HEAD' ? null : content, {headers: {'Content-Type': 'text/plain', ETag: '"asset"', 'Cache-Control': 'public, max-age=0'}});
    } catch {
      if (result.markdownRoutes[pathname.replace(/\/$/, '') || '/']) {
        return new Response(request.method === 'HEAD' ? null : '<html>Documentation</html>', {headers: {'Content-Type': 'text/html', Vary: 'Accept-Encoding', 'X-Frame-Options': 'DENY'}});
      }
      return new Response('Not found', {status: 404});
    }
  }}};
});

after(async () => {
  if (fixture) {
    await fs.rm(fixture, {recursive: true, force: true});
  }
});

const request = (route, options) => worker.fetch(new Request(`https://valiant.staticnorth.dev${route}`, options), env);

test('Markdown exports isolate historical, current, and default documentation and preserve code', async () => {
  assert.equal(index.defaultVersion, '2');
  assert.equal(index.documents.length, 15);
  const stable = index.documents.find((document) => document.path === '/validation/');
  const historical = index.documents.find((document) => document.path === '/1/validation/');
  assert.ok(stable.content.includes('https://valiant.staticnorth.dev/validation/quick-start#install'));
  assert.ok(historical.content.includes('https://valiant.staticnorth.dev/1/validation/quick-start#install'));
  assert.ok(stable.content.includes('[Literal](./missing.md)'));
  assert.ok(index.documents.find((document) => document.path === '/validation/quick-start').content.includes('```csharp\ncontext.Property(x => x.Name).Required();\n```'));
  const full = await fs.readFile(path.join(fixture, 'build/llms-full.txt'), 'utf8');
  assert.ok(full.includes('Unique documentation 2.'));
  assert.ok(!full.includes('Unique documentation 1.'));
  assert.ok(!full.includes('Unique documentation current.'));
});

test('negotiation honors explicit Markdown, HTML preference, q=0, and wildcard defaults', async () => {
  const {prefersMarkdown} = await load('src/agents/worker.mjs');
  for (const accept of ['', '*/*', 'text/*', 'text/markdown;q=0', 'text/markdown;q=0, */*;q=1', 'text/markdown;q=0.2,text/html;q=0.9', 'text/markdown;q=garbage']) {
    assert.equal(prefersMarkdown(accept), false, accept);
  }
  for (const accept of ['text/markdown', 'text/markdown, */*', 'text/markdown;q=0.9,text/html;q=0.1', 'text/markdown;q=1,text/html;q=0']) {
    assert.equal(prefersMarkdown(accept), true, accept);
  }
});

test('known pages return real Markdown, preserve cache variants, and support HEAD', async () => {
  const markdown = await request('/validation/', {headers: {Accept: 'text/markdown'}});
  assert.equal(markdown.status, 200);
  assert.equal(markdown.headers.get('Content-Type'), 'text/markdown; charset=utf-8');
  assert.ok(markdown.headers.get('Link').includes('rel="alternate"'));
  assert.ok(markdown.headers.get('Vary').includes('Accept'));
  assert.ok((await markdown.text()).includes('Unique documentation 2.'));
  const html = await request('/validation/', {headers: {Accept: 'text/html'}});
  assert.equal(html.headers.get('Content-Type'), 'text/html');
  assert.equal(html.headers.get('X-Frame-Options'), 'DENY');
  assert.equal(html.headers.get('Vary'), 'Accept-Encoding, Accept');
  const head = await request('/validation/', {method: 'HEAD', headers: {Accept: 'text/markdown'}});
  assert.equal(head.status, 200);
  assert.equal(await head.text(), '');
  assert.equal((await request('/unknown/', {headers: {Accept: 'text/markdown'}})).status, 404);
  const asset = await request('/markdown/validation.md');
  assert.equal(asset.status, 200);
});

test('documentation API defaults to stable, bounds searches, and never fetches caller paths', async () => {
  const list = await (await request('/api/docs')).json();
  assert.equal(list.documents.length, 5);
  assert.ok(list.documents.every((document) => document.version === '2' && !document.content));
  const search = await (await request('/api/docs/search?query=composition&limit=1')).json();
  assert.equal(search.results.length, 1);
  assert.equal(search.total, 4);
  assert.ok(search.results[0].excerpt.length <= 500);
  const history = await (await request('/api/docs/search?query=composition&version=1')).json();
  assert.ok(history.results.every((document) => document.version === '1'));
  const count = assetRequests.length;
  const missing = await request('/api/docs/read?path=' + encodeURIComponent('/.well-known/secret'));
  assert.equal(missing.status, 404);
  assert.deepEqual(assetRequests.slice(count), ['/agents/documents.json']);
  assert.equal((await request('/api/docs/read?path=https://example.com/')).status, 400);
  assert.equal((await request('/api/docs/search?query=composition&limit=100')).status, 400);
  assert.equal((await request('/api/docs?version=missing')).status, 400);
  assert.equal((await request('/api/docs/search?query=!!!')).status, 400);
  assert.equal((await request('/api/docs', {method: 'POST'})).status, 405);
  const head = await request('/api/docs', {method: 'HEAD'});
  assert.equal(head.status, 200);
  assert.equal(await head.text(), '');
});

test('an unavailable documentation index is an explicit service failure', async () => {
  const response = await worker.fetch(new Request('https://valiant.staticnorth.dev/api/docs/search?query=validation'), {ASSETS: {fetch: async () => new Response('unavailable', {status: 503})}});
  assert.equal(response.status, 503);
  assert.equal((await response.json()).error, 'Documentation is temporarily unavailable.');
  const invalid = await worker.fetch(new Request('https://valiant.staticnorth.dev/api/docs'), {ASSETS: {fetch: async () => Response.json({})}});
  assert.equal(invalid.status, 503);
});

test('official MCP client initializes, lists tools, and reads the same versioned documents', async () => {
  const {Client} = await import('@modelcontextprotocol/sdk/client/index.js');
  const {StreamableHTTPClientTransport} = await import('@modelcontextprotocol/sdk/client/streamableHttp.js');
  const client = new Client({name: 'valiant-test', version: '1.0.0'});
  const transport = new StreamableHTTPClientTransport(new URL('https://valiant.staticnorth.dev/mcp'), {
    fetch: (url, init) => worker.fetch(new Request(url, init), env),
  });
  try {
    await client.connect(transport);
    const tools = await client.listTools();
    assert.deepEqual(tools.tools.map((tool) => tool.name), ['list_documents', 'search_documents', 'read_document']);
    assert.ok(tools.tools.every((tool) => tool.annotations.readOnlyHint));
    const read = await client.callTool({name: 'read_document', arguments: {path: '/1/validation/'}});
    assert.equal(JSON.parse(read.content[0].text).version, '1');
    const search = await client.callTool({name: 'search_documents', arguments: {query: 'composition', module: 'mapping'}});
    assert.equal(JSON.parse(search.content[0].text).results[0].module, 'mapping');
    const missing = await client.callTool({name: 'read_document', arguments: {path: '/private'}});
    assert.equal(missing.isError, true);
    const invalid = await client.callTool({name: 'search_documents', arguments: {query: 'composition', limit: 200}});
    assert.equal(invalid.isError, true);
  } finally {
    await client.close();
  }
});

test('MCP rejects unexpected origins and oversized messages and offers no persistent stream', async () => {
  const headers = {'Content-Type': 'application/json', Accept: 'application/json, text/event-stream'};
  const denied = await request('/mcp', {method: 'POST', headers: {...headers, Origin: 'https://unexpected.example'}, body: '{}'});
  assert.equal(denied.status, 403);
  const preflight = await request('/mcp', {method: 'OPTIONS', headers: {Origin: 'https://unexpected.example'}});
  assert.equal(preflight.status, 403);
  const large = await request('/mcp', {method: 'POST', headers, body: JSON.stringify({jsonrpc: '2.0', id: 1, method: 'initialize', padding: 'x'.repeat(20000)})});
  assert.equal(large.status, 413);
  assert.equal((await request('/mcp', {headers: {Accept: 'text/event-stream'}})).status, 405);
});

test('Server Cards support browser preflight and conditional ETag requests', async () => {
  for (const path of ['/mcp/server-card', '/.well-known/mcp/server-card.json']) {
    const preflight = await request(path, {method: 'OPTIONS', headers: {Origin: 'https://client.example', 'Access-Control-Request-Method': 'GET', 'Access-Control-Request-Headers': 'If-None-Match'}});
    assert.equal(preflight.status, 204);
    assert.equal(preflight.headers.get('Access-Control-Allow-Origin'), '*');
    assert.ok(preflight.headers.get('Access-Control-Allow-Headers').includes('If-None-Match'));
    const card = await request(path);
    assert.equal(card.status, 200);
    assert.equal(card.headers.get('Content-Type'), 'application/mcp-server-card+json');
    assert.equal(card.headers.get('Access-Control-Expose-Headers'), 'ETag');
    assert.ok(card.headers.get('ETag'));
    assert.equal((await request(path, {method: 'POST'})).status, 405);
  }
});

test('WebMCP feature detection, registration, and execution use the shared read-only tools', async () => {
  const {registerDocumentationTools} = await load('src/agents/webmcp.mjs');
  assert.equal(await registerDocumentationTools(undefined, async () => index), false);
  const tools = [];
  const context = {registerTool: async (tool) => {tools.push(tool);}};
  assert.equal(await registerDocumentationTools(context, async () => index), true);
  await registerDocumentationTools(context, async () => index);
  assert.equal(tools.length, 3);
  const result = await tools.find((tool) => tool.name === 'read_document').execute({path: '/validation/'});
  assert.equal(JSON.parse(result.content[0].text).version, '2');
  let signal;
  const failing = {registerTool: async (tool, options) => {signal = options.signal; throw new Error('Registration rejected.');}};
  await assert.rejects(registerDocumentationTools(failing, async () => index), /Registration rejected/);
  assert.equal(signal.aborted, true);
});

test('skill digests and catalog entries resolve to real built resources', async () => {
  const {createHash} = require('node:crypto');
  const skills = JSON.parse(await fs.readFile(path.join(fixture, 'build/.well-known/agent-skills/index.json'), 'utf8'));
  assert.equal(skills.$schema, 'https://schemas.agentskills.io/discovery/0.2.0/schema.json');
  assert.equal(skills.skills.length, 4);
  for (const skill of skills.skills) {
    const content = await fs.readFile(path.join(fixture, 'build', skill.url));
    assert.equal(skill.digest, 'sha256:' + createHash('sha256').update(content).digest('hex'));
  }
  const ard = JSON.parse(await fs.readFile(path.join(fixture, 'build/.well-known/ard.json'), 'utf8'));
  assert.equal(ard.entries.length, 6);
  for (const entry of ard.entries) {
    const resource = await fs.readFile(path.join(fixture, 'build', new URL(entry.url).pathname));
    assert.ok(resource.length > 0);
    assert.ok(entry.representativeQueries.length >= 2);
  }
});

test('OpenAPI response schemas describe actual list, search, read, status, and error payloads', async () => {
  const {Validator} = await import('@cfworker/json-schema');
  const spec = JSON.parse(await fs.readFile(path.join(fixture, 'build/openapi.json'), 'utf8'));
  for (const route of ['/api/docs', '/api/docs/search?query=composition', '/api/docs/read?path=/validation/', '/api/docs/status', '/api/docs?version=missing']) {
    const response = await request(route);
    const endpoint = new URL('https://example.test' + route).pathname;
    const schema = spec.paths[endpoint].get.responses[String(response.status)].content['application/json'].schema;
    const result = new Validator({...schema, components: spec.components}).validate(await response.json());
    assert.equal(result.valid, true, JSON.stringify(result.errors));
  }
});

test('deployment artifact validation rejects lost hidden discovery files and modified skills', async () => {
  const {validateAgentArtifacts} = await load('scripts/check-agent-artifacts.mjs');
  const outDir = path.join(fixture, 'build');
  assert.equal(validateAgentArtifacts(outDir).skills, 4);
  const stripped = path.join(fixture, 'stripped-artifact');
  await fs.cp(outDir, stripped, {recursive: true, filter: (source) => path.basename(source) !== '.well-known'});
  assert.throws(() => validateAgentArtifacts(stripped), /ENOENT/);
  const skillPath = path.join(outDir, '.well-known/agent-skills/valiant-validation/SKILL.md');
  const content = await fs.readFile(skillPath);
  try {
    await fs.appendFile(skillPath, '\nModified after hashing.\n');
    assert.throws(() => validateAgentArtifacts(outDir), /Incorrect skill digest/);
  } finally {
    await fs.writeFile(skillPath, content);
  }
});
