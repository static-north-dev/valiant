import {contentSignal, discoveryLinks, listDocuments, readDocument, searchDocuments} from './documentation.mjs';
import {handleMcpRequest} from './mcp.mjs';
import {withSecurityHeaders} from './security.mjs';

function forRewrittenHtml(request) {
  const headers = new Headers(request.headers);
  headers.delete('If-None-Match');
  headers.delete('If-Modified-Since');
  headers.delete('Range');
  headers.delete('If-Range');
  return new Request(request, {headers});
}

export function prefersMarkdown(accept = '') {
  const ranges = accept.toLowerCase().split(',').map((range) => {
    const [type, ...parameters] = range.trim().split(';');
    const parameter = parameters.map((value) => value.trim()).find((value) => value.startsWith('q='));
    const quality = parameter === undefined ? 1 : /^q=(?:0(?:\.\d{0,3})?|1(?:\.0{0,3})?)$/.test(parameter) ? Number(parameter.slice(2)) : 0;
    return {type: type.trim(), quality};
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

function withHeaders(response, {markdownPath, varyAccept = false, corsMethods, noStore = false} = {}) {
  const headers = new Headers(response.headers);
  headers.append('Link', discoveryLinks + (markdownPath ? `, <${markdownPath}>; rel="alternate"; type="text/markdown"` : ''));
  headers.set('Content-Signal', contentSignal);
  headers.set('X-Content-Type-Options', 'nosniff');
  if (varyAccept) {
    const varies = (headers.get('Vary') ?? '').split(',').map((value) => value.trim()).filter(Boolean);
    if (varies.includes('*')) {
      headers.set('Vary', '*');
    } else if (!varies.some((value) => value.toLowerCase() === 'accept')) {
      headers.append('Vary', 'Accept');
    }
  }
  if (corsMethods) {
    headers.set('Access-Control-Allow-Origin', '*');
    headers.set('Access-Control-Allow-Methods', corsMethods);
    headers.set('Access-Control-Allow-Headers', 'Content-Type, Accept, MCP-Protocol-Version, MCP-Session-Id');
  }
  if (noStore) {
    headers.set('Cache-Control', 'no-store');
  }
  return new Response(response.body, {status: response.status, statusText: response.statusText, headers});
}

async function readDocumentationAsset(assets, url, path) {
  try {
    const response = await assets.fetch(new Request(new URL(path, url)));
    if (!response.ok) throw new Error('Documentation asset is unavailable.');
    const asset = await response.json();
    if (typeof asset?.defaultVersion !== 'string') throw new Error('Documentation asset is invalid.');
    return asset;
  } catch (error) {
    throw new Error('Documentation asset is unavailable.', {cause: error});
  }
}

export function createWorker({markdownRoutes}) {
  const indexes = new WeakMap();
  const worker = {
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
        const result = withHeaders(response, {corsMethods: 'GET, HEAD, OPTIONS'});
        result.headers.set('Access-Control-Allow-Headers', 'Content-Type, If-None-Match');
        result.headers.set('Access-Control-Expose-Headers', 'ETag');
        if (result.ok && request.method !== 'OPTIONS') {
          result.headers.set('Content-Type', 'application/mcp-server-card+json');
        }
        return result;
      }
      const loadIndex = () => {
        let index = indexes.get(env.ASSETS);
        if (!index) {
          index = readDocumentationAsset(env.ASSETS, url, '/agents/documents.json').then((data) => {
            if (!Array.isArray(data.documents) || !Array.isArray(data.versions)) {
              throw new Error('Documentation index is invalid.');
            }
            return data;
          }).catch((error) => {
            indexes.delete(env.ASSETS);
            throw error;
          });
          indexes.set(env.ASSETS, index);
        }
        return index;
      };
      const isApi = url.pathname === '/api/docs' || url.pathname.startsWith('/api/docs/');
      if (url.pathname === '/mcp' || isApi) {
        const apiHeaders = {corsMethods: isApi ? 'GET, HEAD, OPTIONS' : 'POST, OPTIONS', noStore: true};
        try {
          let response;
          if (url.pathname === '/mcp') {
            response = await handleMcpRequest(request, loadIndex);
          } else if (request.method === 'OPTIONS') {
            response = new Response(null, {status: 204});
          } else if (!['GET', 'HEAD'].includes(request.method)) {
            response = Response.json({error: 'Method not allowed.'}, {status: 405, headers: {Allow: 'GET, HEAD, OPTIONS'}});
          } else if (url.pathname === '/api/docs/status') {
            const {defaultVersion, sourceRevision} = await readDocumentationAsset(env.ASSETS, url, '/agents/documents-status.json');
            response = Response.json({status: 'ok', defaultVersion, sourceRevision});
          } else if (['/api/docs', '/api/docs/search', '/api/docs/read'].includes(url.pathname)) {
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
            }
          } else {
            response = Response.json({error: 'Endpoint not found.'}, {status: 404});
          }
          return withHeaders(response, apiHeaders);
        } catch (error) {
          const status = error instanceof TypeError ? 400 : 503;
          return withHeaders(Response.json({error: status === 400 ? error.message : 'Documentation is temporarily unavailable.'}, {status}), apiHeaders);
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
        return withHeaders(await env.ASSETS.fetch(forRewrittenHtml(request)), {markdownPath, varyAccept: true});
      }
      const html = ['GET', 'HEAD'].includes(request.method) &&
        (url.pathname.endsWith('.html') || request.headers.get('Accept')?.includes('text/html'));
      return env.ASSETS.fetch(html ? forRewrittenHtml(request) : request);
    },
  };
  return {async fetch(request, env) {
    const response = await worker.fetch(request, env);
    if (request.method === 'HEAD' && response.body) {
      await response.body.cancel();
      return withSecurityHeaders(new Response(null, response));
    }
    return withSecurityHeaders(response);
  }};
}
