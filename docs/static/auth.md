# auth.md — Valiant public access

This policy covers the public documentation at https://valiant.staticnorth.dev.

## Authentication

Authentication is not required. All documentation, Markdown exports, discovery
files, and documentation tools are public and read-only. Clients can use them
without an account, API key, access token, Authorization header, or session cookie.

## Agent registration

Agent registration and credential provisioning are not required or offered.
There is no OAuth/OIDC authorization server, token endpoint, or protected
documentation resource. Agents can call the public interfaces directly:

- HTTP API: https://valiant.staticnorth.dev/openapi.json
- MCP: https://valiant.staticnorth.dev/mcp (Streamable HTTP)
- Documentation and access instructions: https://valiant.staticnorth.dev/agents

No scopes, credential exchange, or token revocation steps apply to these public
interfaces. The OpenAPI contract explicitly declares support for anonymous access.

## HTTP access

Use a descriptive application User-Agent, such as `my-docs-client/1.0`.
If the host returns an access denial or bot challenge, this documentation service
does not offer a sign-in or registration flow to resolve it.
