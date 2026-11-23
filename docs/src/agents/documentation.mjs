export const moduleNames = ['validation', 'endpoints', 'mapping', 'settings'];

export const discoveryLinks = [
  '</llms.txt>; rel="describedby"; type="text/plain"',
  '</.well-known/api-catalog>; rel="api-catalog"; type="application/linkset+json"',
  '</.well-known/ard.json>; rel="ard"; type="application/json"',
  '</.well-known/agent-skills/index.json>; rel="https://agentskills.io/discovery"; type="application/json"',
].join(', ');

export const contentSignal = 'search=yes, ai-input=yes';

const filterProperties = {
  module: {type: 'string', enum: moduleNames, description: 'Limit results to one Valiant module.'},
  version: {type: 'string', maxLength: 32, description: 'Documentation version. Defaults to the latest published major. Use a version advertised by list_documents.'},
};

export const documentationTools = [
  {
    name: 'list_documents',
    description: 'List Valiant documentation pages and available versions. Defaults to the published major without mixing development or historical APIs.',
    inputSchema: {type: 'object', properties: filterProperties, additionalProperties: false},
  },
  {
    name: 'search_documents',
    description: 'Search Valiant documentation and return bounded excerpts with source URLs. Filter by module or documentation version.',
    inputSchema: {
      type: 'object',
      properties: {
        query: {type: 'string', minLength: 1, maxLength: 200},
        ...filterProperties,
        limit: {type: 'integer', minimum: 1, maximum: 20, default: 8},
      },
      required: ['query'],
      additionalProperties: false,
    },
  },
  {
    name: 'read_document',
    description: 'Read a complete Valiant documentation page as Markdown, preserving C# examples. Use an exact page path returned by list_documents or search_documents.',
    inputSchema: {
      type: 'object',
      properties: {path: {type: 'string', minLength: 1, maxLength: 300}},
      required: ['path'],
      additionalProperties: false,
    },
  },
];

function metadata({content, ...document}) {
  return document;
}

function selectDocuments(index, {module, version = index.defaultVersion} = {}) {
  if (module !== undefined && !moduleNames.includes(module)) {
    throw new TypeError('module must be validation, endpoints, mapping, or settings.');
  }
  if (!index.versions.some((item) => item.name === version)) {
    throw new TypeError('Unknown documentation version. Use list_documents to discover versions.');
  }
  return index.documents.filter((document) => document.version === version && (!module || document.module === module));
}

export function listDocuments(index, args = {}) {
  return {
    defaultVersion: index.defaultVersion,
    versions: index.versions,
    documents: selectDocuments(index, args).map(metadata),
  };
}

export function searchDocuments(index, {query, limit = 8, ...filters} = {}) {
  if (typeof query !== 'string' || !query.trim() || query.length > 200) {
    throw new TypeError('query must contain 1 to 200 characters.');
  }
  if (!Number.isInteger(limit) || limit < 1 || limit > 20) {
    throw new TypeError('limit must be an integer from 1 to 20.');
  }
  const terms = [...new Set(query.toLowerCase().match(/[\p{L}\p{N}_.#]+/gu) ?? [])];
  if (!terms.length) {
    throw new TypeError('query must contain a word or identifier.');
  }
  const matches = selectDocuments(index, filters).flatMap((document) => {
    const text = document.content.toLowerCase();
    if (!terms.every((term) => text.includes(term))) {
      return [];
    }
    const title = document.title.toLowerCase();
    const description = document.description.toLowerCase();
    const score = terms.reduce((total, term) => total + (title.includes(term) ? 8 : 0) + (description.includes(term) ? 3 : 0) + 1, 0);
    const start = Math.max(0, text.indexOf(terms[0]) - 100);
    return [{...metadata(document), score, excerpt: document.content.slice(start, start + 500)}];
  });
  matches.sort((left, right) => right.score - left.score || left.path.localeCompare(right.path));
  return {total: matches.length, results: matches.slice(0, limit)};
}

export function readDocument(index, {path} = {}) {
  if (typeof path !== 'string' || !path.startsWith('/') || path.length > 300) {
    throw new TypeError('path must be a documentation page path of at most 300 characters.');
  }
  return index.documents.find((document) => document.path.replace(/\/$/, '') === path.replace(/\/$/, '')) ?? null;
}

export function executeDocumentationTool(index, name, args) {
  switch (name) {
    case 'list_documents': return listDocuments(index, args);
    case 'search_documents': return searchDocuments(index, args);
    case 'read_document': {
      const document = readDocument(index, args);
      if (!document) {
        throw new RangeError('Document not found. Use list_documents to discover page paths.');
      }
      return document;
    }
    default: throw new TypeError('Unknown documentation tool.');
  }
}
