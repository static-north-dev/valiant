import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

export async function verifyAgentHttp(baseUrl, {expectedRevision} = {}) {
  const get = async (route, options = {}) => {
    const response = await fetch(new URL(route, baseUrl), {
      ...options,
      headers: {'User-Agent': 'ValiantDocsVerification/1.0', 'Cache-Control': 'no-cache', ...options.headers},
      signal: AbortSignal.timeout(15000),
    });
    assert.equal(response.status, 200, `${route} returned ${response.status}`);
    return response;
  };
  const json = async (route) => {
    const response = await get(route);
    assert.ok(response.headers.get('Content-Type')?.includes('json'), `${route} did not return JSON`);
    return response.json();
  };
  const html = await get('/', {headers: {Accept: 'text/html'}});
  assert.ok(html.headers.get('Content-Type')?.includes('text/html'));
  assert.ok(html.headers.get('Link')?.includes('/llms.txt'));
  assert.ok(html.headers.get('Vary')?.includes('Accept'));
  const markdown = await get('/', {headers: {Accept: 'text/markdown'}});
  assert.ok(markdown.headers.get('Content-Type')?.includes('text/markdown'));
  assert.ok((await markdown.text()).startsWith('# Valiant'));
  assert.ok(markdown.headers.get('Vary')?.includes('Accept'));
  const robots = await (await get('/robots.txt')).text();
  assert.ok(robots.includes('Content-Signal: search=yes, ai-input=yes'));
  const index = await json('/agents/documents.json');
  if (expectedRevision) {
    assert.equal(index.sourceRevision, expectedRevision, 'The deployed index belongs to a different source revision.');
  }
  const docs = await json('/api/docs');
  assert.equal(docs.defaultVersion, index.defaultVersion);
  assert.ok(docs.documents.length > 0 && docs.documents.every((document) => document.version === index.defaultVersion));
  const page = docs.documents.find((document) => document.id === 'validation/quick-start');
  assert.ok(page);
  const pageMarkdown = await get(page.path, {headers: {Accept: 'text/markdown'}});
  assert.ok(pageMarkdown.headers.get('Content-Type')?.includes('text/markdown'));
  assert.ok((await pageMarkdown.text()).includes('```csharp'));
  const head = await get(page.path, {method: 'HEAD', headers: {Accept: 'text/markdown'}});
  assert.equal(await head.text(), '');
  const skills = await json('/.well-known/agent-skills/index.json');
  assert.equal(skills.skills.length, 4);
  for (const skill of skills.skills) {
    const bytes = new Uint8Array(await (await get(skill.url)).arrayBuffer());
    assert.equal(skill.digest, `sha256:${createHash('sha256').update(bytes).digest('hex')}`);
  }
  const catalog = await json('/.well-known/ard.json');
  assert.ok(catalog.entries.length >= 4);
  await json('/.well-known/api-catalog');
  await json('/.well-known/mcp/server-card.json');
  for (const route of ['/mcp/server-card', '/.well-known/mcp/server-card.json']) {
    const card = await get(route);
    assert.ok(card.headers.get('Content-Type')?.includes('application/mcp-server-card+json'));
    assert.ok(card.headers.get('Access-Control-Expose-Headers')?.includes('ETag'));
    const preflight = await fetch(new URL(route, baseUrl), {method: 'OPTIONS', headers: {'User-Agent': 'ValiantDocsVerification/1.0', Origin: 'https://agent-client.example', 'Access-Control-Request-Method': 'GET', 'Access-Control-Request-Headers': 'If-None-Match'}, signal: AbortSignal.timeout(15000)});
    assert.equal(preflight.status, 204);
    assert.ok(preflight.headers.get('Access-Control-Allow-Headers')?.includes('If-None-Match'));
  }
  const rpc = async (id, method, params, protocolVersion) => {
    const response = await get('/mcp', {
      method: 'POST',
      headers: {'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', ...(protocolVersion ? {'MCP-Protocol-Version': protocolVersion} : {})},
      body: JSON.stringify({jsonrpc: '2.0', id, method, params}),
    });
    const message = await response.json();
    assert.equal(message.error, undefined, `MCP ${method} failed`);
    return message.result;
  };
  const initialization = await rpc(1, 'initialize', {protocolVersion: '2025-11-25', capabilities: {}, clientInfo: {name: 'valiant-deployment-verification', version: '1.0.0'}});
  const initialized = await fetch(new URL('/mcp', baseUrl), {
    method: 'POST',
    headers: {'User-Agent': 'ValiantDocsVerification/1.0', 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', 'MCP-Protocol-Version': initialization.protocolVersion},
    body: JSON.stringify({jsonrpc: '2.0', method: 'notifications/initialized'}),
    signal: AbortSignal.timeout(15000),
  });
  assert.equal(initialized.status, 202);
  const tools = await rpc(2, 'tools/list', {}, initialization.protocolVersion);
  assert.equal(tools.tools.length, 3);
  const read = await rpc(3, 'tools/call', {name: 'read_document', arguments: {path: page.path}}, initialization.protocolVersion);
  assert.ok(!read.isError);
  assert.equal(JSON.parse(read.content[0].text).version, index.defaultVersion);
  return {version: index.defaultVersion, documents: docs.documents.length, skills: skills.skills.length, mcpTools: tools.tools.length};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const baseUrl = process.argv[2] ?? 'https://valiant.staticnorth.dev';
  const expectedRevision = process.env.DOCS_EXPECTED_REVISION;
  let result;
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      result = await verifyAgentHttp(baseUrl, {expectedRevision});
      break;
    } catch (error) {
      if (attempt === 4) {
        throw error;
      }
      console.log(`Agent HTTP verification is waiting for the deployment (${error.message}).`);
      await new Promise((resolve) => setTimeout(resolve, 2000));
    }
  }
  console.log(`Verified agent HTTP access at ${baseUrl}: ${JSON.stringify(result)}`);
}
