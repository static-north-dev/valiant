import {McpServer} from '@modelcontextprotocol/sdk/server/mcp.js';
import {WebStandardStreamableHTTPServerTransport} from '@modelcontextprotocol/sdk/server/webStandardStreamableHttp.js';
import {CfWorkerJsonSchemaValidator} from '@modelcontextprotocol/sdk/validation/cfworker-provider.js';
import {z} from 'zod';
import {documentationTools, executeDocumentationTool, moduleNames} from './documentation.mjs';

z.config({jitless: true});

export const serverInfo = {name: 'dev.staticnorth/valiant-docs', version: '1.0.0'};

export async function handleMcpRequest(request, loadIndex) {
  const origin = request.headers.get('Origin');
  if (origin && origin !== new URL(request.url).origin) {
    return Response.json({error: 'Origin is not allowed.'}, {status: 403});
  }
  if (request.method === 'OPTIONS') {
    return new Response(null, {status: 204});
  }
  if (request.method !== 'POST') {
    return Response.json({error: 'Method not allowed.'}, {status: 405, headers: {Allow: 'POST, OPTIONS'}});
  }
  const server = new McpServer(serverInfo, {jsonSchemaValidator: new CfWorkerJsonSchemaValidator()});
  const filters = {
    module: z.enum(moduleNames).optional(),
    version: z.string().max(32).optional(),
  };
  const schemas = {
    list_documents: filters,
    search_documents: {...filters, query: z.string().min(1).max(200), limit: z.number().int().min(1).max(20).optional()},
    read_document: {path: z.string().min(1).max(300)},
  };
  for (const tool of documentationTools) {
    server.registerTool(tool.name, {
      description: tool.description,
      inputSchema: schemas[tool.name],
      annotations: {readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false},
    }, async (args) => {
      try {
        const result = executeDocumentationTool(await loadIndex(), tool.name, args);
        return {content: [{type: 'text', text: JSON.stringify(result)}]};
      } catch (error) {
        return {isError: true, content: [{type: 'text', text: error instanceof TypeError || error instanceof RangeError ? error.message : 'Documentation is temporarily unavailable.'}]};
      }
    });
  }
  const transport = new WebStandardStreamableHTTPServerTransport({
    sessionIdGenerator: undefined,
    enableJsonResponse: true,
    maxRequestBodySize: 16 * 1024,
  });
  await server.connect(transport);
  try {
    return await transport.handleRequest(request);
  } finally {
    await server.close();
  }
}
