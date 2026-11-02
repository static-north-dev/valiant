# Valiant Docs

This directory contains the Docusaurus documentation site for StaticNorth.Valiant.

Use Node.js 22 or newer. The pinned Wrangler CLI used by both local previews
and deployment requires Node.js 22.

The site uses one docs plugin instance for the complete Valiant release.
Validation, Endpoints, Mapping, and Settings remain separate navigation
sections, but share one version inventory, snapshot, and version selector.

## Commands

```bash
npm install
npm run start
npm run build
npm run serve
npm run preview:pages
npm run docs:check-agent-artifacts
npm run docs:check-agent-http -- http://127.0.0.1:3091
```

## LLM and crawler files

The static site publishes these root files:

- `/llms.txt`: a concise, curated entry point for language models and agents;
- `/auth.md`: the public access policy. Authentication and agent registration
  are not required or offered; the OpenAPI contract explicitly supports
  anonymous access through `security: [{}]`. This policy does not claim an
  OAuth server or a WorkOS Auth.md registration implementation, and does not
  guarantee a passing result for scanners that require those capabilities;
- `/llms-full.txt`: the complete default published documentation in one Markdown
  context, with an explicit version and source revision;
- `/llms-{validation,endpoints,mapping,settings}.txt`: smaller contexts for the
  same default published version;
- `/agents/documents.json`: an index of every public document in every built
  version, including canonical page paths, Markdown URLs, and complete content;
- `/markdown/...`: individual Markdown pages generated from the exact sources
  used for the corresponding HTML routes, including older majors and local
  development previews;
- `/robots.txt`: allows indexing and points at Docusaurus's generated
  `/sitemap.xml` and agent catalog. Content Signals permit search and AI input;
  training permission is deliberately left unspecified.

`static/llms-full.txt` remains a committed **authoring** context generated from
`content` in sidebar order. Run these commands before committing a content or
sidebar change:

```bash
npm run docs:generate-llms
npm run docs:check-llms
```

During builds, `plugins/agent-docs` uses Docusaurus's loaded version metadata
to generate the deployed index and overwrite the built `llms-full.txt` with the
actual default version. Production never serves development content under a
stable URL merely because `content` has advanced past a published snapshot.
Relative Markdown links resolve within their source version; fenced code is
preserved. Draft and unlisted pages are excluded from all agent exports.

Four source-controlled module skills live in `agent-skills`. The build copies
them to `/.well-known/agent-skills/` and computes the v0.2.0 discovery index's
SHA-256 digests from the exact published bytes. The build also publishes an
API Catalog, an MCP server card, and an ARD manifest. `ard.json` is the current
ARD discovery path; `ai-catalog.json` is a compatibility alias for scanners using
the predecessor path. The Poppy draft 0.3 manifest advertises only ordinary
signed-out website browsing (`web: {}`); it does not claim OAuth or sessions.

## Documentation tools

The build bundles a stateless Pages Worker into `_worker.js`. It uses only the
default `ASSETS` binding and needs no KV, database, credentials, or external
service. All tool data comes from the built documentation index:

- `GET /api/docs?module=validation&version=1`: list page metadata and versions;
- `GET /api/docs/search?query=composition&limit=8`: search with bounded excerpts;
- `GET /api/docs/read?path=/validation/quick-start`: read one indexed page;
- `GET /api/docs/status`: check index availability;
- `POST /mcp`: the same list/search/read tools through the official MCP SDK's
  stateless Streamable HTTP transport;
- WebMCP-capable browsers register the same three read-only tools at page load.

The HTTP API contract is `/openapi.json`; `/agents` explains the public client
interfaces. List and search default to the published major. Other versions,
including `current` in local builds, must be selected explicitly. Read accepts
only paths present in the generated index and never fetches a caller's URL.
The MCP endpoint accepts no session state, limits input bodies to 16 KiB,
validates origins, and does not offer a persistent GET/SSE stream. Unknown paths
and failed index loads return explicit errors.

Requests for published HTML pages with `Accept: text/markdown` receive the
matching Markdown when its quality is at least the HTML preference and is not
zero. Ordinary browser and wildcard requests retain HTML. Both variants use
`Vary: Accept`, and the Worker adds response headers directly because Pages
`_headers` rules do not apply to Function-generated responses. Static assets
bypass the Worker where possible through `_routes.json`.

Both static assets and Worker responses publish `X-Frame-Options: DENY` and a
Content Security Policy with `frame-ancestors 'none'`, `object-src 'none'`,
same-origin resource defaults, and restricted base/form destinations. HTML
responses receive a fresh 128-bit nonce on every script through HTMLRewriter;
their CSP permits same-origin scripts and that nonce, with inline event handlers
and eval disabled. This also lets Cloudflare attach the nonce to its injected
bot-detection scripts. Styles allow inline declarations used by React and Prism.
Child frames are limited to this origin and Cloudflare's challenge origin.

