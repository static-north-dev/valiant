import fs from 'node:fs/promises';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {build} from 'esbuild';
import {contentSignal, discoveryLinks, moduleNames} from '../src/agents/documentation.mjs';
import {serverInfo} from '../src/agents/mcp.mjs';

const require = createRequire(import.meta.url);
const {DEFAULT_PARSE_FRONT_MATTER} = require('@docusaurus/utils');

function revision(siteDir) {
  try {
    return execFileSync('git', ['rev-parse', 'HEAD'], {cwd: siteDir, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore']}).trim();
  } catch {
    return null;
  }
}

function withoutFrontmatter(markdown) {
  return markdown.replace(/^---\r?\n[\s\S]*?\r?\n---(?:\r?\n|$)/, '').trim();
}

export function resolveMarkdownLinks(markdown, source, documentsBySource, siteUrl) {
  let fence;
  return markdown.split(/\r?\n/).map((line) => {
    const marker = line.match(/^\s{0,3}(`{3,}|~{3,})/);
    if (marker) {
      if (!fence) {
        fence = marker[1];
      } else if (marker[1][0] === fence[0] && marker[1].length >= fence.length) {
        fence = undefined;
      }
      return line;
    }
    if (fence) {
      return line;
    }
    return line.replace(/(\]\(|^\s*\[[^\]]+\]:\s*)([^\s)]+\.md(?:#[^\s)]*)?)/g, (match, prefix, target) => {
      if (/^[a-z][a-z\d+.-]*:/i.test(target)) {
        return match;
      }
      const [file, fragment] = target.split('#');
      const linked = documentsBySource.get(path.resolve(path.dirname(source), file));
      if (!linked) {
        throw new Error(`Markdown link '${target}' in '${source}' does not reference a published document.`);
      }
      return `${prefix}${siteUrl}${linked.permalink}${fragment ? `#${fragment}` : ''}`;
    });
  }).join('\n');
}

function frontmatter(document, sourceRevision) {
  return ['---', ...Object.entries({
    title: document.title,
    description: document.description,
    version: document.version,
    canonical: document.url,
    source_revision: sourceRevision,
  }).filter(([, value]) => value !== null).map(([key, value]) => `${key}: ${JSON.stringify(value)}`), '---', ''].join('\n');
}

function documentOrder(left, right) {
  const rank = (document) => document.id.endsWith('/index') ? 0 : document.id.endsWith('/quick-start') ? 1 : 2;
  return moduleNames.indexOf(left.module) - moduleNames.indexOf(right.module) || rank(left) - rank(right) || left.id.localeCompare(right.id);
}

function contextBundle(documents, version, sourceRevision, title) {
  return [
    `# ${title}`,
    '',
    `Documentation version: ${version}. This context contains only this version.`,
    ...(sourceRevision ? [`Source revision: ${sourceRevision}.`] : []),
    '',
    ...documents.flatMap((document) => [`## ${document.title}`, '', `Source: ${document.url}`, '', withoutFrontmatter(document.content), '']),
  ].join('\n');
}

function openApi(siteUrl) {
  const filterParameters = [
    {name: 'module', in: 'query', schema: {type: 'string', enum: moduleNames}},
    {name: 'version', in: 'query', description: 'Defaults to the latest published major.', schema: {type: 'string'}},
  ];
  const response = (description, schema) => ({description, content: {'application/json': {schema: {$ref: `#/components/schemas/${schema}`}}}});
  const get = (operationId, summary, schema, parameters = []) => ({get: {
    operationId, summary, parameters,
    responses: {'200': response('Successful result.', schema), '400': response('Invalid arguments.', 'Error'), '404': response('Page or endpoint not found.', 'Error'), '503': response('Documentation index unavailable.', 'Error')},
  }});
  return {
    openapi: '3.1.0',
    info: {title: 'Valiant Documentation API', version: serverInfo.version, description: 'Public, read-only access to versioned Valiant documentation. No authentication or account is required.'},
    servers: [{url: siteUrl}],
    security: [],
    paths: {
      '/api/docs': get('listDocuments', 'List documentation pages and available versions.', 'DocumentList', filterParameters),
      '/api/docs/search': get('searchDocuments', 'Search documentation with bounded excerpts.', 'SearchResults', [
        {name: 'query', in: 'query', required: true, schema: {type: 'string', minLength: 1, maxLength: 200}},
        ...filterParameters,
        {name: 'limit', in: 'query', schema: {type: 'integer', minimum: 1, maximum: 20, default: 8}},
      ]),
      '/api/docs/read': get('readDocument', 'Read the Markdown for an exact published page path.', 'Document', [
        {name: 'path', in: 'query', required: true, schema: {type: 'string', minLength: 1, maxLength: 300}},
      ]),
      '/api/docs/status': get('documentationStatus', 'Check that the documentation index is available.', 'Status'),
    },
    components: {schemas: {
      Version: {
        type: 'object', required: ['name', 'label', 'path', 'isDefault'],
        properties: {name: {type: 'string'}, label: {type: 'string'}, path: {type: 'string'}, isDefault: {type: 'boolean'}},
      },
      DocumentMetadata: {
        type: 'object', required: ['id', 'title', 'description', 'module', 'version', 'path', 'url', 'markdownUrl'],
        properties: {
          id: {type: 'string'}, title: {type: 'string'}, description: {type: 'string'},
          module: {type: 'string', enum: moduleNames}, version: {type: 'string'},
          path: {type: 'string', description: 'Exact published page path accepted by read_document.', examples: ['/validation/quick-start']},
          url: {type: 'string', format: 'uri'}, markdownUrl: {type: 'string'},
        },
      },
      Document: {allOf: [{$ref: '#/components/schemas/DocumentMetadata'}, {type: 'object', required: ['content'], properties: {content: {type: 'string', description: 'Complete Markdown with metadata and preserved C# code fences.'}}}]},
      DocumentList: {
        type: 'object', required: ['defaultVersion', 'versions', 'documents'],
        properties: {
          defaultVersion: {type: 'string'},
          versions: {type: 'array', items: {$ref: '#/components/schemas/Version'}},
          documents: {type: 'array', items: {$ref: '#/components/schemas/DocumentMetadata'}},
        },
      },
      SearchResults: {
        type: 'object', required: ['total', 'results'],
        properties: {
          total: {type: 'integer', minimum: 0, description: 'Total matches before the result limit.'},
          results: {type: 'array', maxItems: 20, items: {allOf: [{$ref: '#/components/schemas/DocumentMetadata'}, {type: 'object', required: ['score', 'excerpt'], properties: {score: {type: 'number'}, excerpt: {type: 'string', maxLength: 500}}}]}},
        },
      },
      Status: {type: 'object', required: ['status', 'defaultVersion', 'sourceRevision'], properties: {status: {type: 'string', enum: ['ok']}, defaultVersion: {type: 'string'}, sourceRevision: {type: ['string', 'null']}}},
      Error: {type: 'object', required: ['error'], properties: {error: {type: 'string'}}},
    }},
  };
}

async function bundleNotices(inputs, workingDirectory) {
  const roots = new Set();
  for (const input of inputs) {
    const parts = path.resolve(workingDirectory, input).split(path.sep);
    const index = parts.lastIndexOf('node_modules');
    if (index >= 0) {
      roots.add(parts.slice(0, index + (parts[index + 1].startsWith('@') ? 3 : 2)).join(path.sep));
    }
  }
  const notices = [];
  for (const root of [...roots].sort()) {
    const pkg = JSON.parse(await fs.readFile(path.join(root, 'package.json'), 'utf8'));
    const licenses = (await fs.readdir(root)).filter((file) => /^licen[sc]e(?:\..*)?$|^copying$/i.test(file));
    notices.push(`${pkg.name}@${pkg.version} (${pkg.license ?? 'see package license'})`, ...await Promise.all(licenses.map((file) => fs.readFile(path.join(root, file), 'utf8'))));
  }
  return notices.join('\n\n');
}

export async function buildAgentArtifacts({siteDir, outDir, versions, siteUrl, sourceRevision = revision(siteDir)}) {
  const write = async (url, content) => {
    const file = path.join(outDir, url);
    await fs.mkdir(path.dirname(file), {recursive: true});
    await fs.writeFile(file, typeof content === 'string' ? content : `${JSON.stringify(content, null, 2)}\n`);
  };
  const defaultVersion = versions.find((version) => version.isLast)?.versionName;
  if (!defaultVersion) {
    throw new Error('The docs plugin did not identify a default documentation version.');
  }
  const documents = [];
  for (const version of versions) {
    const published = version.docs.filter((document) => !document.draft && !document.unlisted);
    const sources = new Map(published.map((document) => [path.resolve(siteDir, document.source.replace(/^@site\//, '')), document]));
    for (const [source, document] of sources) {
      const module = document.id.split('/')[0];
      if (!moduleNames.includes(module)) {
        throw new Error(`Unknown documentation module in '${document.id}'.`);
      }
      const markdownUrl = `/markdown${document.permalink.replace(/\/$/, '')}.md`;
      const entry = {
        id: document.id,
        title: document.title,
        description: document.description,
        module,
        version: version.versionName,
        path: document.permalink,
        url: `${siteUrl}${document.permalink}`,
        markdownUrl,
      };
      const markdown = resolveMarkdownLinks(withoutFrontmatter(await fs.readFile(source, 'utf8')), source, sources, siteUrl);
      entry.content = `${frontmatter(entry, sourceRevision)}\n${markdown}\n`;
      await write(markdownUrl, entry.content);
      documents.push(entry);
    }
  }
  documents.sort((left, right) => left.version.localeCompare(right.version) || documentOrder(left, right));
  const index = {
    defaultVersion,
    sourceRevision,
    versions: versions.map((version) => ({name: version.versionName, label: version.label, path: version.path, isDefault: version.isLast})),
    documents,
  };
  await write('/agents/documents.json', index);
  const defaults = documents.filter((document) => document.version === defaultVersion).sort(documentOrder);
  await write('/llms-full.txt', contextBundle(defaults, defaultVersion, sourceRevision, 'StaticNorth.Valiant — Full Documentation'));
  const moduleLinks = [];
  for (const module of moduleNames) {
    const moduleDocuments = defaults.filter((document) => document.module === module);
    const bundleUrl = `/llms-${module}.txt`;
    await write(bundleUrl, contextBundle(moduleDocuments, defaultVersion, sourceRevision, `Valiant ${module}`));
    moduleLinks.push(`- [${module}](${siteUrl}${bundleUrl}): Markdown context for documentation version ${defaultVersion}.`);
  }
  const curated = await fs.readFile(path.join(siteDir, 'static/llms.txt'), 'utf8');
  await write('/llms.txt', `${curated.trim()}\n\n## Published documentation version\n\nDefault version: ${defaultVersion}. Read ${siteUrl}/agents/documents.json for all available versions and exact page paths.\n\n## Module Markdown contexts\n\n${moduleLinks.join('\n')}\n`);
  const skills = [];
  for (const module of moduleNames) {
    const name = `valiant-${module}`;
    const content = await fs.readFile(path.join(siteDir, 'agent-skills', name, 'SKILL.md'), 'utf8');
    const {frontMatter} = await DEFAULT_PARSE_FRONT_MATTER({fileContent: content});
    if (frontMatter.name !== name || typeof frontMatter.description !== 'string') {
      throw new Error(`Invalid skill frontmatter in '${name}'.`);
    }
    const url = `/.well-known/agent-skills/${name}/SKILL.md`;
    await write(url, content);
    skills.push({name, type: 'skill-md', description: frontMatter.description, url, digest: `sha256:${createHash('sha256').update(content).digest('hex')}`});
  }
  await write('/.well-known/agent-skills/index.json', {$schema: 'https://schemas.agentskills.io/discovery/0.2.0/schema.json', skills});
  const card = {
    $schema: 'https://static.modelcontextprotocol.io/schemas/v1/server-card.schema.json',
    ...serverInfo,
    title: 'Valiant documentation',
    description: 'Read and search versioned Valiant documentation and C# examples.',
    websiteUrl: `${siteUrl}/agents`,
    remotes: [{type: 'streamable-http', url: `${siteUrl}/mcp`, supportedProtocolVersions: ['2025-11-25', '2025-06-18', '2025-03-26']}],
  };
  await write('/mcp/server-card', card);
  await write('/.well-known/mcp/server-card.json', card);
  await write('/openapi.json', openApi(siteUrl));
  await write('/.well-known/api-catalog', {linkset: [{
    anchor: `${siteUrl}/api/docs`,
    'service-desc': [{href: `${siteUrl}/openapi.json`, type: 'application/openapi+json'}],
    'service-doc': [{href: `${siteUrl}/agents`, type: 'text/html'}],
    status: [{href: `${siteUrl}/api/docs/status`, type: 'application/json'}],
  }]});
  const entries = skills.map((skill) => ({
    identifier: `urn:air:${new URL(siteUrl).hostname}:skills:${skill.name}`,
    displayName: skill.name,
    type: 'application/agent-skills+md',
    url: `${siteUrl}${skill.url}`,
    description: skill.description,
    representativeQueries: [`How do I use Valiant ${skill.name.replace('valiant-', '')} in a .NET application?`, `Generate correct C# using ${skill.name}.`],
  }));
  entries.push({identifier: `urn:air:${new URL(siteUrl).hostname}:mcp:documentation`, displayName: 'Valiant documentation MCP', type: 'application/mcp-server-card+json', url: `${siteUrl}/mcp/server-card`, representativeQueries: ['Search Valiant documentation for validator composition.', 'Read the Valiant settings quick start.']});
  entries.push({identifier: `urn:air:${new URL(siteUrl).hostname}:api:documentation`, displayName: 'Valiant documentation API', type: 'application/openapi+json', url: `${siteUrl}/openapi.json`, representativeQueries: ['List Valiant documentation for a published major.', 'Find the mapping guide for immutable models.']});
  const catalog = {specVersion: '1.0', host: {name: 'Valiant', url: siteUrl}, entries};
  await write('/.well-known/ard.json', catalog);
  await write('/.well-known/ai-catalog.json', catalog);
  await write('/.well-known/poppy.json', {protocol_version: '0.3', organization: {name: 'Static North — Valiant', domain: new URL(siteUrl).hostname}, web: {}});
  const home = ['# Valiant', '', 'Source-generated .NET building blocks for validation, endpoints, mapping, and settings.', '', `Documentation version: ${defaultVersion}.`, '', ...defaults.filter((document) => document.id.endsWith('/index')).map((document) => `- [${document.title}](${document.url}): ${document.description}`), '', `Agent access: ${siteUrl}/agents`, `Documentation index: ${siteUrl}/llms.txt`, ''].join('\n');
  await write('/markdown/index.md', home);
  await write('/markdown/agents.md', await fs.readFile(path.join(siteDir, 'agent-access.md'), 'utf8'));
  const markdownRoutes = Object.fromEntries([['/', '/markdown/index.md'], ['/agents', '/markdown/agents.md'], ...documents.map((document) => [document.path.replace(/\/$/, ''), document.markdownUrl])]);
  const headers = [
    '/*', `  Link: ${discoveryLinks}`, `  Content-Signal: ${contentSignal}`, '  X-Content-Type-Options: nosniff',
    '/markdown/*', '  Content-Type: text/markdown; charset=utf-8', '  Access-Control-Allow-Origin: *',
    '/.well-known/*', '  Access-Control-Allow-Origin: *',
    '/.well-known/api-catalog', '  Content-Type: application/linkset+json',
    ...['/mcp/server-card', '/.well-known/mcp/server-card.json'].flatMap((url) => [
      url,
      '  Content-Type: application/mcp-server-card+json',
      ...(url === '/mcp/server-card' ? ['  Access-Control-Allow-Origin: *'] : []),
      '  Access-Control-Allow-Methods: GET, HEAD, OPTIONS',
      '  Access-Control-Allow-Headers: Content-Type, If-None-Match',
      '  Access-Control-Expose-Headers: ETag',
    ]),
    '/agents/documents.json', '  Access-Control-Allow-Origin: *',
    '/openapi.json', '  Content-Type: application/openapi+json', '  Access-Control-Allow-Origin: *',
    ...skills.flatMap((skill) => [skill.url, '  Content-Type: text/markdown; charset=utf-8']),
    '',
  ].join('\n');
  await write('/_headers', headers);
  await write('/_routes.json', {version: 1, include: ['/*'], exclude: [
    '/assets/*', '/img/*', '/markdown/*', '/llms*.txt', '/agents/documents.json',
    '/openapi.json', '/robots.txt', '/sitemap.xml',
    '/.well-known/agent-skills/*', '/.well-known/api-catalog',
    '/.well-known/ard.json', '/.well-known/ai-catalog.json', '/.well-known/poppy.json',
  ]});
  const bundle = await build({
    absWorkingDir: siteDir,
    stdin: {
      contents: `import {createWorker} from ${JSON.stringify(fileURLToPath(new URL('../src/agents/worker.mjs', import.meta.url)))};\nexport default createWorker(${JSON.stringify({markdownRoutes})});`,
      resolveDir: siteDir,
      sourcefile: 'valiant-pages-worker.mjs',
    },
    bundle: true,
    format: 'esm',
    platform: 'browser',
    target: 'es2022',
    outfile: path.join(outDir, '_worker.js'),
    minify: true,
    legalComments: 'linked',
    metafile: true,
  });
  const legalPath = path.join(outDir, '_worker.js.LEGAL.txt');
  const comments = await fs.readFile(legalPath, 'utf8').catch(() => '');
  await write('/_worker.js.LEGAL.txt', `${comments}\n\n${await bundleNotices(Object.keys(bundle.metafile.inputs), siteDir)}\n`);
  return {index, skills, markdownRoutes};
}
