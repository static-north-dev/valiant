const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {spawnSync} = require('node:child_process');
const test = require('node:test');

const repositoryRoot = path.resolve(__dirname, '..', '..');
const scriptPath = path.join(
  repositoryRoot,
  '.github',
  'scripts',
  'ensure-cloudflare-pages-project.sh',
);

function projectResponse(overrides = {}) {
  return {
    success: true,
    errors: [],
    messages: [],
    result: {
      name: 'valiant-docs',
      production_branch: 'main',
      ...overrides,
    },
  };
}

function runHelper(responses, environment = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'valiant-cloudflare-test-'));
  const fakeCurl = path.join(root, 'fake-curl.js');
  const stateFile = path.join(root, 'state');
  const callsFile = path.join(root, 'calls.jsonl');

  fs.writeFileSync(
    fakeCurl,
    `#!/usr/bin/env node
const fs = require('node:fs');

const responses = JSON.parse(process.env.MOCK_CURL_RESPONSES);
const stateFile = process.env.MOCK_CURL_STATE;
const callsFile = process.env.MOCK_CURL_CALLS;
const args = process.argv.slice(2);
let method = 'GET';
let outputPath;
let body = '';
let url = '';

for (let index = 0; index < args.length; index += 1) {
  const argument = args[index];
  if (argument === '--request') {
    method = args[++index];
  } else if (argument === '--output') {
    outputPath = args[++index];
  } else if (argument === '--write-out' || argument === '--header') {
    index += 1;
  } else if (argument === '--data') {
    body = args[++index];
  } else if (argument.startsWith('http://') || argument.startsWith('https://')) {
    url = argument;
  }
}

const callIndex = fs.existsSync(stateFile)
  ? Number.parseInt(fs.readFileSync(stateFile, 'utf8'), 10)
  : 0;
fs.writeFileSync(stateFile, String(callIndex + 1));
fs.appendFileSync(callsFile, JSON.stringify({method, url, body}) + '\\n');

const response = responses[callIndex];
if (!response) {
  console.error('Fake curl received an unexpected request.');
  process.exit(90);
}

if (response.exitCode) {
  console.error(response.stderr || 'Simulated curl transport failure.');
  process.exit(response.exitCode);
}

fs.writeFileSync(outputPath, response.body);
process.stdout.write(String(response.status));
`,
    {mode: 0o755},
  );

  const result = spawnSync('bash', [scriptPath], {
    cwd: repositoryRoot,
    encoding: 'utf8',
    env: {
      ...process.env,
      CLOUDFLARE_ACCOUNT_ID: 'account-id',
      CLOUDFLARE_API_TOKEN: 'super-secret-token',
      CLOUDFLARE_PAGES_PROJECT: 'valiant-docs',
      CLOUDFLARE_PAGES_PRODUCTION_BRANCH: 'main',
      CLOUDFLARE_API_ORIGIN: 'https://cloudflare.test/client/v4',
      CURL_BIN: fakeCurl,
      MOCK_CURL_RESPONSES: JSON.stringify(responses),
      MOCK_CURL_STATE: stateFile,
      MOCK_CURL_CALLS: callsFile,
      ...environment,
    },
  });

  const calls = fs.existsSync(callsFile)
    ? fs
        .readFileSync(callsFile, 'utf8')
        .trim()
        .split('\n')
        .filter(Boolean)
        .map((line) => JSON.parse(line))
    : [];

  fs.rmSync(root, {recursive: true, force: true});
  return {...result, calls};
}

test('accepts an existing Pages project with the expected production branch', () => {
  const result = runHelper([
    {status: 200, body: JSON.stringify(projectResponse())},
  ]);

  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /project "valiant-docs" is ready/);
  assert.deepEqual(result.calls.map(({method}) => method), ['GET']);
});

test('creates a missing Pages project', () => {
  const result = runHelper([
    {status: 404, body: JSON.stringify({success: false, errors: []})},
    {status: 200, body: JSON.stringify(projectResponse())},
  ]);

  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /Created Cloudflare Pages project "valiant-docs"/);
  assert.deepEqual(result.calls.map(({method}) => method), ['GET', 'POST']);
  assert.deepEqual(JSON.parse(result.calls[1].body), {
    name: 'valiant-docs',
    production_branch: 'main',
  });
});

test('recovers when another request creates the project first', () => {
  const result = runHelper([
    {status: 404, body: JSON.stringify({success: false, errors: []})},
    {status: 409, body: JSON.stringify({success: false, errors: []})},
    {status: 200, body: JSON.stringify(projectResponse())},
  ]);

  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /creation raced with another request/);
  assert.deepEqual(result.calls.map(({method}) => method), ['GET', 'POST', 'GET']);
});

test('rejects unsuccessful and malformed success responses', () => {
  for (const body of [
    JSON.stringify({success: false, errors: [{code: 1000, message: 'Denied'}]}),
    '{not-json',
  ]) {
    const result = runHelper([{status: 200, body}]);

    assert.notEqual(result.status, 0);
  }
});

test('reports account-scoping guidance without exposing the token', () => {
  const result = runHelper([
    {
      status: 403,
      body: JSON.stringify({
        success: false,
        errors: [{code: 9109, message: 'Invalid access token'}],
      }),
    },
  ]);
  const output = `${result.stdout}\n${result.stderr}`;

  assert.notEqual(result.status, 0);
  assert.match(output, /account ID, not a zone ID/);
  assert.match(output, /not a domain\/zone/);
  assert.doesNotMatch(output, /super-secret-token/);
});

test('fails before curl when a required secret is missing', () => {
  const result = runHelper([], {CLOUDFLARE_API_TOKEN: ''});

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /CLOUDFLARE_API_TOKEN repository secret is required/);
  assert.deepEqual(result.calls, []);
});

test('reports curl transport failures without exposing the token', () => {
  const result = runHelper([
    {exitCode: 7, stderr: 'Simulated connection failure.'},
  ]);
  const output = `${result.stdout}\n${result.stderr}`;

  assert.equal(result.status, 7);
  assert.match(output, /curl exit 7/);
  assert.doesNotMatch(output, /super-secret-token/);
});
