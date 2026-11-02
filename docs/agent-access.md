# Valiant documentation for agents

Valiant publishes public, read-only documentation. No account, API key, or OAuth token is required.
The [public access policy](https://valiant.staticnorth.dev/auth.md) explains authentication and agent registration.

- Start at [llms.txt](https://valiant.staticnorth.dev/llms.txt).
- Read [the document index](https://valiant.staticnorth.dev/agents/documents.json) to find available versions and exact page paths. The default is the latest published major. Select `current` explicitly for development docs.
- Fetch a page with `Accept: text/markdown`, or use its `markdownUrl` from the index.
- Read a module context at `/llms-validation.txt`, `/llms-endpoints.txt`, `/llms-mapping.txt`, or `/llms-settings.txt`. These contain only the default published version.
- Discover authoring skills at [the skills index](https://valiant.staticnorth.dev/.well-known/agent-skills/index.json). Verify the artifact's SHA-256 digest before using a skill.
- Connect an MCP client to `https://valiant.staticnorth.dev/mcp` using Streamable HTTP. It exposes `list_documents`, `search_documents`, and `read_document`.
- Browsers supporting WebMCP expose the same three read-only tools on page load.
- Use [the OpenAPI contract](https://valiant.staticnorth.dev/openapi.json) for HTTP access through `/api/docs`, `/api/docs/search?query=...`, and `/api/docs/read?path=...`.

Search returns bounded excerpts. Read a complete page before copying C# examples, and match the documentation version to the installed package major.

For custom HTTP clients on Cloudflare Pages, send a descriptive application User-Agent such as `my-docs-client/1.0`. Cloudflare may reject library-default user agents, including Python urllib, before requests reach the application.