HTML is served with `Cache-Control: private, no-store` and without conditional
asset validators so a cached page cannot reuse a nonce or receive mismatched
script/CSP values after a 304. Other assets and Markdown retain their caching.
The Pages HTTP verifier checks security headers and matching, distinct HTML
nonces against the real Worker runtime.

`npm run serve` previews **static files only**. Use `npm run preview:pages` to
exercise negotiation and the HTTP/MCP endpoints in Wrangler/workerd. It uses
the same pinned Wrangler version as deployment. `wrangler.toml` pins a supported
compatibility date instead of implicitly changing runtime behavior each day.

`npm run docs:check-agent-artifacts` validates the built files, version metadata,
skill digests, and discovery targets. CI and publication run it after building;
the deploy job runs the same dependency-free check on the downloaded artifact.
The site artifact explicitly includes hidden files so `.well-known` survives
upload/download. Repository tests cover version isolation, HTTP negotiation,
invalid input, SDK client calls, WebMCP registration, and missing/tampered files.
Publication then runs `docs:check-agent-http` against the exact `pages.dev`
deployment URL returned by Wrangler, including Markdown and HTML variants,
exact skill digests, and MCP protocol calls. It verifies the index's source
revision matches the persisted docs commit and fails if Wrangler returns no
deployment URL. This validates the deployed artifact and runtime independently
of the custom domain's bot protection. The built Worker includes license
notices for its bundled dependencies.

Custom-domain access is checked separately:

```bash
npm run docs:check-agent-http -- https://valiant.staticnorth.dev
```

Bot Fight Mode can remain enabled on `staticnorth.dev`. A successful Pages
deployment check does not establish that the custom domain accepts every bot
or CI runner; its browser challenges remain visible in the separate check.

The deploy job uses Node.js 22, matching the other documentation jobs. HTTP
verification reports the method, URL, response type, Cloudflare Ray ID and
challenge/error markers on failure without dumping the response body. Access
denials (401/403) fail immediately; deployment readiness failures still retry.
An access denial from a GitHub runner can occur even when the same public URL
works from another network. Use the Ray ID to investigate the host's access
and bot rules; a successful deployment is not proof that every client can read it.

Cloudflare Pages can reject library-default HTTP user agents with error 1010
before the Worker runs. Custom clients should send a descriptive application
User-Agent. This repository cannot change Cloudflare-managed bot filtering or
publish DNS-AID records. The live scanner's score must be verified after the
normal **Publish Docs** deployment; draft-protocol checks may lag the versions
published here.

## Cloudflare Pages

The site builds static files plus its bundled Pages request handler; no
Cloudflare adapter or additional binding configuration is required. CI builds
the site for pushes and pull requests targeting `main`, but never deploys it.

Documentation publication is independent from NuGet releases. Run the
**Publish Docs** workflow manually whenever the site should be refreshed. It
pins the current `main` commit, snapshots the major tracked in
`current-major.json`, merges it into the complete history on `main`, ensures
the `valiant-docs` Cloudflare Pages project exists, and deploys the exact
persisted commit.

The workflow creates a missing Pages project with production branch `main`.
The deployed static output is `docs/build`.

Add these GitHub Actions repository secrets:

- `CLOUDFLARE_ACCOUNT_ID`: the Cloudflare **account ID**, not the zone ID;
- `CLOUDFLARE_API_TOKEN`: a custom token with
  **Account → Cloudflare Pages → Edit** permission. Its resource scope must
  include the account identified by `CLOUDFLARE_ACCOUNT_ID`; selecting a
  domain or zone such as `staticnorth.dev` does not grant access to the
  account-level Pages API.

The deployment job also grants its generated `GITHUB_TOKEN`
`deployments: write`, allowing Wrangler Action to create a GitHub Deployment
and attach the Cloudflare Pages URL.

The snapshot persistence job grants its `GITHUB_TOKEN` only `contents: write`.
Repository rules for `main` must allow `github-actions[bot]` to push this
generated documentation commit. If `main` requires pull requests, signed
commits, status checks, or another rule the bot cannot satisfy, add a narrow
ruleset bypass for GitHub Actions or replace the token with a narrowly scoped
GitHub App credential. A broad personal access token is not recommended.

## Versioning

The default docs plugin reads all release documentation from `content`:

- Validation: `content/validation`
- Endpoints: `content/endpoints`
- Mapping: `content/mapping`
- Settings: `content/settings`

Documentation versions are SemVer majors only. Major `1` represents the latest
published `1.x` documentation, not only the original `1.0.0` content.
`current-major.json` is the reviewed source of truth for the content currently
being authored. The **Publish Docs** workflow refreshes one snapshot containing
all four sections. Change that file to `"2"` before the first major 2
publication; later major 2 runs refresh only major 2 and preserve every older
major.

