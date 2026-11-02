import {contentSignal, discoveryLinks, listDocuments, readDocument, searchDocuments} from './documentation.mjs';
import {handleMcpRequest} from './mcp.mjs';

export function prefersMarkdown(accept = '') {
  const ranges = accept.toLowerCase().split(',').map((range) => {
    const [type, ...parameters] = range.trim().split(';');
    const parameter = parameters.map((value) => value.trim()).find((value) => value.startsWith('q='));
    const quality = parameter === undefined ? 1 : /^q=(?:0(?:\.\d{0,3})?|1(?:\.0{0,3})?)$/.test(parameter) ? Number(parameter.slice(2)) : 0;
    return {type, quality};
  });
  if (!ranges.some(({type}) => type === 'text/markdown')) {
    return false;
  }
  function quality(type) {
    for (const candidate of [type, 'text/*', '*/*']) {
      const matches = ranges.filter((range) => range.type === candidate);
      if (matches.length) {
        return Math.max(...matches.map((range) => range.quality));
      }
    }
    return 0;
  }
  return quality('text/markdown') > 0 && quality('text/markdown') >= quality('text/html');
}

function withHeaders(response, {markdownPath, varyAccept = false, cors = false, noStore = false} = {}) {
  const headers = new Headers(response.headers);
  headers.set('Link', discoveryLinks + (markdownPath ? `, <${markdownPath}>; rel="alternate"; type="text/markdown"` : ''));
  headers.set('Content-Signal', contentSignal);
  headers.set('X-Content-Type-Options', 'nosniff');
  if (varyAccept) {
    headers.set('Vary', [...new Set((headers.get('Vary') ?? '').split(',').map((value) => value.trim()).filter(Boolean).concat('Accept'))].join(', '));
  }
  if (cors) {
    headers.set('Access-Control-Allow-Origin', '*');
    headers.set('Access-Control-Allow-Methods', 'GET, HEAD, POST, OPTIONS');
    headers.set('Access-Control-Allow-Headers', 'Content-Type, Accept, MCP-Protocol-Version, MCP-Session-Id');
  }
  if (noStore) {
    headers.set('Cache-Control', 'no-store');
  }
  return new Response(response.body, {status: response.status, statusText: response.statusText, headers});
}

export function createWorker({markdownRoutes}) {
  return {
    async fetch(request, env) {
      const url = new URL(request.url);
      if (['/mcp/server-card', '/.well-known/mcp/server-card.json'].includes(url.pathname)) {
        let response;
        if (request.method === 'OPTIONS') {
          response = new Response(null, {status: 204});
        } else if (['GET', 'HEAD'].includes(request.method)) {
          response = await env.ASSETS.fetch(request);
        } else {
          response = Response.json({error: 'Method not allowed.'}, {status: 405, headers: {Allow: 'GET, HEAD, OPTIONS'}});
        }
        const result = withHeaders(response, {cors: true});
        result.headers.set('Access-Control-Allow-Methods', 'GET, HEAD, OPTIONS');
        result.headers.set('Access-Control-Allow-Headers', 'Content-Type, If-None-Match');
        result.headers.set('Access-Control-Expose-Headers', 'ETag');
        if (result.ok && request.method !== 'OPTIONS') {
          result.headers.set('Content-Type', 'application/mcp-server-card+json');
        }
        return result;
      }
      const loadIndex = async () => {
        try {
          const response = await env.ASSETS.fetch(new Request(new URL('/agents/documents.json', url)));
          if (!response.ok) {
            throw new Error('Documentation index is unavailable.');
          }
          const index = await response.json();
          if (typeof index.defaultVersion !== 'string' || !Array.isArray(index.documents) || !Array.isArray(index.versions)) {
            throw new Error('Documentation index is invalid.');
          }
          return index;
        } catch (error) {
          throw new Error('Documentation index is unavailable.', {cause: error});
        }
      };
      const isApi = url.pathname === '/api/docs' || url.pathname.startsWith('/api/docs/');
      if (url.pathname === '/mcp' || isApi) {
        try {
          let response;
          if (url.pathname === '/mcp') {
            response = await handleMcpRequest(request, loadIndex);
          } else if (request.method === 'OPTIONS') {
            response = new Response(null, {status: 204});
          } else if (!['GET', 'HEAD'].includes(request.method)) {
            response = Response.json({error: 'Method not allowed.'}, {status: 405, headers: {Allow: 'GET, HEAD, OPTIONS'}});
          } else {
            const index = await loadIndex();
            const filters = Object.fromEntries(['module', 'version'].filter((key) => url.searchParams.has(key)).map((key) => [key, url.searchParams.get(key)]));
            switch (url.pathname) {
              case '/api/docs': response = Response.json(listDocuments(index, filters)); break;
              case '/api/docs/search': response = Response.json(searchDocuments(index, {
                ...filters,
                query: url.searchParams.get('query'),
                ...(url.searchParams.has('limit') ? {limit: Number(url.searchParams.get('limit'))} : {}),
              })); break;
              case '/api/docs/read': {
                const document = readDocument(index, {path: url.searchParams.get('path')});
                response = document ? Response.json(document) : Response.json({error: 'Document not found.'}, {status: 404});
                break;
              }
              case '/api/docs/status': response = Response.json({status: 'ok', defaultVersion: index.defaultVersion, sourceRevision: index.sourceRevision}); break;
              default: response = Response.json({error: 'Endpoint not found.'}, {status: 404});
            }
          }
          if (request.method === 'HEAD') {
            response = new Response(null, {status: response.status, headers: response.headers});
          }
          return withHeaders(response, {cors: true, noStore: true});
        } catch (error) {
          const status = error instanceof TypeError ? 400 : 503;
          return withHeaders(Response.json({error: status === 400 ? error.message : 'Documentation is temporarily unavailable.'}, {status}), {cors: true, noStore: true});
        }
      }
      const route = url.pathname.replace(/\/$/, '') || '/';
      const markdownPath = Object.hasOwn(markdownRoutes, route) ? markdownRoutes[route] : undefined;
      if (markdownPath && ['GET', 'HEAD'].includes(request.method)) {
        if (prefersMarkdown(request.headers.get('Accept') ?? '')) {
          const assetUrl = new URL(markdownPath, url);
          const response = await env.ASSETS.fetch(new Request(assetUrl, request));
          const result = withHeaders(response, {markdownPath, varyAccept: true});
          if (result.ok) {
            result.headers.set('Content-Type', 'text/markdown; charset=utf-8');
          }
          return result;
        }
        return withHeaders(await env.ASSETS.fetch(request), {markdownPath, varyAccept: true});
      }
      return env.ASSETS.fetch(request);
    },
  };
}
