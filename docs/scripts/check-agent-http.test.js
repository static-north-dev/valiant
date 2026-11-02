const assert = require('node:assert/strict');
const test = require('node:test');

test('deployment verification reports Cloudflare denial details and stops immediately', async (t) => {
  const {verifyAgentDeployment} = await import('./check-agent-http.mjs');
  const requests = [];
  t.mock.method(globalThis, 'fetch', async (url, options) => {
    requests.push({url: String(url), options});
    return new Response('<title>Access\n denied</title><h1><span>Error</span><span>1010</span></h1><p>private response detail</p>', {
      status: 403,
      headers: {server: 'cloudflare', 'content-type': 'text/html', 'cf-ray': 'test-ray-RIX', 'cf-mitigated': 'challenge'},
    });
  });
  await assert.rejects(verifyAgentDeployment('https://valiant.staticnorth.dev'), (error) => {
    assert.match(error.message, /GET https:\/\/valiant\.staticnorth\.dev\/ returned 403; expected 200/);
    for (const detail of ['cf-ray=test-ray-RIX', 'cf-mitigated=challenge', 'edge-error=1010', 'page="Access denied"', 'Access denied:']) {
      assert.ok(error.message.includes(detail), error.message);
    }
    assert.ok(!error.message.includes('private response detail'));
    assert.equal(error.retryable, false);
    return true;
  });
  assert.equal(requests.length, 1);
  assert.equal(requests[0].options.headers['User-Agent'], 'ValiantDocsVerification/1.0');
});

test('plain authorization denials remain failures without claiming a Cloudflare error', async (t) => {
  const {verifyAgentDeployment} = await import('./check-agent-http.mjs');
  let requests = 0;
  t.mock.method(globalThis, 'fetch', async () => {
    requests++;
    return new Response('Unauthorized', {status: 401});
  });
  await assert.rejects(verifyAgentDeployment('https://valiant.staticnorth.dev'), (error) => {
    assert.match(error.message, /returned 401/);
    assert.ok(!error.message.includes('edge-error='));
    return true;
  });
  assert.equal(requests, 1);
});

test('temporary deployment failures still retry before an access denial stops the check', async (t) => {
  const {verifyAgentDeployment} = await import('./check-agent-http.mjs');
  let requests = 0;
  const messages = [];
  t.mock.method(console, 'log', (message) => messages.push(message));
  t.mock.method(globalThis, 'fetch', async () => new Response('', {status: ++requests === 1 ? 503 : 403}));
  await assert.rejects(verifyAgentDeployment('https://valiant.staticnorth.dev'), /returned 403/);
  assert.equal(requests, 2);
  assert.equal(messages.length, 1);
  assert.match(messages[0], /waiting for the deployment.*returned 503/);
});