The local commands remain useful for previewing or diagnosing versioning, but
they are not a publication prerequisite:

```bash
npm run docs:current-major
npm run docs:snapshot-major -- 1
npm run docs:validate-major -- 1
npm run build
```

The snapshot command refreshes an existing major without reordering it, creates
a newer major when needed, preserves all other majors, and rolls back partial
changes if the unified snapshot fails. A valid snapshot must contain all four
section indexes and all four sidebar definitions. The publication workflow's
import command accepts the constrained snapshot artifact, rejects unexpected
paths and symlinks, replaces only the requested major, and preserves the
remaining history.

Production builds set `DOCS_INCLUDE_CURRENT=false`, so only released numeric
major versions appear. Normal local and CI builds retain the `current` docs
version for previewing changes before they merge to `main`.

The newest released major uses `/validation/`, `/endpoints/`, `/mapping/`, and
`/settings/`. Older majors use `/<major>/<section>/`, such as
`/1/endpoints/`. Section navigation follows the active shared version.

The navbar version selector intentionally remains a dropdown when only one
released major exists. Docusaurus normally renders a single version as a plain
link, so `src/theme/NavbarItem/DocsVersionDropdownNavbarItem` contains a narrow
override of the Docusaurus 3.10.2 component. When upgrading Docusaurus, diff
that override against the new upstream component before changing the pinned
version.

## Publishing documentation

The **Publish Docs** workflow is manually triggered and always publishes the
latest `main` content for the major in `current-major.json`. No GitHub
Release is required. The workflow must exist on the repository's default
branch. To publish:

1. Merge the desired documentation and, when beginning a new major, the
   `current-major.json` change into `main`.
2. Open **Actions → Publish Docs → Run workflow**.
3. The workflow pins the starting `main` SHA, tests and audits the docs,
   creates the shared major snapshot, and validates every generated route.
4. It verifies or creates the Cloudflare Pages project, commits only
   `versions.json`, `versioned_docs`, and `versioned_sidebars` to `main`,
   rebuilds the exact persisted commit, and deploys that verified artifact.

If `main` moves before the generated snapshot is persisted, the workflow
fails instead of publishing stale documentation on top of newer content. Run it
again to publish the new head. A no-change rerun still verifies and deploys the
existing persisted commit.

Repository rules for `main` must allow `github-actions[bot]` to push the
generated snapshot commit. The job has only `contents: write`; it cannot bypass
required pull requests, signed commits, or status checks. Add a narrow ruleset
bypass for GitHub Actions or replace the token with a narrowly scoped GitHub App
credential when those protections are enabled.

Commits pushed with the repository `GITHUB_TOKEN` do not recursively trigger
the `push` CI workflow. The manual workflow performs its own versioning tests,
audit, production build, and route checks before deployment.

## Publishing NuGet releases

Publishing a GitHub Release only publishes NuGet packages. The release tag—not
the release title—sets the package version. Tags must be SemVer without build
metadata and with an optional `v` prefix, such as `v1.2.0` or
`1.2.0-preview.1`. The workflow checks out the immutable tag, restores, builds,
tests, audits, verifies package consumers, runs Native AOT smoke verification,
and publishes the verified packages through NuGet trusted publishing. Those
commands use `StaticNorth.Valiant.Release.slnf`, which contains every .NET project but
deliberately excludes the documentation `.esproj`; a release cannot build or
deploy the site indirectly.

Update the NuGet.org trusted-publishing policy to use
`.github/workflows/release.yml`; trusted-publishing policies can be bound to the
workflow filename. Configure the repository variable `NUGET_USER` for releases.
Configure `CLOUDFLARE_ACCOUNT_ID` and `CLOUDFLARE_API_TOKEN` only for the
separate documentation workflow.

Rerunning a partially failed release is safe because NuGet publication uses
`--skip-duplicate`. CI and documentation publication fail on high or critical
npm advisories, with no advisory exceptions. Compatible security patches for
transitive dependencies are pinned in `package.json` overrides and the lockfile,
including `image-size` 2.0.4, which replaces the previously exempt vulnerable
image parsers. Overrides for packages such as `brace-expansion` and `nanoid`
stay on the latest compatible release line required by their consumers;
forcing their newest majors would change the APIs expected by the build tools.
Tinypool 2.2.0 replaces Docusaurus's older worker pool to remove its critical
advisories; its major-version migration drops Node.js 18 and is covered by the
docs builds. Every workflow job that invokes a Docusaurus build has a 20-minute
timeout. Lower-severity findings in Docusaurus's build/development dependency
tree remain visible in `npm audit`; they are not added to an exception list.
