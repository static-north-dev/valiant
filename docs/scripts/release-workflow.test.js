const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const repositoryRoot = path.resolve(__dirname, '..', '..');
const workflowsRoot = path.resolve(
  repositoryRoot,
  '.github',
  'workflows',
);
const releaseWorkflow = fs.readFileSync(
  path.join(workflowsRoot, 'release.yml'),
  'utf8',
);
const docsWorkflow = fs.readFileSync(
  path.join(workflowsRoot, 'publish-docs.yml'),
  'utf8',
);
const ciWorkflow = fs.readFileSync(
  path.join(workflowsRoot, 'ci.yml'),
  'utf8',
);
const releaseSolutionFilter = JSON.parse(
  fs.readFileSync(path.join(repositoryRoot, 'StaticNorth.Valiant.Release.slnf'), 'utf8'),
);
const solution = fs.readFileSync(
  path.join(repositoryRoot, 'StaticNorth.Valiant.sln'),
  'utf8',
);

function job(workflow, name) {
  const marker = `\n  ${name}:\n`;
  const start = workflow.indexOf(marker);
  assert.notEqual(start, -1, `Missing ${name} job.`);
  const remainder = workflow.slice(start + marker.length);
  const next = remainder.search(/\n  [a-z0-9-]+:\n/);
  const end = next === -1 ? workflow.length : start + marker.length + next;
  return workflow.slice(start, end);
}

function jobNames(workflow) {
  const jobs = workflow.slice(workflow.indexOf('\njobs:\n') + '\njobs:\n'.length);
  return [...jobs.matchAll(/^  ([a-z0-9-]+):$/gm)].map((match) => match[1]);
}

test('CI validates pushes and pull requests on main', () => {
  assert.match(
    ciWorkflow,
    /^on:\n  push:\n    branches:\n      - main\n  pull_request:\n    branches:\n      - main$/m,
  );
  assert.doesNotMatch(ciWorkflow, /branches:\n\s+- develop/);
});

test('published releases only verify and publish NuGet packages', () => {
  assert.match(
    releaseWorkflow,
    /^on:\n  release:\n    types:\n      - published$/m,
  );
  assert.deepEqual(jobNames(releaseWorkflow), ['prepare', 'publish-nuget']);
  assert.match(job(releaseWorkflow, 'publish-nuget'), /needs: prepare/);
  assert.match(job(releaseWorkflow, 'publish-nuget'), /id-token: write/);
  assert.doesNotMatch(
    releaseWorkflow,
    /docs_major|docs:snapshot|docs:import|DOCS_INCLUDE_CURRENT|CLOUDFLARE_|wrangler|setup-node/,
  );
});

test('release verification excludes the documentation project', () => {
  assert.match(
    releaseWorkflow,
    /dotnet restore StaticNorth\.Valiant\.Release\.slnf --locked-mode/,
  );
  assert.match(releaseWorkflow, /dotnet build StaticNorth\.Valiant\.Release\.slnf/);
  assert.match(
    releaseWorkflow,
    /dotnet test --solution StaticNorth\.Valiant\.Release\.slnf/,
  );
  assert.match(
    releaseWorkflow,
    /dotnet sln StaticNorth\.Valiant\.Release\.slnf list/,
  );
  assert.doesNotMatch(
    releaseWorkflow,
    /dotnet (?:restore|build|test|sln) StaticNorth\.Valiant\.sln/,
  );
  assert.doesNotMatch(releaseWorkflow, /StaticNorth\.Valiant\.sln(?!f)/);

  const solutionProjects = [...solution.matchAll(
    /^Project\([^)]*\) = .*?, "([^"]+\.csproj)"/gm,
  )]
    .map((match) => match[1].replaceAll('\\', '/'))
    .sort();
  const filteredProjects = [...releaseSolutionFilter.solution.projects].sort();

  assert.equal(releaseSolutionFilter.solution.path, 'StaticNorth.Valiant.sln');
  assert.deepEqual(filteredProjects, solutionProjects);
  assert.doesNotMatch(JSON.stringify(releaseSolutionFilter), /\.esproj/);
  assert.doesNotMatch(JSON.stringify(releaseSolutionFilter), /docs\//);
});

test('documentation publication is a separate manual workflow', () => {
  assert.match(
    docsWorkflow,
    /^on:\n  workflow_dispatch:\n\npermissions:\n  contents: read$/m,
  );
  assert.doesNotMatch(docsWorkflow, /^  (push|release):/m);
  assert.deepEqual(jobNames(docsWorkflow), [
    'prepare',
    'generate-docs-snapshot',
    'verify-docs-candidate',
    'cloudflare-preflight',
    'persist-docs-snapshot',
    'verify-docs',
    'deploy-docs',
  ]);
});

test('production publication jobs use the production environment', () => {
  assert.match(job(releaseWorkflow, 'publish-nuget'), /^    environment: production$/m);
  assert.match(job(docsWorkflow, 'deploy-docs'), /^    environment: production$/m);
});

test('documentation audits retain a narrow no-fix advisory gate', () => {
  const ciDocsJob = job(ciWorkflow, 'build-docs');
  assert.match(ciDocsJob, /timeout-minutes: 20/);
  assert.match(ciDocsJob, /npm run audit:dependencies/);
  assert.doesNotMatch(ciDocsJob, /npm audit/);

  for (const name of ['generate-docs-snapshot', 'verify-docs']) {
    assert.match(job(docsWorkflow, name), /npm run audit:dependencies/);
    assert.doesNotMatch(job(docsWorkflow, name), /npm audit/);
  }

  for (const name of ['verify-docs-candidate', 'verify-docs']) {
    assert.match(job(docsWorkflow, name), /timeout-minutes: 20/);
  }
});

