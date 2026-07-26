import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const require = createRequire(import.meta.url);
const sidebars = require('../sidebars.js');

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const docsRoot = path.resolve(scriptDirectory, '..');
const contentRoot = path.join(docsRoot, 'content');
const outputPath = path.join(docsRoot, 'static', 'llms-full.txt');
const siteUrl = 'https://valiant.staticnorth.dev';
const sidebarSections = [
  ['Validation', 'validationSidebar'],
  ['Endpoints', 'endpointsSidebar'],
  ['Mapping', 'mappingSidebar'],
  ['Settings', 'settingsSidebar'],
];

function fail(message) {
  throw new Error(`Unable to generate llms-full.txt: ${message}`);
}

function walk(directory) {
  return fs.readdirSync(directory, {withFileTypes: true}).flatMap((entry) => {
    const entryPath = path.join(directory, entry.name);

    if (entry.isDirectory()) {
      return walk(entryPath);
    }

    return [entryPath];
  });
}

function collectDocumentIds(items, documentIds) {
  for (const item of items) {
    if (typeof item === 'string') {
      documentIds.push(item);
      continue;
    }

    if (item?.type === 'category' && Array.isArray(item.items)) {
      collectDocumentIds(item.items, documentIds);
      continue;
    }

    fail(`sidebar item '${JSON.stringify(item)}' is not a document or category.`);
  }
}

function allDocumentIds() {
  const markdownFiles = walk(contentRoot).filter((file) => file.endsWith('.md'));
  const mdxFiles = walk(contentRoot).filter((file) => file.endsWith('.mdx'));

  if (mdxFiles.length > 0) {
    fail(`MDX pages need explicit handling: ${mdxFiles.join(', ')}`);
  }

  return markdownFiles
    .map((file) => path.relative(contentRoot, file).replace(/\\/g, '/').replace(/\.md$/, ''))
    .sort();
}

function orderedDocuments() {
  const ordered = [];

  for (const [section, sidebarId] of sidebarSections) {
    const items = sidebars[sidebarId];
    if (!Array.isArray(items)) {
      fail(`sidebar '${sidebarId}' is missing.`);
    }

    const documentIds = [];
    collectDocumentIds(items, documentIds);
    ordered.push([section, documentIds]);
  }

  const orderedIds = ordered.flatMap(([, documentIds]) => documentIds);
  const duplicateIds = orderedIds.filter((id, index) => orderedIds.indexOf(id) !== index);
  if (duplicateIds.length > 0) {
    fail(`sidebar documents are duplicated: ${[...new Set(duplicateIds)].join(', ')}`);
  }

  const expectedIds = allDocumentIds();
  const missingIds = expectedIds.filter((id) => !orderedIds.includes(id));
  const unknownIds = orderedIds.filter((id) => !expectedIds.includes(id));
  if (missingIds.length > 0 || unknownIds.length > 0) {
    fail(
      `sidebar/content mismatch; missing: ${missingIds.join(', ') || 'none'}; ` +
        `unknown: ${unknownIds.join(', ') || 'none'}.`,
    );
  }

  return ordered;
}

function stripFrontmatter(markdown, documentId) {
  if (!markdown.startsWith('---\n') && !markdown.startsWith('---\r\n')) {
    return markdown;
  }

  const closingIndex = markdown.indexOf('\n---', 4);
  if (closingIndex === -1) {
    fail(`${documentId}.md has an unterminated frontmatter block.`);
  }

  const bodyStart = markdown.indexOf('\n', closingIndex + 1);
  if (bodyStart === -1) {
    return '';
  }

  return markdown.slice(bodyStart + 1);
}

function documentUrl(documentId) {
  const route = documentId.endsWith('/index')
    ? documentId.slice(0, -'/index'.length)
    : documentId;
  return `${siteUrl}/${route}${route ? '/' : ''}`;
}

function canonicalizeDocLinks(markdown, documentId) {
  return markdown.replace(/\]\(([^)\s]+)\)/g, (match, target) => {
    if (!target.endsWith('.md') && !target.includes('.md#')) {
      return match;
    }

    const [filePath, fragment] = target.split('#', 2);
    if (!filePath.endsWith('.md')) {
      return match;
    }

    const resolvedId = path.posix
      .normalize(path.posix.join(path.posix.dirname(documentId), filePath))
      .replace(/\.md$/, '');

    return `](${documentUrl(resolvedId)}${fragment ? `#${fragment}` : ''})`;
  });
}

function promoteHeadings(markdown) {
  let inFence = false;

  return markdown
    .split(/\r?\n/)
    .map((line) => {
      if (/^\s*(```|~~~)/.test(line)) {
        inFence = !inFence;
        return line;
      }

      return !inFence && /^#{1,4}\s/.test(line) ? `##${line}` : line;
    })
    .join('\n');
}

function readDocument(documentId) {
  const filePath = path.join(contentRoot, `${documentId}.md`);
  if (!fs.existsSync(filePath)) {
    fail(`sidebar document '${documentId}' does not exist.`);
  }

  const markdown = fs.readFileSync(filePath, 'utf8');
  return promoteHeadings(canonicalizeDocLinks(stripFrontmatter(markdown, documentId), documentId)).trim();
}

export function generate() {
  const sections = orderedDocuments().map(([section, documentIds]) => [
    `## ${section}`,
    '',
    ...documentIds.map(readDocument),
  ]);

  return [
    '# StaticNorth.Valiant — Full Documentation',
    '',
    '> Complete current documentation for StaticNorth.Valiant, source-generated .NET building blocks for validation, endpoint registration, mapping, and settings.',
    '',
    'This generated file contains the current documentation only. It intentionally excludes historical Docusaurus snapshots so a single context never mixes incompatible versions. Regenerate it with `npm run docs:generate-llms` whenever `docs/content` or `sidebars.js` changes.',
    '',
    ...sections.flatMap((section) => [...section, '']),
  ].join('\n');
}

function main() {
  const content = generate();
  const command = process.argv[2] ?? '--write';

  if (command === '--check') {
    if (!fs.existsSync(outputPath) || fs.readFileSync(outputPath, 'utf8') !== content) {
      fail('docs/static/llms-full.txt is stale. Run npm run docs:generate-llms.');
    }
    return;
  }

  if (command !== '--write') {
    fail(`unsupported command '${command}'. Use --write or --check.`);
  }

  fs.writeFileSync(outputPath, content);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}
