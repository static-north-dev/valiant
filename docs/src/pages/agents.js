import Heading from '@theme/Heading';
import Layout from '@theme/Layout';

export default function AgentAccess() {
  return (
    <Layout title="Documentation for agents" description="Read Valiant documentation through Markdown, authoring skills, MCP, WebMCP, or the public documentation API.">
      <main className="container margin-vert--xl">
        <Heading as="h1">Documentation for agents</Heading>
        <p>Public, read-only access to Valiant documentation. No account or API key is required.</p>
        <Heading as="h2">Choose the right version</Heading>
        <p>Start with <a href="/llms.txt">llms.txt</a> or the <a href="/agents/documents.json">document index</a>. The default is the latest published major. Select <code>current</code> explicitly for development documentation.</p>
        <Heading as="h2">Read Markdown</Heading>
        <p>Request a documentation page with <code>Accept: text/markdown</code>, or use its <code>markdownUrl</code> from the index. Module contexts cover <a href="/llms-validation.txt">Validation</a>, <a href="/llms-endpoints.txt">Endpoints</a>, <a href="/llms-mapping.txt">Mapping</a>, and <a href="/llms-settings.txt">Settings</a>.</p>
        <Heading as="h2">Use documentation tools</Heading>
        <p>Connect your MCP client to <code>https://valiant.staticnorth.dev/mcp</code> with Streamable HTTP. The tools are <code>list_documents</code>, <code>search_documents</code>, and <code>read_document</code>. Browsers supporting WebMCP expose the same tools.</p>
        <p>For HTTP clients, use the <a href="/openapi.json">OpenAPI contract</a>. The <a href="/.well-known/agent-skills/index.json">skills index</a> provides module authoring guidance with SHA-256 digests.</p>
        <p>Search returns excerpts; read the full page before using its C# examples. Custom HTTP clients should send a descriptive application User-Agent because Cloudflare Pages can reject library-default user agents.</p>
      </main>
    </Layout>
  );
}