test('documentation publication pins the tracked major and main source SHA', () => {
  const prepare = job(docsWorkflow, 'prepare');
  assert.match(prepare, /ref: main/);
  assert.match(prepare, /node docs\/scripts\/current-major\.js/);
  assert.match(prepare, /source_sha: \$\{\{ steps\.source\.outputs\.source_sha \}\}/);
  assert.doesNotMatch(docsWorkflow, /inputs\.major|github\.event\.inputs/);

  const sourceReference = /ref: \$\{\{ needs\.prepare\.outputs\.source_sha \}\}/;
  assert.match(job(docsWorkflow, 'generate-docs-snapshot'), sourceReference);
  assert.match(job(docsWorkflow, 'verify-docs-candidate'), sourceReference);
  assert.match(job(docsWorkflow, 'cloudflare-preflight'), sourceReference);
  assert.match(job(docsWorkflow, 'persist-docs-snapshot'), sourceReference);
});

test('documentation publication keeps credentials and permissions narrowly scoped', () => {
  assert.equal(docsWorkflow.match(/contents: write/g)?.length, 1);
  assert.match(job(docsWorkflow, 'persist-docs-snapshot'), /contents: write/);
  assert.equal(docsWorkflow.match(/deployments: write/g)?.length, 1);
  assert.match(job(docsWorkflow, 'deploy-docs'), /deployments: write/);

  for (const name of [
    'prepare',
    'generate-docs-snapshot',
    'verify-docs-candidate',
    'persist-docs-snapshot',
    'verify-docs',
  ]) {
    assert.doesNotMatch(
      job(docsWorkflow, name),
      /secrets\.CLOUDFLARE_(ACCOUNT_ID|API_TOKEN)/,
    );
  }

  for (const name of ['cloudflare-preflight', 'deploy-docs']) {
    assert.match(
      job(docsWorkflow, name),
      /secrets\.CLOUDFLARE_ACCOUNT_ID/,
    );
    assert.match(
      job(docsWorkflow, name),
      /secrets\.CLOUDFLARE_API_TOKEN/,
    );
  }
});

test('documentation persistence targets main and fails safely when it moves', () => {
  const persist = job(docsWorkflow, 'persist-docs-snapshot');
  assert.match(persist, /remote_head="\$\(git rev-parse origin\/main\)"/);
  assert.match(persist, /\[ "\$remote_head" != "\$DOCS_SOURCE_SHA" \]/);
  assert.match(persist, /Run the Publish Docs workflow again/);
  assert.match(persist, /current_major="\$\(node docs\/scripts\/current-major\.js\)"/);
  assert.match(persist, /docs_commit="\$DOCS_SOURCE_SHA"/);
  assert.match(persist, /git push origin HEAD:main/);
  assert.match(persist, /git rev-parse origin\/main/);
  assert.doesNotMatch(persist, /origin\/develop|HEAD:develop/);
  assert.match(
    persist,
    /git add \\\n\s+docs\/versions\.json \\\n\s+docs\/versioned_docs \\\n\s+docs\/versioned_sidebars/,
  );
});

test('the final docs artifact is built and deployed from the persisted SHA', () => {
  const persistedReference =
    /ref: \$\{\{ needs\.persist-docs-snapshot\.outputs\.docs_commit \}\}/;
  const verify = job(docsWorkflow, 'verify-docs');
  const deploy = job(docsWorkflow, 'deploy-docs');

  assert.match(verify, persistedReference);
  assert.match(verify, /npm run build/);
  assert.match(verify, /Upload verified documentation site/);
  assert.match(deploy, persistedReference);
  assert.match(deploy, /Download verified documentation site/);
  assert.doesNotMatch(deploy, /npm (ci|run build)/);
});

test('manual publication idempotently provisions the shared Cloudflare project', () => {
  const preflight = job(docsWorkflow, 'cloudflare-preflight');
  const deploy = job(docsWorkflow, 'deploy-docs');

  assert.match(docsWorkflow, /CLOUDFLARE_PAGES_PROJECT: valiant-docs/);
  assert.match(docsWorkflow, /CLOUDFLARE_PAGES_PRODUCTION_BRANCH: main/);
  assert.match(
    preflight,
    /bash \.github\/scripts\/ensure-cloudflare-pages-project\.sh/,
  );
  assert.match(
    deploy,
    /--project-name=\$\{\{ env\.CLOUDFLARE_PAGES_PROJECT \}\}/,
  );
  assert.match(
    deploy,
    /--branch=\$\{\{ env\.CLOUDFLARE_PAGES_PRODUCTION_BRANCH \}\}/,
  );
});

test('manual publication uses one shared documentation version artifact', () => {
  assert.doesNotMatch(
    docsWorkflow,
    /(endpoints|mapping|settings)_(versions|versioned_docs|versioned_sidebars)/,
  );
  assert.match(
    job(docsWorkflow, 'generate-docs-snapshot'),
    /versioned_docs\/version-\$DOCS_MAJOR/,
  );
  assert.match(
    job(docsWorkflow, 'generate-docs-snapshot'),
    /versioned_sidebars\/version-\$DOCS_MAJOR-sidebars\.json/,
  );
  assert.match(job(docsWorkflow, 'persist-docs-snapshot'), /docs\/versions\.json/);
  assert.match(docsWorkflow, /route="docs\/build\/\$major\/\$product\/index\.html"/);
  assert.doesNotMatch(docsWorkflow, /docs\/build\/\$product\/\$major\/index\.html/);
});
